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
                Loc.Tr("Un globo punteado con los países del mundo (bordes y nombres) y cada terremoto de las últimas 24 horas (o de la semana) saliendo de la superficie: la altura es la magnitud y el color la profundidad. Dos descargas gratuitas y sin clave, en un hilo aparte: el feed en tiempo real del Servicio Geológico de EE.UU. (cada 5 minutos) y los contornos de países de Natural Earth (una vez, guardados en la carpeta temporal). Cada punto del globo es tierra si cae dentro de un país (punto en polígono). Pasá el mouse por un sismo para ver magnitud, profundidad, país, lugar y hora. W muestra la semana (los sismos dibujan solos los bordes de las placas), R repite el periodo en 40 segundos con un anillo y un sonido por sismo, L oculta los nombres. Sin conexión muestra sismos inventados y lo avisa.",
                       "A dotted globe with the world's countries (borders and names) and every earthquake of the last 24 hours (or week) standing out of the surface: height is magnitude, color is depth. Two free, keyless downloads on a background thread: the U.S. Geological Survey's real-time feed (every 5 minutes) and Natural Earth's country outlines (once, kept in the temp folder). Each dot of the globe is land if it falls inside a country (point in polygon). Hover a quake for its magnitude, depth, country, place and time. W shows the week (the quakes trace the plate boundaries by themselves), R replays the period in 40 seconds with a ring and a sound per quake, L hides the names. Offline it shows made-up quakes and says so."),
                QuakeGlobe,
                new[] { SampleTag.Data, SampleTag.ThreeD, SampleTag.Audio, SampleTag.Interaction }),
        };

        private const string QuakeGlobe =
@"// The planet's earthquakes, live, on a dotted globe with the world's countries.
//
// Two downloads, both free and keyless, on a background thread (the globe never waits):
// - the earthquakes: the USGS (U.S. Geological Survey) real-time feed, GeoJSON,
//   refreshed every 5 minutes. https://earthquake.usgs.gov/earthquakes/feed/
// - the countries: Natural Earth's 1:110m country shapes (public domain), also GeoJSON,
//   downloaded once and kept in your temp folder for next time.
//   https://www.naturalearthdata.com
//
// The globe is ~14,000 dots spread evenly over the sphere: the ones that fall inside a
// country (point-in-polygon against its outline) are land, the rest a faint ocean. The
// borders are drawn on top, and the bigger countries are labelled. Each earthquake
// stands out of the surface: its height is the magnitude, its color the depth (shallow
// yellow, deep red and violet -- deep ones are where one plate dives under another).
// Hover one to see its magnitude, depth, place, country and time.
//
// Drag: turn.  Wheel: zoom.  D / W: last day / last week (with the week the quakes
// trace the plate boundaries by themselves).  R: replay the period in 40 s, with a ring
// and a sound per quake.  L: labels on/off.  Space: stop the spin.
// No connection: a plain dotted sphere and made-up quakes, clearly labelled as such.
public class QuakeGlobe : Sketch
{
    // ---- settings ------------------------------------------------------------------------
    const string FeedDay = ""https://earthquake.usgs.gov/earthquakes/feed/v1.0/summary/all_day.geojson"";
    const string FeedWeek = ""https://earthquake.usgs.gov/earthquakes/feed/v1.0/summary/2.5_week.geojson"";
    const string CountriesUrl = ""https://raw.githubusercontent.com/nvkelso/natural-earth-vector/master/geojson/ne_110m_admin_0_countries.geojson"";
    const string NameField = ""NAME_ES"";      // NAME for English names, NAME_FR, NAME_DE, NAME_PT...
    const float R = 10;                      // globe radius
    const float ReplaySeconds = 40;

    record Quake(float Lat, float Lon, float DepthKm, float Mag, string Place, DateTime Time)
    {
        public string Country = """";
    }

    class Country
    {
        public string Name = """";
        public int Rank;
        public float LabelLat, LabelLon;
        public List<float[]> Rings = new();  // lon, lat, lon, lat...; ring 0 of each polygon is its outline
        public List<bool> Outer = new();
        public float MinLon, MaxLon, MinLat, MaxLat;
    }

    // ---- data (built on background threads, swapped in on the main thread) ---------------
    List<Quake> quakes = new();
    volatile List<Quake> arrivedQuakes;
    volatile string arrivedStatus;
    List<Country> countries = new();
    volatile List<Country> arrivedCountries;
    volatile PVector[] arrivedLand, arrivedSea;
    PVector[] land = System.Array.Empty<PVector>(), sea = System.Array.Empty<PVector>();
    string status = ""downloading..."", mapStatus = ""downloading the world map..."";
    volatile string arrivedMapStatus;
    bool week, demo, labels = true;
    long lastFetch = -999999;
    volatile bool fetching;

    // ---- view ----------------------------------------------------------------------------
    PFont font;
    float yaw = 1.45f, pitch = -0.1f, zoom = 1, cy, sy, cp, sp, camDist, focal;   // starts facing the Americas
    bool spinning = true;
    float replayT = -1;
    readonly List<(Quake Q, float Born)> rings = new();
    Quake hovered;

    public override void Setup()
    {
        Size(960, 600);
        font = CreateFont(""Segoe UI"", 14);
        LoadWorld();
    }

    // =====================================================================================
    // Downloads
    // =====================================================================================
    void LoadWorld()
    {
        System.Threading.Tasks.Task.Run(async () =>
        {
            List<Country> list = new();
            try
            {
                string cache = System.IO.Path.Combine(System.IO.Path.GetTempPath(), ""dana_ne_110m_countries.geojson"");
                string json;
                if (File.Exists(cache)) json = File.ReadAllText(cache);
                else
                {
                    using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(30) };
                    json = await http.GetStringAsync(CountriesUrl);
                    File.WriteAllText(cache, json);
                }
                list = ParseCountries(json);
                arrivedMapStatus = $""Natural Earth · {list.Count} countries"";
            }
            catch (Exception e)
            {
                arrivedMapStatus = $""no world map ({e.GetType().Name}): plain globe"";
            }

            // The dots: a Fibonacci spiral spreads them evenly; each one is land if it's inside a country.
            const int dots = 14000;
            var landDots = new List<PVector>();
            var seaDots = new List<PVector>();
            for (int k = 0; k < dots; k++)
            {
                float y = 1 - 2 * (k + 0.5f) / dots, r = MathF.Sqrt(1 - y * y), a = k * 2.39996f;
                var p = new PVector(MathF.Cos(a) * r, y, MathF.Sin(a) * r);
                float lat = MathF.Asin(y) * 180 / MathF.PI, lon = MathF.Atan2(p.X, -p.Z) * 180 / MathF.PI;
                if (CountryAt(list, lat, lon) != null) landDots.Add(p);
                else if (k % 3 == 0) seaDots.Add(p);
            }
            arrivedLand = landDots.ToArray();
            arrivedSea = seaDots.ToArray();
            arrivedCountries = list;
        });
    }

    static List<Country> ParseCountries(string json)
    {
        var list = new List<Country>();
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        foreach (var f in doc.RootElement.GetProperty(""features"").EnumerateArray())
        {
            var props = f.GetProperty(""properties"");
            var c = new Country
            {
                Name = Str(props, NameField) ?? Str(props, ""NAME"") ?? Str(props, ""ADMIN"") ?? ""?"",
                Rank = props.TryGetProperty(""LABELRANK"", out var lr) && lr.ValueKind == System.Text.Json.JsonValueKind.Number ? lr.GetInt32() : 9,
                LabelLon = Num(props, ""LABEL_X""), LabelLat = Num(props, ""LABEL_Y""),
                MinLon = 999, MaxLon = -999, MinLat = 999, MaxLat = -999,
            };
            var g = f.GetProperty(""geometry"");
            var coords = g.GetProperty(""coordinates"");
            if (g.GetProperty(""type"").GetString() == ""Polygon"") AddPolygon(c, coords);
            else foreach (var poly in coords.EnumerateArray()) AddPolygon(c, poly);
            list.Add(c);
        }
        return list;
    }

    static string Str(System.Text.Json.JsonElement e, string key) =>
        e.TryGetProperty(key, out var v) && v.ValueKind == System.Text.Json.JsonValueKind.String ? v.GetString() : null;
    static float Num(System.Text.Json.JsonElement e, string key) =>
        e.TryGetProperty(key, out var v) && v.ValueKind == System.Text.Json.JsonValueKind.Number ? (float)v.GetDouble() : 0;

    static void AddPolygon(Country c, System.Text.Json.JsonElement poly)
    {
        bool first = true;
        foreach (var ring in poly.EnumerateArray())
        {
            var pts = new List<float>();
            foreach (var pt in ring.EnumerateArray())
            {
                float lon = (float)pt[0].GetDouble(), lat = (float)pt[1].GetDouble();
                pts.Add(lon); pts.Add(lat);
                if (first)
                {
                    c.MinLon = Math.Min(c.MinLon, lon); c.MaxLon = Math.Max(c.MaxLon, lon);
                    c.MinLat = Math.Min(c.MinLat, lat); c.MaxLat = Math.Max(c.MaxLat, lat);
                }
            }
            c.Rings.Add(pts.ToArray());
            c.Outer.Add(first);
            first = false;
        }
    }

    /// <summary>Which country contains this point (even-odd ray casting against the outlines; holes count too).</summary>
    static Country CountryAt(List<Country> list, float lat, float lon)
    {
        foreach (var c in list)
        {
            if (lon < c.MinLon || lon > c.MaxLon || lat < c.MinLat || lat > c.MaxLat) continue;
            bool inside = false;
            foreach (var ring in c.Rings)
                for (int i = 0, j = ring.Length - 2; i < ring.Length; j = i, i += 2)
                {
                    float xi = ring[i], yi = ring[i + 1], xj = ring[j], yj = ring[j + 1];
                    if ((yi > lat) != (yj > lat) && lon < (xj - xi) * (lat - yi) / (yj - yi) + xi)
                        inside = !inside;
                }
            if (inside) return c;
        }
        return null;
    }

    void FetchQuakes()
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
                string json = await http.GetStringAsync(url);
                var list = ParseQuakes(json);
                arrivedStatus = $""USGS · {list.Count} earthquakes · {(week ? ""magnitude 2.5+, last 7 days"" : ""all, last 24 hours"")} · updated {DateTime.Now:HH:mm}"";
                demo = false;
                arrivedQuakes = list;
            }
            catch (Exception e)
            {
                if (quakes.Count == 0 || demo)
                {
                    arrivedStatus = $""no connection ({e.GetType().Name}) · these quakes are MADE UP"";
                    demo = true;
                    arrivedQuakes = DemoQuakes();
                }
                else arrivedStatus = status + "" · last update failed"";
            }
            finally { fetching = false; }
        });
    }

    /// <summary>USGS GeoJSON: features[] -> properties { mag, place, time (ms) } + geometry.coordinates [lon, lat, depth km].</summary>
    static List<Quake> ParseQuakes(string json)
    {
        var list = new List<Quake>();
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        foreach (var f in doc.RootElement.GetProperty(""features"").EnumerateArray())
        {
            var p = f.GetProperty(""properties"");
            var c = f.GetProperty(""geometry"").GetProperty(""coordinates"");
            if (p.GetProperty(""mag"").ValueKind != System.Text.Json.JsonValueKind.Number) continue;
            var time = DateTimeOffset.FromUnixTimeMilliseconds(p.GetProperty(""time"").GetInt64()).LocalDateTime;
            list.Add(new Quake((float)c[1].GetDouble(), (float)c[0].GetDouble(), (float)c[2].GetDouble(),
                               (float)p.GetProperty(""mag"").GetDouble(), Str(p, ""place"") ?? ""?"", time));
        }
        list.Sort((a, b) => a.Time.CompareTo(b.Time));
        return list;
    }

    List<Quake> DemoQuakes()
    {
        (float Lat, float Lon)[] spots = { (38, 142), (-33, -72), (61, -150), (35, -117), (-6, 130), (37, 70), (13, -90), (-18, -178), (40, 29), (-4, 101), (52, -176), (19, -155) };
        var list = new List<Quake>();
        var start = DateTime.Now.AddHours(-24);
        for (int k = 0; k < 200; k++)
        {
            var s = spots[(int)Random(spots.Length)];
            list.Add(new Quake(s.Lat + Random(-3, 3), s.Lon + Random(-3, 3), Pow(Random(1), 2) * 600,
                               1 + Pow(Random(1), 3) * 5.5f, ""made-up quake (offline demo)"", start.AddMinutes(Random(24 * 60))));
        }
        list.Sort((a, b) => a.Time.CompareTo(b.Time));
        return list;
    }

    // =====================================================================================
    // Each frame
    // =====================================================================================
    public override void Draw()
    {
        if (arrivedCountries != null) { countries = arrivedCountries; land = arrivedLand; sea = arrivedSea; mapStatus = arrivedMapStatus; arrivedCountries = null; }
        if (arrivedQuakes != null)
        {
            var list = arrivedQuakes;
            foreach (var q in list) q.Country = CountryAt(countries, q.Lat, q.Lon)?.Name ?? """";
            quakes = list; status = arrivedStatus; arrivedQuakes = null;
        }
        if (Millis() - lastFetch > 5 * 60 * 1000) FetchQuakes();
        if (spinning) yaw += 0.0015f;

        // The camera: the world turns (yaw, pitch), we look at it from -z.
        cy = Cos(yaw); sy = Sin(yaw); cp = Cos(pitch); sp = Sin(pitch);
        camDist = 25 / zoom;
        focal = Height / 2f / Tan(Radians(30));

        Background(5, 8, 18);
        // An atmosphere: a soft glow just outside the globe's edge.
        var (ex, ey, _) = Project(0, 0, 0);
        float screenR = focal * R / MathF.Sqrt(camDist * camDist - R * R);
        NoStroke();
        for (int k = 8; k >= 1; k--)
        {
            Fill(40, 110, 220, 7);
            Circle(ex, ey, 2 * screenR + k * 9);
        }
        Fill(6, 12, 28);
        Circle(ex, ey, 2 * screenR);

        DrawDots(sea, 0.9f, 30, 55, 95);
        DrawDots(land, 2.1f, 120, 200, 230);
        DrawBorders();
        var clock = Replay();
        DrawQuakes(clock);
        if (labels) DrawLabels();
        Hud();
    }

    /// <summary>World point -> screen (x, y) and how far it is in front of the globe's middle (>0: facing us).</summary>
    (float X, float Y, float Facing) Project(float x, float y, float z)
    {
        float x1 = x * cy - z * sy, z1 = x * sy + z * cy;
        float y2 = y * cp - z1 * sp, z2 = y * sp + z1 * cp;
        float zc = z2 + camDist;
        return (Width / 2f + focal * x1 / zc, Height / 2f - focal * y2 / zc, -z2);
    }

    static PVector LatLon(float lat, float lon, float r)
    {
        float la = lat * MathF.PI / 180, lo = lon * MathF.PI / 180;
        return new PVector(MathF.Cos(la) * MathF.Sin(lo) * r, MathF.Sin(la) * r, -MathF.Cos(la) * MathF.Cos(lo) * r);
    }

    void DrawDots(PVector[] dots, float size, float r, float g, float b)
    {
        NoStroke();
        foreach (var d in dots)
        {
            var (x, y, facing) = Project(d.X * R, d.Y * R, d.Z * R);
            if (facing < 0) continue;                                   // the far side is hidden
            float edge = facing / R;                                    // 1 in the middle, 0 at the rim
            Fill(r, g, b, 60 + 195 * edge);
            Circle(x, y, size * (0.55f + 0.45f * edge) * zoom);
        }
    }

    void DrawBorders()
    {
        NoFill();
        StrokeWeight(0.8f);
        Stroke(120, 200, 230, 110);
        foreach (var c in countries)
            foreach (var ring in c.Rings)
            {
                float px = 0, py = 0; bool prev = false;
                for (int i = 0; i < ring.Length; i += 2)
                {
                    var p = LatLon(ring[i + 1], ring[i], R * 1.002f);
                    var (x, y, facing) = Project(p.X, p.Y, p.Z);
                    bool vis = facing > 0;
                    if (vis && prev) Line(px, py, x, y);
                    px = x; py = y; prev = vis;
                }
            }
    }

    DateTime Replay()
    {
        DateTime now = DateTime.Now;
        if (replayT < 0 || quakes.Count == 0) return now;
        DateTime first = quakes[0].Time, last = quakes[^1].Time;
        DateTime At(float t) => first + TimeSpan.FromTicks((long)((last - first).Ticks * Math.Min(1, t / ReplaySeconds)));
        DateTime before = At(replayT);
        replayT += 1 / 60f;
        DateTime clock = At(replayT);
        foreach (var q in quakes)
            if (q.Time > before && q.Time <= clock)
            {
                rings.Add((q, Millis() / 1000f));
                Rumble(q);
            }
        if (replayT > ReplaySeconds + 3) replayT = -1;
        return clock;
    }

    void Rumble(Quake q)
    {
        // Bigger: lower and louder. Deeper: darker.
        Play(72 - q.Mag * 7, amp: Constrain(0.03f + q.Mag * q.Mag * 0.006f, 0.03f, 0.35f), attack: 0.01,
             release: 0.4 + q.Mag * 0.3, cutoff: 100 - Math.Min(40, q.DepthKm / 15), synth: Synth.Triangle);
        if (q.Mag >= 4.5f) Sample(""kick"", amp: Math.Min(1, q.Mag / 7), rate: 0.5);
    }

    void DrawQuakes(DateTime clock)
    {
        // Rings first, under the columns.
        float tNow = Millis() / 1000f;
        rings.RemoveAll(r => tNow - r.Born > 3);
        NoFill();
        foreach (var (q, born) in rings)
        {
            float age = tNow - born, spread = age * (2 + q.Mag) * 1.4f;
            var (cr, cg, cb) = DepthColor(q.DepthKm);
            Stroke(cr, cg, cb, 255 * (1 - age / 3));
            StrokeWeight(1.5f);
            float lastX = 0, lastY = 0; bool prev = false;
            for (int k = 0; k <= 48; k++)
            {
                float a = k * TWO_PI / 48;
                var p = LatLon(q.Lat + Cos(a) * spread, q.Lon + Sin(a) * spread / Max(0.2f, Cos(Radians(q.Lat))), R * 1.003f);
                var (x, y, f) = Project(p.X, p.Y, p.Z);
                if (f > 0 && prev) Line(lastX, lastY, x, y);
                lastX = x; lastY = y; prev = f > 0;
            }
        }

        // Columns, far ones first so the near ones cover them.
        var visible = new List<(Quake Q, float X0, float Y0, float X1, float Y1, float F)>();
        foreach (var q in quakes)
        {
            if (q.Time > clock) continue;
            float h = 0.2f + Pow(Max(0, q.Mag), 1.6f) * 0.25f;
            var a = LatLon(q.Lat, q.Lon, R);
            var b = LatLon(q.Lat, q.Lon, R + h);
            var (x0, y0, f0) = Project(a.X, a.Y, a.Z);
            var (x1, y1, _) = Project(b.X, b.Y, b.Z);
            if (f0 < -1) continue;                                      // behind the globe
            visible.Add((q, x0, y0, x1, y1, f0));
        }
        visible.Sort((u, v) => u.F.CompareTo(v.F));

        hovered = null;
        float best = 12 * 12;
        foreach (var v in visible)
        {
            float d2 = (v.X1 - MouseX) * (v.X1 - MouseX) + (v.Y1 - MouseY) * (v.Y1 - MouseY);
            if (v.F > 0 && d2 < best) { best = d2; hovered = v.Q; }
        }
        foreach (var v in visible)
        {
            var q = v.Q;
            var (r, g, b) = DepthColor(q.DepthKm);
            bool hot = q == hovered;
            float hours = (float)(DateTime.Now - q.Time).TotalHours;
            float pulse = hours < 1 ? 0.5f + 0.5f * Sin(Millis() / 160f) : 0;
            float alpha = v.F > 0 ? 255 : 90;
            StrokeWeight(1.2f + q.Mag * 0.25f);
            Stroke(r, g, b, alpha);
            Line(v.X0, v.Y0, v.X1, v.Y1);
            NoStroke();
            float size = (2.5f + q.Mag * 2.2f) * zoom * 0.8f;
            Fill(r, g, b, alpha * 0.25f);
            Circle(v.X1, v.Y1, size * (2 + pulse));                     // glow
            Fill(hot ? 255 : r, hot ? 255 : g, hot ? 255 : b, alpha);
            Circle(v.X1, v.Y1, size);
        }
    }

    static (float, float, float) DepthColor(float km)
    {
        // 0 km yellow -> 70 km orange -> 300 km red -> 700 km violet.
        float t = Math.Clamp(MathF.Log(1 + Math.Max(0, km)) / MathF.Log(700), 0, 1);
        float r = t > 0.8f ? 255 - (t - 0.8f) * 400 : 255;
        return (r, 230 - 200 * t, 60 + 300 * Math.Max(0, t - 0.6f));
    }

    void DrawLabels()
    {
        TextFont(font, 11);
        TextSize(11);
        TextAlign(TextAlignH.Center, TextAlignV.Center);
        foreach (var c in countries)
        {
            if (c.Rank > 3 + (int)(zoom * 1.5f)) continue;              // closer: more names
            var p = LatLon(c.LabelLat, c.LabelLon, R * 1.01f);
            var (x, y, facing) = Project(p.X, p.Y, p.Z);
            if (facing < R * 0.35f) continue;
            float a = Constrain((facing / R - 0.35f) * 3, 0, 1);
            Fill(220, 235, 255, 150 * a);
            Text(c.Name, x, y);
        }
    }

    void Hud()
    {
        TextFont(font, 14);
        TextSize(14);
        if (hovered != null)
        {
            var q = hovered;
            string where = q.Country != """" ? q.Country : ""ocean / no country"";
            float w = 360, x = Math.Min(MouseX + 16, Width - w - 8), y = Math.Min(MouseY + 12, Height - 96);
            NoStroke();
            Fill(0, 0, 0, 190);
            Rect(x, y, w, 84);
            TextAlign(TextAlignH.Left, TextAlignV.Top);
            Fill(255);
            Text($""M {q.Mag:0.0}  ·  {q.DepthKm:0} km deep  ·  {where}"", x + 10, y + 8);
            Fill(200, 210, 230);
            Text(q.Place.Length > 48 ? q.Place[..48] + ""…"" : q.Place, x + 10, y + 32);
            Text($""{q.Time:ddd dd MMM HH:mm}  ({(DateTime.Now - q.Time).TotalHours:0.0} h ago)"", x + 10, y + 56);
        }

        TextAlign(TextAlignH.Left, TextAlignV.Bottom);
        TextSize(13);
        Fill(demo ? 255 : 230, demo ? 170 : 235, demo ? 110 : 255, 170);
        string replay = replayT >= 0 ? $""  ·  replay {Math.Min(100, replayT / ReplaySeconds * 100):0}%"" : """";
        Text($""{status}{replay}  ·  {mapStatus}"", 16, Height - 30);
        Fill(255, 255, 255, 80);
        Text(""drag: turn · wheel: zoom · hover: details · D/W: day/week · R: replay · L: labels · Space: spin"", 16, Height - 12);

        // Legend: the depth colors.
        TextAlign(TextAlignH.Right, TextAlignV.Center);
        Fill(255, 255, 255, 120);
        Text(""depth"", Width - 16, 20);
        int[] km = { 0, 30, 70, 150, 300, 600 };
        for (int k = 0; k < km.Length; k++)
        {
            var (r, g, b) = DepthColor(km[k]);
            Fill(r, g, b);
            Circle(Width - 22, 40 + k * 18, 9);
            Fill(255, 255, 255, 150);
            Text($""{km[k]} km"", Width - 34, 40 + k * 18);
        }
    }

    public override void MouseDragged()
    {
        yaw += (MouseX - PMouseX) * 0.008f;
        pitch = Constrain(pitch + (MouseY - PMouseY) * 0.008f, -1.3f, 1.3f);
    }

    public override void MouseWheel(float delta) => zoom = Constrain(zoom * (delta > 0 ? 1 / 1.1f : 1.1f), 0.7f, 2.6f);

    public override void KeyPressed()
    {
        if (Key == 'w' || Key == 'W') { week = true; status = ""downloading the week...""; lastFetch = -999999; }
        else if (Key == 'd' || Key == 'D') { week = false; status = ""downloading the day...""; lastFetch = -999999; }
        else if (Key == 'r' || Key == 'R') { replayT = 0; rings.Clear(); }
        else if (Key == 'l' || Key == 'L') labels = !labels;
        else if (Key == ' ') spinning = !spinning;
    }
}
";
    }
}
