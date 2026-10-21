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
                Loc.Tr("Un globo punteado con relieve (las montañas se levantan y tienen sombra) y los países del mundo, con cada terremoto de las últimas 24 horas (o de la semana) saliendo del suelo: la altura es la magnitud y el color la profundidad. Muestra lo nuevo de Dana para datos de internet: tres descargas en segundo plano de una línea cada una (RequestGeoJSON, RequestPImage) con caché en disco y un panel de carga hecho con su Status y Progress; los países con GeoMap.FeatureAt; el relieve leído con img.Pixels; los 36.000 puntos dibujados de una vez con Points(cloud, view); y el área donde se sintió un sismo con Geo.Circle. Pasá el mouse por un sismo para ver magnitud, profundidad, país, lugar y hora, y las ondas hasta donde llegó. W muestra la semana, R repite el periodo en 40 segundos con sonido, E cambia el relieve y L oculta los nombres.",
                       "A dotted globe with relief (mountains rise and are hill-shaded) and the world's countries, with every earthquake of the last 24 hours (or week) standing out of the ground: height is magnitude, color is depth. It shows Dana's new tools for web data: three background downloads of one line each (RequestGeoJSON, RequestPImage) cached on disk, with a loading panel built from their Status and Progress; countries with GeoMap.FeatureAt; the relief read through img.Pixels; the 36,000 points drawn in one go with Points(cloud, view); and a quake's felt area with Geo.Circle. Hover a quake for its magnitude, depth, country, place and time, and the waves out to where it was felt. W shows the week, R replays the period in 40 seconds with sound, E switches the relief and L hides the names."),
                QuakeGlobe,
                new[] { SampleTag.Data, SampleTag.ThreeD, SampleTag.Audio, SampleTag.Interaction }),
        };

        private const string QuakeGlobe =
@"// The planet's earthquakes, live, on a dotted globe with relief and the world's countries.
//
// Three free, keyless downloads -- each one a single line, loading in the background
// while the sketch keeps drawing (RequestGeoJSON / RequestPImage), cached on disk so the
// next run starts at once:
// - the earthquakes: the USGS (U.S. Geological Survey) real-time feed, GeoJSON, every
//   5 minutes. https://earthquake.usgs.gov/earthquakes/feed/
// - the countries: Natural Earth's 1:110m outlines (public domain), GeoJSON.
// - the relief: a 2048 x 1024 grayscale elevation map of the Earth (three-globe's, from
//   NASA data), read pixel by pixel with img.LoadPixels() / img.Pixels.
//
// The globe is ~36,000 points from Geo.FibonacciSphere(): the ones inside a country
// (countries.FeatureAt(lat, lon)) are land and rise with the terrain -- flat below a
// threshold, amplified above it, so mountain ranges stand out -- the rest a faint sea.
// They're all drawn in one batch with Points(cloud, view), hiding the far side of the
// ball. Each earthquake stands out of the ground: height is magnitude, color is depth.
// Hover one: magnitude, depth, country, place, time, and the area where it was felt
// (Geo.Circle) with waves running out to it.
//
// Drag: turn.  Wheel: zoom.  D / W: last day / last week (with the week the quakes trace
// the plate boundaries by themselves).  R: replay the period in 40 s, a ring and a sound
// per quake.  E: relief flat / normal / dramatic.  L: labels.  Space: stop the spin.
public class QuakeGlobe : Sketch
{
    // ---- settings ------------------------------------------------------------------------
    const string FeedDay = ""https://earthquake.usgs.gov/earthquakes/feed/v1.0/summary/all_day.geojson"";
    const string FeedWeek = ""https://earthquake.usgs.gov/earthquakes/feed/v1.0/summary/2.5_week.geojson"";
    const string CountriesUrl = ""https://raw.githubusercontent.com/nvkelso/natural-earth-vector/master/geojson/ne_110m_admin_0_countries.geojson"";
    const string ReliefUrl = ""https://raw.githubusercontent.com/vasturiano/three-globe/master/example/img/earth-topology.png"";
    const string NameField = ""NAME_ES"";      // NAME for English names, NAME_FR, NAME_DE, NAME_PT...
    const float R = 10;                      // globe radius
    const float MountainThreshold = 0.14f;   // below this (of the elevation range) the land stays flat
    const float ReplaySeconds = 40;
    static readonly TimeSpan Forever = TimeSpan.FromDays(365);

    record Quake(float Lat, float Lon, float DepthKm, float Mag, string Place, DateTime Time)
    {
        public string Country = """";
    }

    // ---- data ----------------------------------------------------------------------------
    Request<GeoMap> countriesReq, quakesReq, quakesRead;
    Request<PImage> reliefReq;
    GeoMap countries;
    Color[] relief;
    int reliefW, reliefH;
    List<Quake> quakes = new();
    bool week, labels = true;
    long lastFetch;

    // The globe's points: built a few thousand per frame once the countries are in (a bar shows it).
    readonly PVector[] sphere = Geo.FibonacciSphere(36000);
    readonly List<int> land = new(), sea = new();
    float[] height = System.Array.Empty<float>(), shade = System.Array.Empty<float>();
    int classified;
    bool built, heightsDone;
    float shown;                             // fades the world in once it's built

    // ---- view ----------------------------------------------------------------------------
    PFont font;
    readonly PointCloud cloud = new();
    View3D view;
    float yaw = -1.4f, pitch = 0.15f, zoom = 1, reliefScale = 1;   // starts over the Americas
    bool spinning = true;
    float replayT = -1;
    readonly List<(Quake Q, float Born)> rings = new();
    Quake hovered;

    public override void Setup()
    {
        Size(960, 600);
        font = CreateFont(""Segoe UI"", 14);
        countriesReq = RequestGeoJSON(CountriesUrl, cacheFor: Forever);
        reliefReq = RequestPImage(ReliefUrl, cacheFor: Forever);
        FetchQuakes();
        cloud.OccluderCenter = new PVector(0, 0, 0);
        cloud.OccluderRadius = R * 0.995f;
    }

    void FetchQuakes()
    {
        quakesReq = RequestGeoJSON(week ? FeedWeek : FeedDay, cacheFor: TimeSpan.FromMinutes(4));
        lastFetch = Millis();
    }

    // =====================================================================================
    // Each frame
    // =====================================================================================
    public override void Draw()
    {
        TakeArrivals();
        if (Millis() - lastFetch > 5 * 60 * 1000) FetchQuakes();
        if (spinning) yaw -= 0.0015f;
        if (built && (heightsDone || reliefReq.Failed)) shown = Min(1, shown + 0.02f);

        view = View3D.Orbit(yaw, pitch, 25 / zoom);
        Background(5, 8, 18);
        Atmosphere();
        if (shown > 0)
        {
            DrawGlobe();
            DrawBorders();
            var clock = Replay();
            DrawQuakes(clock);
            if (labels) DrawLabels();
        }
        Hud();
        if (shown < 1) Loading();
    }

    void TakeArrivals()
    {
        if (countries == null && countriesReq.Ready) countries = countriesReq.Value;
        if (relief == null && reliefReq.Ready)
        {
            var img = reliefReq.Value;
            img.LoadPixels();                                   // the PNG's pixels, as Colors
            relief = img.Pixels; reliefW = img.Width; reliefH = img.Height;
        }

        // Land or sea: 3,000 points a frame, so the bar moves and the window never freezes.
        if (!built && (countries != null || countriesReq.Failed))
        {
            int end = Math.Min(sphere.Length, classified + 3000);
            for (; classified < end; classified++)
            {
                var (lat, lon) = Geo.FromSphere(sphere[classified]);
                if (countries?.FeatureAt(lat, lon) != null) land.Add(classified);
                else if ((uint)(classified * 2654435761u) % 6 == 0) sea.Add(classified);   // 1 in 6, scattered (every 6th would line up in spirals)
            }
            if (classified == sphere.Length) built = true;
        }

        // Heights and hill shading, once both the land and the relief are in.
        if (built && relief != null && !heightsDone)
        {
            height = new float[land.Count];
            shade = new float[land.Count];
            for (int k = 0; k < land.Count; k++)
            {
                var (lat, lon) = Geo.FromSphere(sphere[land[k]]);
                height[k] = HeightAt(lat, lon);
                // Light from the north-west: slopes facing it brighter, the others darker.
                float slope = (HeightAt(lat + 0.6f, lon) - HeightAt(lat - 0.6f, lon)) + (HeightAt(lat, lon - 0.6f) - HeightAt(lat, lon + 0.6f));
                shade[k] = Math.Clamp(1 + slope * 4, 0.45f, 1.6f);
            }
            heightsDone = true;
        }

        if (quakesReq.Ready && quakesRead != quakesReq)
        {
            quakes = ReadQuakes(quakesReq.Value);
            quakesRead = quakesReq;
        }
        if (countries != null)
            foreach (var q in quakes)
                if (q.Country == """") q.Country = countries.FeatureAt(q.Lat, q.Lon)?.Text(NameField) ?? ""-"";
    }

    /// <summary>USGS GeoJSON: one Point per quake (its third coordinate is the depth, km) with mag, place and time (ms).</summary>
    static List<Quake> ReadQuakes(GeoMap map)
    {
        var list = new List<Quake>();
        foreach (var f in map.Features)
        {
            double mag = f.Number(""mag"");
            if (double.IsNaN(mag) || f.Points.Count == 0) continue;
            var time = DateTimeOffset.FromUnixTimeMilliseconds((long)f.Number(""time"", 0)).LocalDateTime;
            list.Add(new Quake(f.Lat, f.Lon, f.Alt, (float)mag, f.Text(""place"") ?? ""?"", time));
        }
        list.Sort((a, b) => a.Time.CompareTo(b.Time));
        return list;
    }

    /// <summary>Elevation 0..1 (the map is equirectangular: Geo.Equirectangular() gives the pixel).</summary>
    float HeightAt(float lat, float lon)
    {
        if (relief == null) return 0;
        var (x, y) = Geo.Equirectangular(lat, lon, reliefW, reliefH);
        int px = Math.Clamp((int)x, 0, reliefW - 1), py = Math.Clamp((int)y, 0, reliefH - 1);
        return relief[py * reliefW + px].R / 248f;
    }

    /// <summary>Height above the sphere: almost flat below the threshold, amplified above it.</summary>
    float Lift(float h) => R * reliefScale * (0.012f * h + 0.16f * MathF.Pow(Math.Max(0, h - MountainThreshold) / (1 - MountainThreshold), 1.3f));

    PVector Ground(float lat, float lon, float extra = 0) => Geo.ToSphere(lat, lon, R + Lift(HeightAt(lat, lon)) + extra);

    /// <summary>Screen position of a point, and whether it's on the side of the globe facing us.</summary>
    (float X, float Y, bool Front) See(PVector p)
    {
        var (x, y, depth) = view.Project(p, Width, Height);
        var e = view.Eye;
        bool front = depth > 0 && p.X * (e.X - p.X) + p.Y * (e.Y - p.Y) + p.Z * (e.Z - p.Z) > 0;
        return (x, y, front);
    }

    // =====================================================================================
    // Drawing
    // =====================================================================================
    void Atmosphere()
    {
        var (cx, cy, _) = view.Project(new PVector(0, 0, 0), Width, Height);
        float d = 25 / zoom;
        float screenR = view.Scale(d, Height) * R * d / MathF.Sqrt(d * d - R * R);
        NoStroke();
        for (int k = 8; k >= 1; k--)
        {
            Fill(40, 110, 220, 7);
            Circle(cx, cy, 2 * screenR + k * 9);
        }
        Fill(6, 12, 28);
        Circle(cx, cy, 2 * screenR);
    }

    void DrawGlobe()
    {
        cloud.Clear();
        byte a = (byte)(255 * shown);
        foreach (int i in sea)
            cloud.Add(PVector.Mult(sphere[i], R), Color(30, 60, 105, 150 * shown), 0.07f);
        bool lifted = heightsDone && reliefScale > 0;
        for (int k = 0; k < land.Count; k++)
        {
            float h = heightsDone ? height[k] : 0;
            var p = PVector.Mult(sphere[land[k]], R + (lifted ? Lift(h) : 0));
            // Terrain colors: green lowlands, tan uplands, white peaks, hill-shaded.
            float cr, cg, cb;
            if (h < 0.18f) { float t = h / 0.18f; cr = Lerp(70, 160, t); cg = Lerp(190, 185, t); cb = Lerp(130, 105, t); }
            else if (h < 0.55f) { float t = (h - 0.18f) / 0.37f; cr = Lerp(150, 175, t); cg = Lerp(175, 140, t); cb = Lerp(100, 105, t); }
            else { float t = Math.Min(1, (h - 0.55f) / 0.35f); cr = Lerp(175, 245, t); cg = Lerp(140, 245, t); cb = Lerp(105, 250, t); }
            float l = heightsDone ? shade[k] : 1;
            var c = Color(Math.Min(255, cr * l), Math.Min(255, cg * l), Math.Min(255, cb * l), a);
            cloud.Add(p, c, 0.12f + h * 0.08f);
            // Mountains: a stalk from the ground up, so a range reads as volume.
            if (lifted && h > MountainThreshold)
                for (float s = 0.3f; s < 1; s += 0.35f)
                    cloud.Add(PVector.Mult(sphere[land[k]], R + Lift(h) * s), Color(cr * l * 0.8f, cg * l * 0.8f, cb * l * 0.8f, a * 0.7f), 0.07f);
        }
        Points(cloud, view);                                     // all of it, one batch
    }

    void DrawBorders()
    {
        if (countries == null) return;
        NoFill();
        StrokeWeight(0.8f);
        Stroke(120, 200, 230, 110 * shown);
        foreach (var f in countries.Features)
            foreach (var ring in f.Rings)
            {
                float px = 0, py = 0; bool prev = false;
                for (int i = 0; i < ring.Count; i++)
                {
                    var (x, y, front) = See(Ground(ring.Lat[i], ring.Lon[i], 0.02f));
                    if (front && prev) Line(px, py, x, y);
                    px = x; py = y; prev = front;
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
                // Bigger: lower and louder. Deeper: darker.
                Play(72 - q.Mag * 7, amp: Constrain(0.03f + q.Mag * q.Mag * 0.006f, 0.03f, 0.35f), attack: 0.01,
                     release: 0.4 + q.Mag * 0.3, cutoff: 100 - Math.Min(40, q.DepthKm / 15), synth: Synth.Triangle);
                if (q.Mag >= 4.5f) Sample(""kick"", amp: Math.Min(1, q.Mag / 7), rate: 0.5);
            }
        if (replayT > ReplaySeconds + 3) replayT = -1;
        return clock;
    }

    /// <summary>A ring on the ground, <paramref name=""km""/> from (lat, lon) -- Geo.Circle does the sphere math.</summary>
    void GroundRing(float lat, float lon, float km, bool filled)
    {
        var pts = Geo.Circle(lat, lon, Math.Max(km, 1), 64);
        if (filled)
        {
            BeginShape();
            foreach (var (la, lo) in pts) { var s = See(Ground(la, lo, 0.03f)); if (s.Front) Vertex(s.X, s.Y); }
            EndShape(true);
            return;
        }
        float px = 0, py = 0; bool prev = false;
        foreach (var (la, lo) in pts)
        {
            var (x, y, front) = See(Ground(la, lo, 0.03f));
            if (front && prev) Line(px, py, x, y);
            px = x; py = y; prev = front;
        }
    }

    /// <summary>Roughly how far from the epicentre people feel it, km: ~10 at M2, ~160 at M5, ~1,200 at M7.</summary>
    static float FeltKm(float mag) => MathF.Pow(10, 0.43f * mag + 0.06f);

    void DrawQuakes(DateTime clock)
    {
        float tNow = Millis() / 1000f;
        rings.RemoveAll(r => tNow - r.Born > 3);
        NoFill();
        foreach (var (q, born) in rings)
        {
            float age = tNow - born;
            var (r, g, b) = DepthColor(q.DepthKm);
            Stroke(r, g, b, 255 * (1 - age / 3));
            StrokeWeight(1.5f);
            GroundRing(q.Lat, q.Lon, FeltKm(q.Mag) * age, false);
        }

        var visible = new List<(Quake Q, float X0, float Y0, float X1, float Y1, bool Front)>();
        foreach (var q in quakes)
        {
            if (q.Time > clock) continue;
            float h = 0.2f + Pow(Max(0, q.Mag), 1.6f) * 0.25f;
            var (x0, y0, front) = See(Ground(q.Lat, q.Lon));
            var (x1, y1, _) = See(Ground(q.Lat, q.Lon, h));
            if (float.IsNaN(x0)) continue;
            visible.Add((q, x0, y0, x1, y1, front));
        }
        visible.Sort((u, v) => u.Front.CompareTo(v.Front));          // the far side first

        hovered = null;
        float best = 12 * 12;
        foreach (var v in visible)
        {
            float d2 = (v.X1 - MouseX) * (v.X1 - MouseX) + (v.Y1 - MouseY) * (v.Y1 - MouseY);
            if (v.Front && d2 < best) { best = d2; hovered = v.Q; }
        }
        if (hovered != null) FeltArea(hovered);

        foreach (var v in visible)
        {
            var q = v.Q;
            var (r, g, b) = DepthColor(q.DepthKm);
            bool hot = q == hovered, dim = hovered != null && !hot;
            float pulse = (DateTime.Now - q.Time).TotalHours < 1 ? 0.5f + 0.5f * Sin(Millis() / 160f) : 0;
            float alpha = (v.Front ? 255 : 90) * (dim ? 0.3f : 1) * shown;
            StrokeWeight(1.2f + q.Mag * 0.25f);
            Stroke(r, g, b, alpha);
            Line(v.X0, v.Y0, v.X1, v.Y1);
            NoStroke();
            float size = (2.5f + q.Mag * 2.2f) * zoom * 0.8f;
            Fill(r, g, b, alpha * 0.25f);
            Circle(v.X1, v.Y1, size * (2 + pulse));
            Fill(hot ? 255 : r, hot ? 255 : g, hot ? 255 : b, alpha);
            Circle(v.X1, v.Y1, size);
        }
    }

    /// <summary>The hovered quake's reach: the felt area, and four waves running out to its edge.</summary>
    void FeltArea(Quake q)
    {
        var (r, g, b) = DepthColor(q.DepthKm);
        float km = FeltKm(q.Mag), t = Millis() / 1000f;
        NoStroke();
        Fill(r, g, b, 70);
        GroundRing(q.Lat, q.Lon, km, true);
        NoFill();
        Stroke(r, g, b, 170);
        StrokeWeight(1.2f);
        GroundRing(q.Lat, q.Lon, km, false);
        for (int w = 0; w < 4; w++)
        {
            float frac = (t * 0.5f + w / 4f) % 1;
            Stroke(r, g, b, 230 * (1 - frac));
            StrokeWeight(2.2f * (1 - frac) + 0.6f);
            GroundRing(q.Lat, q.Lon, km * frac, false);
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
        if (countries == null) return;
        TextFont(font, 12);
        TextAlign(TextAlignH.Center, TextAlignV.Center);
        foreach (var f in countries.Features)
        {
            if (f.Number(""LABELRANK"", 9) > 2 + (int)((zoom - 1) * 3)) continue;   // closer: more names
            var p = Ground((float)f.Number(""LABEL_Y"", 0), (float)f.Number(""LABEL_X"", 0), 0.1f);
            var (x, y, front) = See(p);
            if (!front) continue;
            float facing = PVector.Dot(PVector.Sub(view.Eye, p).Normalize(), PVector.Mult(p, 1f / p.Mag()));
            Fill(235, 245, 255, 230 * Constrain((facing - 0.3f) * 3, 0, 1) * shown);
            Text(f.Text(NameField) ?? f.Name, x, y);
        }
    }

    void Hud()
    {
        TextFont(font, 14);
        if (hovered != null)
        {
            var q = hovered;
            float w = 360, x = Math.Min(MouseX + 16, Width - w - 8), y = Math.Min(MouseY + 12, Height - 120);
            NoStroke();
            Fill(0, 0, 0, 190);
            Rect(x, y, w, 108, 8);                                // rounded, like Processing's rect(x, y, w, h, r)
            TextAlign(TextAlignH.Left, TextAlignV.Top);
            Fill(255);
            Text($""M {q.Mag:0.0}  ·  {q.DepthKm:0} km deep  ·  {(q.Country is """" or ""-"" ? ""ocean"" : q.Country)}"", x + 12, y + 8);
            Fill(200, 210, 230);
            Text(q.Place.Length > 48 ? q.Place[..48] + ""…"" : q.Place, x + 12, y + 32);
            Text($""{q.Time:ddd dd MMM HH:mm}  ({(DateTime.Now - q.Time).TotalHours:0.0} h ago)"", x + 12, y + 56);
            Fill(255, 220, 160);
            Text($""felt up to ~{FeltKm(q.Mag):N0} km away (the waves reach that far)"", x + 12, y + 80);
        }

        TextAlign(TextAlignH.Left, TextAlignV.Bottom);
        TextSize(13);
        string feed = quakesReq.Failed && quakes.Count == 0 ? ""earthquakes: "" + quakesReq.Status
                    : $""USGS · {quakes.Count} earthquakes · {(week ? ""M2.5+, last 7 days"" : ""all, last 24 hours"")}"" + (quakesReq.Stale ? "" · offline, older copy"" : """");
        string replay = replayT >= 0 ? $""  ·  replay {Math.Min(100, replayT / ReplaySeconds * 100):0}%"" : """";
        Fill(230, 235, 255, 170);
        Text($""{feed}{replay}  ·  Natural Earth · {countries?.Count ?? 0} countries"", 16, Height - 30);
        Fill(255, 255, 255, 80);
        Text(""drag: turn · wheel: zoom · hover: details · D/W: day/week · R: replay · E: relief · L: labels · Space: spin"", 16, Height - 12);

        TextAlign(TextAlignH.Right, TextAlignV.Center);
        Fill(255, 255, 255, 120);
        Text(""depth"", Width - 16, 20);
        int[] kms = { 0, 30, 70, 150, 300, 600 };
        for (int k = 0; k < kms.Length; k++)
        {
            var (r, g, b) = DepthColor(kms[k]);
            Fill(r, g, b);
            Circle(Width - 22, 40 + k * 18, 9);
            Fill(255, 255, 255, 150);
            Text($""{kms[k]} km"", Width - 34, 40 + k * 18);
        }
    }

    /// <summary>The loading panel: each request's own Status and Progress.</summary>
    void Loading()
    {
        float a = 1 - shown, w = 460, h = 190, x = (Width - w) / 2, y = (Height - h) / 2;
        NoStroke();
        Fill(8, 14, 30, 225 * a);
        Rect(x, y, w, h, 12);
        TextFont(font, 15);
        TextAlign(TextAlignH.Left, TextAlignV.Top);
        Fill(230, 240, 255, 255 * a);
        string[] spin = { ""◐"", ""◓"", ""◑"", ""◒"" };
        Text($""{spin[(int)(Millis() / 150) % 4]}  Loading the world"", x + 20, y + 16);
        Row(0, ""World map (country outlines)"", countriesReq.Status, countriesReq.Ready ? 1 : countriesReq.Progress);
        Row(1, ""Relief (elevation of the Earth)"", reliefReq.Status, reliefReq.Ready ? 1 : reliefReq.Progress);
        Row(2, ""Building the globe (land and sea)"", $""{classified:N0} / {sphere.Length:N0} points"", classified / (float)sphere.Length);
        Row(3, ""Earthquakes (USGS)"", quakes.Count > 0 ? $""{quakes.Count} earthquakes"" : quakesReq.Status, quakes.Count > 0 ? 1 : quakesReq.Progress);

        void Row(int k, string name, string state, float progress)
        {
            float ly = y + 52 + k * 34;
            TextSize(12);
            TextAlign(TextAlignH.Left, TextAlignV.Top);
            Fill(190, 205, 230, 255 * a);
            Text(name, x + 20, ly);
            TextAlign(TextAlignH.Right, TextAlignV.Top);
            Fill(140, 160, 190, 255 * a);
            Text(state, x + w - 20, ly);
            Fill(255, 255, 255, 25 * a);
            Rect(x + 20, ly + 17, w - 40, 4, 2);
            Fill(90, 190, 255, 255 * a);
            if (progress >= 0) Rect(x + 20, ly + 17, (w - 40) * Math.Min(1, progress), 4, 2);
            else Rect(x + 20 + (Millis() / 4) % (long)(w - 100), ly + 17, 60, 4, 2);   // size unknown: a sliding block
        }
    }

    // =====================================================================================
    // Input
    // =====================================================================================
    public override void MouseDragged()
    {
        yaw -= (MouseX - PMouseX) * 0.008f;
        pitch = Constrain(pitch - (MouseY - PMouseY) * 0.008f, -1.3f, 1.3f);
    }

    public override void MouseWheel(float delta) => zoom = Constrain(zoom * (delta > 0 ? 1 / 1.1f : 1.1f), 0.7f, 2.6f);

    public override void KeyPressed()
    {
        if (Key == 'w' || Key == 'W') { week = true; FetchQuakes(); }
        else if (Key == 'd' || Key == 'D') { week = false; FetchQuakes(); }
        else if (Key == 'r' || Key == 'R') { replayT = 0; rings.Clear(); }
        else if (Key == 'e' || Key == 'E') reliefScale = reliefScale == 0 ? 1 : reliefScale == 1 ? 2.2f : 0;
        else if (Key == 'l' || Key == 'L') labels = !labels;
        else if (Key == ' ') spinning = !spinning;
    }
}
";
    }
}
