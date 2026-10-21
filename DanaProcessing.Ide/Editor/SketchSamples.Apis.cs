using DanaProcessing.Ide.Localization;

namespace DanaProcessing.Ide.Editor
{
    /// <summary>
    /// Samples built on free public web APIs (no key needed), from the
    /// public-apis/public-apis list. Each one downloads on a background
    /// thread and falls back to clearly-labelled made-up data when offline.
    /// </summary>
    public static partial class SketchSamples
    {
        private static readonly SketchSample[] Apis =
        {
            new SketchSample(
                Loc.Tr("Terremotos del planeta en vivo (USGS)", "The planet's earthquakes, live (USGS)"),
                Loc.Tr("Un globo de caracteres ASCII con cada terremoto de las últimas 24 horas (o de la semana) saliendo de él como una columna de glifos: la altura es la magnitud y el color la profundidad. Los datos son el feed en tiempo real del Servicio Geológico de EE.UU., gratis y sin clave, descargado con HttpClient en un hilo aparte y actualizado cada 5 minutos. Con la semana (W) los terremotos dibujan solos los bordes de las placas, como el Anillo de Fuego. R repite el día en 40 segundos, con un anillo y un sonido por cada sismo. Arrastrá para girar, pasá el mouse por una columna para ver dónde y cuándo fue. Sin conexión muestra sismos inventados y lo avisa.",
                       "A globe of ASCII characters with every earthquake of the last 24 hours (or week) standing out of it as a column of glyphs: height is magnitude, color is depth. The data is the U.S. Geological Survey's real-time feed, free and keyless, downloaded with HttpClient on a background thread and refreshed every 5 minutes. With the week (W) the quakes trace the plate boundaries by themselves, like the Ring of Fire. R replays the day in 40 seconds, with a ring and a sound for each quake. Drag to turn, hover a column to see where and when. Offline it shows made-up quakes and says so."),
                QuakeGlobe,
                new[] { SampleTag.Data, SampleTag.ThreeD, SampleTag.Audio, SampleTag.Interaction }),
        };

        private const string QuakeGlobe =
@"// The planet's earthquakes, live: a globe of ASCII characters with every earthquake
// of the last day (or week) standing out of it as a column of glyphs.
//
// The data comes from the USGS (U.S. Geological Survey) real-time feeds: free, no key,
// updated every minute. https://earthquake.usgs.gov/earthquakes/feed/v1.0/geojson.php
// It's GeoJSON: for each quake, its magnitude, depth, place and time, and where it was.
// It's downloaded on a background thread (HttpClient), so the globe never stutters while
// it waits, and again every 5 minutes.
//
// - Column height: the magnitude (every point of magnitude is ~10x more shaking, so the
//   height grows faster than the number). Color: the depth -- shallow ones yellow, deep
//   ones (hundreds of km down, where one plate dives under another) red and violet.
// - Look at the week's feed (W): nobody drew the plate boundaries on this globe, but the
//   quakes trace them anyway -- the Ring of Fire around the Pacific shows up by itself.
// - Replay (R): the day plays again in 40 seconds, each quake appearing when it happened,
//   with a ring spreading from it and a sound: lower and louder the bigger it was.
//
// Drag: turn the globe.  Wheel: zoom.  Hover a column: what, where, when.
// D / W: last day / last week.  R: replay.  Space: stop the spin.
// No connection? It shows made-up quakes along the plate boundaries, and says so.
public class QuakeGlobe : Sketch
{
    // ---- settings ------------------------------------------------------------------------
    const string FeedDay = ""https://earthquake.usgs.gov/earthquakes/feed/v1.0/summary/all_day.geojson"";
    const string FeedWeek = ""https://earthquake.usgs.gov/earthquakes/feed/v1.0/summary/2.5_week.geojson"";
    const float Radius = 10;
    const float ReplaySeconds = 40;

    record Quake(float Lat, float Lon, float DepthKm, float Mag, string Place, DateTime Time);

    // ---- data (filled by the download thread, swapped in on the main thread) -------------
    List<Quake> quakes = new();
    volatile List<Quake> arrived;
    volatile string arrivedStatus;
    string status = ""downloading..."";
    bool week, demo;
    long lastFetch = -999999;
    volatile bool fetching;

    // ---- view ----------------------------------------------------------------------------
    PFont mono;
    float yaw = 2.6f, pitch = 0.35f, zoom = 1, spinSpeed = 0.0015f;
    bool spinning = true;
    readonly List<PVector> surface = new();
    float replayT = -1;                                     // -1: not replaying
    readonly List<(Quake Q, float Born)> rings = new();
    int hovered = -1;

    // ---- drawing buffers -----------------------------------------------------------------
    const int MaxItems = 20000;
    readonly float[] wx = new float[MaxItems], wy = new float[MaxItems], wz = new float[MaxItems];
    readonly int[] wcol = new int[MaxItems], wquake = new int[MaxItems];
    readonly float[] walpha = new float[MaxItems], wscale = new float[MaxItems];
    readonly char[] wglyph = new char[MaxItems];
    readonly float[] depthKey = new float[MaxItems], sx = new float[MaxItems], sy = new float[MaxItems], ss = new float[MaxItems];
    readonly int[] order = new int[MaxItems];
    int items;

    public override void Setup()
    {
        Size(960, 540);
        mono = CreateFont(""Consolas"", 32);
        // The globe itself: points spread evenly (a Fibonacci spiral), plus meridians and parallels.
        const int dots = 2600;
        for (int k = 0; k < dots; k++)
        {
            float y = 1 - 2 * (k + 0.5f) / dots, r = Sqrt(1 - y * y), a = k * 2.39996f;
            surface.Add(new PVector(Cos(a) * r, y, Sin(a) * r));
        }
        for (int lat = -60; lat <= 60; lat += 30)
            for (int lon = 0; lon < 360; lon += 3)
                surface.Add(LatLon(lat, lon, 1));
        for (int lon = 0; lon < 360; lon += 30)
            for (int lat = -87; lat <= 87; lat += 3)
                surface.Add(LatLon(lat, lon, 1));
    }

    // =====================================================================================
    // Data
    // =====================================================================================
    void Fetch()
    {
        if (fetching) return;
        fetching = true;
        lastFetch = Millis();
        string url = week ? FeedWeek : FeedDay;
        System.Threading.Tasks.Task.Run(async () =>
        {
            try
            {
                using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(15) };
                http.DefaultRequestHeaders.UserAgent.ParseAdd(""DanaProcessing-sample/1.0"");
                string json = await http.GetStringAsync(url);
                var list = Parse(json);
                arrivedStatus = $""USGS · {list.Count} earthquakes, {(week ? ""magnitude 2.5+ in the last 7 days"" : ""all in the last 24 hours"")} · updated {DateTime.Now:HH:mm}"";
                demo = false;
                arrived = list;
            }
            catch (Exception e)
            {
                if (quakes.Count == 0 || demo)
                {
                    arrivedStatus = $""no connection ({e.GetType().Name}) · showing MADE-UP quakes along the plate boundaries"";
                    demo = true;
                    arrived = DemoQuakes();
                }
                else
                    arrivedStatus = status + "" · last update failed, keeping these"";
            }
            finally { fetching = false; }
        });
    }

    /// <summary>USGS GeoJSON: features[] -> properties { mag, place, time (ms) } + geometry.coordinates [lon, lat, depth].</summary>
    static List<Quake> Parse(string json)
    {
        var list = new List<Quake>();
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        foreach (var f in doc.RootElement.GetProperty(""features"").EnumerateArray())
        {
            var p = f.GetProperty(""properties"");
            var c = f.GetProperty(""geometry"").GetProperty(""coordinates"");
            if (p.GetProperty(""mag"").ValueKind != System.Text.Json.JsonValueKind.Number) continue;
            float mag = (float)p.GetProperty(""mag"").GetDouble();
            string place = p.TryGetProperty(""place"", out var pl) && pl.ValueKind == System.Text.Json.JsonValueKind.String ? pl.GetString() : ""?"";
            var time = DateTimeOffset.FromUnixTimeMilliseconds(p.GetProperty(""time"").GetInt64()).LocalDateTime;
            list.Add(new Quake((float)c[1].GetDouble(), (float)c[0].GetDouble(), (float)c[2].GetDouble(), mag, place, time));
        }
        list.Sort((a, b) => a.Time.CompareTo(b.Time));
        return list;
    }

    /// <summary>Offline: invented quakes scattered along rough plate boundaries (clearly labelled as such).</summary>
    List<Quake> DemoQuakes()
    {
        (float Lat, float Lon)[][] lines =
        {
            new[] { (60f, -150f), (52f, -175f), (52f, 160f), (44f, 147f), (35f, 141f), (24f, 125f), (14f, 121f), (-5f, 130f), (-8f, 115f), (-3f, 101f), (5f, 95f) },  // Ring of Fire, west
            new[] { (60f, -150f), (48f, -126f), (34f, -118f), (18f, -103f), (12f, -88f), (5f, -79f), (-10f, -78f), (-25f, -71f), (-40f, -74f), (-55f, -70f) },     // Ring of Fire, east
            new[] { (-5f, 130f), (-18f, 168f), (-25f, -178f), (-40f, 176f), (-48f, 166f) },                                                                       // Tonga, New Zealand
            new[] { (36f, 27f), (38f, 44f), (35f, 58f), (30f, 70f), (29f, 84f), (27f, 96f) },                                                                     // Alpine-Himalayan belt
            new[] { (64f, -18f), (40f, -29f), (15f, -45f), (0f, -20f), (-30f, -13f), (-50f, -5f) },                                                              // Mid-Atlantic ridge
        };
        var list = new List<Quake>();
        var start = DateTime.Now.AddHours(-24);
        for (int k = 0; k < 220; k++)
        {
            var line = lines[(int)Random(lines.Length)];
            int seg = (int)Random(line.Length - 1);
            float t = Random(1);
            float lat = Lerp(line[seg].Lat, line[seg + 1].Lat, t) + Random(-2, 2);
            float lon = Lerp(line[seg].Lon, line[seg + 1].Lon, t) + Random(-2, 2);
            float mag = 1 + Pow(Random(1), 3) * 5.5f;
            list.Add(new Quake(lat, lon, Pow(Random(1), 2) * 600, mag, ""made-up quake (offline demo)"", start.AddMinutes(Random(24 * 60))));
        }
        list.Sort((a, b) => a.Time.CompareTo(b.Time));
        return list;
    }

    // =====================================================================================
    // Each frame
    // =====================================================================================
    public override void Draw()
    {
        if (arrived != null)
        {
            quakes = arrived;
            status = arrivedStatus;
            arrived = null;
        }
        if (Millis() - lastFetch > 5 * 60 * 1000) Fetch();
        if (spinning) yaw += spinSpeed;

        Background(4, 6, 14);
        BuildScene();
        Render();
        Hud();
    }

    static PVector LatLon(float lat, float lon, float r)
    {
        float la = lat * MathF.PI / 180, lo = lon * MathF.PI / 180;
        return new PVector(MathF.Cos(la) * MathF.Sin(lo) * r, MathF.Sin(la) * r, -MathF.Cos(la) * MathF.Cos(lo) * r);
    }

    void BuildScene()
    {
        items = 0;
        foreach (var p in surface)
            Add(p.X * Radius, p.Y * Radius, p.Z * Radius, '.', Pack(70, 110, 170), 0.55f, 0.5f, -1);

        // Replay: the clock runs through the period in 40 s; quakes appear as their time comes.
        DateTime now = DateTime.Now;
        DateTime first = quakes.Count > 0 ? quakes[0].Time : now, last = quakes.Count > 0 ? quakes[^1].Time : now;
        DateTime clock = now;
        if (replayT >= 0)
        {
            float before = replayT;
            replayT += 1 / 60f;
            clock = first + TimeSpan.FromTicks((long)((last - first).Ticks * Min(1, replayT / ReplaySeconds)));
            DateTime prevClock = first + TimeSpan.FromTicks((long)((last - first).Ticks * Min(1, before / ReplaySeconds)));
            foreach (var q in quakes)
                if (q.Time > prevClock && q.Time <= clock)
                {
                    rings.Add((q, Millis() / 1000f));
                    Rumble(q);
                }
            if (replayT > ReplaySeconds + 3) replayT = -1;
        }

        for (int k = 0; k < quakes.Count; k++)
        {
            var q = quakes[k];
            if (q.Time > clock) continue;
            float hours = (float)(clock - q.Time).TotalHours;
            float h = 0.3f + Pow(Max(0, q.Mag), 1.6f) * 0.28f;         // column height
            int color = DepthColor(q.DepthKm);
            int steps = Math.Max(1, (int)(h / 0.32f));
            float fresh = hours < 1 ? 0.5f + 0.5f * Sin(Millis() / 150f) : 0;   // the last hour pulses
            for (int s = 0; s <= steps; s++)
            {
                float r = Radius + s * 0.32f;
                var p = LatLon(q.Lat, q.Lon, r);
                bool top = s == steps;
                char g = top ? (q.Mag >= 5 ? '@' : q.Mag >= 3.5f ? '#' : q.Mag >= 2 ? '*' : '+') : s == 0 ? 'o' : '|';
                Add(p.X, p.Y, p.Z, g, color, top ? 1 : 0.75f + 0.25f * fresh, top ? 0.9f + q.Mag * 0.12f : 0.7f, top ? k : -1);
            }
        }

        // Rings spreading over the surface from new quakes (replay only).
        float tNow = Millis() / 1000f;
        rings.RemoveAll(r => tNow - r.Born > 3);
        foreach (var (q, born) in rings)
        {
            float age = tNow - born, spread = age * (2 + q.Mag) * 1.5f;    // degrees
            int dots = 36;
            for (int k = 0; k < dots; k++)
            {
                float a = k * TWO_PI / dots;
                float lat = q.Lat + Cos(a) * spread, lon = q.Lon + Sin(a) * spread / Max(0.2f, Cos(Radians(q.Lat)));
                var p = LatLon(lat, lon, Radius + 0.05f);
                Add(p.X, p.Y, p.Z, '~', DepthColor(q.DepthKm), 1 - age / 3, 0.6f, -1);
            }
        }
    }

    void Rumble(Quake q)
    {
        // Bigger: lower and louder. Deeper: a darker, softer filter.
        double note = 72 - q.Mag * 7;
        double amp = Constrain(0.03f + q.Mag * q.Mag * 0.006f, 0.03f, 0.35f);
        Play(note, amp: amp, attack: 0.01, release: 0.4 + q.Mag * 0.3, cutoff: 100 - Math.Min(40, q.DepthKm / 15), synth: Synth.Triangle);
        if (q.Mag >= 4.5f) Sample(""kick"", amp: Math.Min(1, q.Mag / 7), rate: 0.5);
    }

    static int DepthColor(float km)
    {
        // 0 km yellow -> 70 km orange -> 300 km red -> 700 km violet.
        float t = Math.Clamp(MathF.Log(1 + Math.Max(0, km)) / MathF.Log(700), 0, 1);
        float r = 255, g = 230 - 200 * t, b = 60 + 120 * Math.Max(0, t - 0.6f) * 2.5f;
        if (t > 0.8f) r = 255 - (t - 0.8f) * 400;
        return Pack(r, g, b);
    }

    void Add(float x, float y, float z, char g, int rgb, float alpha, float scale, int quake)
    {
        if (items >= MaxItems) return;
        wx[items] = x; wy[items] = y; wz[items] = z; wglyph[items] = g; wcol[items] = rgb;
        walpha[items] = alpha; wscale[items] = scale; wquake[items] = quake;
        items++;
    }

    static int Pack(float r, float g, float b) =>
        ((int)Math.Clamp(r, 0, 255) << 16) | ((int)Math.Clamp(g, 0, 255) << 8) | (int)Math.Clamp(b, 0, 255);

    void Render()
    {
        // Turn the world (yaw, pitch) and look at it from a fixed camera on -z.
        float cy = Cos(yaw), sy_ = Sin(yaw), cp = Cos(pitch), sp = Sin(pitch);
        float camDist = 30 / zoom, focal = Height / 2f / Tan(Radians(30));
        int n = 0;
        hovered = -1;
        float best = 14 * 14;
        for (int k = 0; k < items; k++)
        {
            float x = wx[k] * cy - wz[k] * sy_, z = wx[k] * sy_ + wz[k] * cy;
            float y = wy[k] * cp - z * sp; z = wy[k] * sp + z * cp;
            float zc = z + camDist;
            if (zc < 1) continue;
            float size = focal * wscale[k] / zc;
            float px = Width / 2f + focal * x / zc, py = Height / 2f - focal * y / zc;
            sx[k] = px; sy[k] = py; ss[k] = size;
            depthKey[n] = -zc; order[n] = k; n++;
            // The back of the globe hides behind it; only front-facing columns can be hovered.
            if (wquake[k] >= 0 && z < 0)
            {
                float d2 = (px - MouseX) * (px - MouseX) + (py - MouseY) * (py - MouseY);
                if (d2 < best) { best = d2; hovered = wquake[k]; }
            }
        }
        System.Array.Sort(depthKey, order, 0, n);

        TextFont(mono, 32);
        TextAlign(TextAlignH.Center, TextAlignV.Center);
        for (int q = 0; q < n; q++)
        {
            int k = order[q];
            float zc = -depthKey[q];
            // The far side of the globe is dimmer (and its dots smaller): it reads as a sphere.
            float back = Constrain((zc - camDist) / Radius, -1, 1);
            float a = 255 * walpha[k] * (back > 0 ? 1 - 0.75f * back : 1);
            int c = wcol[k];
            if (wquake[k] >= 0 && wquake[k] == hovered) c = Pack(255, 255, 255);
            Fill((c >> 16) & 255, (c >> 8) & 255, c & 255, a);
            float size = ss[k];
            if (size < 3) continue;
            TextSize(size < 24 ? MathF.Round(size) : MathF.Round(size / 6) * 6);
            Text(wglyph[k].ToString(), sx[k], sy[k]);
        }
    }

    void Hud()
    {
        TextFont(mono, 14);
        TextSize(14);
        TextAlign(TextAlignH.Left, TextAlignV.Top);
        if (hovered >= 0 && hovered < quakes.Count)
        {
            var q = quakes[hovered];
            Fill(0, 0, 0, 170);
            Rect(MouseX + 14, MouseY + 10, 330, 62);
            Fill(255);
            Text($""M {q.Mag:0.0} · {q.DepthKm:0} km deep"", MouseX + 22, MouseY + 16);
            Fill(200, 210, 230);
            Text(q.Place.Length > 40 ? q.Place[..40] : q.Place, MouseX + 22, MouseY + 34);
            Text($""{q.Time:ddd dd MMM HH:mm}  ({(DateTime.Now - q.Time).TotalHours:0.0} h ago)"", MouseX + 22, MouseY + 52);
        }

        TextAlign(TextAlignH.Left, TextAlignV.Bottom);
        TextSize(13);
        Fill(demo ? 255 : 255, demo ? 170 : 255, demo ? 120 : 255, 140);
        string replay = replayT >= 0 ? $"" · replay {Min(100, replayT / ReplaySeconds * 100):0}%"" : """";
        Text(status + replay, 16, Height - 30);
        Fill(255, 255, 255, 70);
        Text(""drag: turn · wheel: zoom · hover: details · D/W: day/week · R: replay · Space: spin"", 16, Height - 12);

        // Legend: depth colors.
        TextAlign(TextAlignH.Right, TextAlignV.Center);
        int[] km = { 0, 30, 70, 150, 300, 600 };
        for (int k = 0; k < km.Length; k++)
        {
            int c = DepthColor(km[k]);
            Fill((c >> 16) & 255, (c >> 8) & 255, c & 255);
            Text($""{km[k]} km  #"", Width - 16, 24 + k * 16);
        }
    }

    public override void MouseDragged()
    {
        yaw += (MouseX - PMouseX) * 0.008f;
        pitch = Constrain(pitch + (MouseY - PMouseY) * 0.008f, -1.3f, 1.3f);
    }

    public override void MouseWheel(float delta) => zoom = Constrain(zoom * (delta > 0 ? 1 / 1.1f : 1.1f), 0.6f, 3);

    public override void KeyPressed()
    {
        if (Key == 'w' || Key == 'W') { week = true; status = ""downloading the week...""; lastFetch = -999999; }
        else if (Key == 'd' || Key == 'D') { week = false; status = ""downloading the day...""; lastFetch = -999999; }
        else if (Key == 'r' || Key == 'R') { replayT = 0; rings.Clear(); }
        else if (Key == ' ') spinning = !spinning;
    }
}
";
    }
}
