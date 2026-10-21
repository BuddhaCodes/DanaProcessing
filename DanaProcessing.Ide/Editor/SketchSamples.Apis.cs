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
            new SketchSample(
                Loc.Tr("Tormenta desde arriba (RainViewer + Open-Meteo)", "A storm from above (RainViewer + Open-Meteo)"),
                Loc.Tr("El radar de lluvia real de las últimas dos horas convertido en un campo 3D de glifos: donde llueve flota una nube de caracteres, más alta y densa cuanto más fuerte llueve, y de ella caen gotas ( | / \\ ) inclinadas por el viento real del lugar, que salpican donde diluvia. Los cuadros del radar se reproducen en loop, así que ves moverse la tormenta. Radar de RainViewer (9 tiles por cuadro, pedidos de a poco para respetar su límite), viento y temperatura de Open-Meteo, costas de Natural Earth; todo gratis y sin clave. 1-6 cambian de ciudad, arrastrá para girar, ←/→ recorren los cuadros y M muestra una tormenta inventada (avisando) para cuando no llueve en ningún lado.",
                       "The real rain radar of the last two hours turned into a 3D field of glyphs: where it rains a cloud of characters floats, higher and denser the harder it rains, and drops ( | / \\ ) fall from it, leaning with the place's real wind and splashing where it pours. The radar frames play in a loop, so you watch the storm move. Radar from RainViewer (9 tiles per frame, requested a few at a time to respect its limit), wind and temperature from Open-Meteo, coasts from Natural Earth; all free and keyless. 1-6 switch city, drag to turn, Left/Right step through the frames and M shows a made-up storm (labelled as such) for when it isn't raining anywhere."),
                StormFromAbove,
                new[] { SampleTag.Data, SampleTag.ThreeD, SampleTag.Text, SampleTag.Interaction }),

            new SketchSample(
                Loc.Tr("El relieve de cualquier lugar (Open Topo Data)", "The relief of any place (Open Topo Data)"),
                Loc.Tr("Pide una grilla de 40 x 40 alturas alrededor de un lugar (16 pedidos de 100 puntos, uno por segundo como pide la API, guardados en disco) y la convierte en un terreno 3D de glifos que se arma fila por fila: ~ agua, . , llanura, + * colinas, # roca empinada, ^ nieve, con color por altura y sombreado. C dibuja curvas de nivel (marching squares) a su altura y S las guarda como SVG listo para plotter con BeginRaw(archivo, plotter: true): dos plumas y los trazos ordenados para que la pluma casi no viaje en el aire. 1-6: Pico Turquino, Everest, Gran Cañón, Teide, Fuji, Aconcagua.",
                       "Asks for a 40 x 40 grid of heights around a place (16 calls of 100 points, one a second as the API asks, kept on disk) and turns it into a 3D terrain of glyphs that builds itself row by row: ~ water, . , lowlands, + * hills, # steep rock, ^ snow, colored by altitude and hill-shaded. C draws contour lines (marching squares) at their height and S saves them as a plotter-ready SVG with BeginRaw(file, plotter: true): two pens and the strokes ordered so the pen barely travels in the air. 1-6: Pico Turquino, Everest, Grand Canyon, Teide, Fuji, Aconcagua."),
                TerrainAnywhere,
                new[] { SampleTag.Data, SampleTag.ThreeD, SampleTag.Text, SampleTag.Generative }),
        };

        private const string StormFromAbove =
@"// A real storm, seen from above, made of falling characters.
//
// Two free, keyless sources, downloaded in the background while the sketch keeps drawing:
// - RainViewer: the weather radar of the last two hours, as map tiles (10-minute steps).
//   https://www.rainviewer.com/api.html  -- 9 tiles around the place, one set per frame.
// - Open-Meteo: the wind right now at the place (speed and where it blows from).
//   https://open-meteo.com
// and Natural Earth's country outlines for the coasts.
//
// Every radar pixel block becomes a cell of a 3D field: where it rains, a cloud of glyphs
// floats -- higher and denser the harder it rains -- and drops ( | / \ ) fall from it to
// the ground, leaning with the real wind and splashing where it pours. The radar frames
// play in a loop, so you watch the storm move over the last two hours.
//
// Drag: turn.  Wheel: zoom.  1-6: another place.  Space: pause the radar loop.
// Left/Right: step through the frames.  M: a made-up storm (clearly labelled), for when
// it isn't raining anywhere you look.
public class StormFromAbove : Sketch
{
    // ---- settings -----------------------------------------------------------------------
    const int Zoom = 6;                          // RainViewer's free tiles go up to 7; 6 shows ~1,500 km
    const int Tiles = 3;                         // 3 x 3 tiles of 256 px around the place
    const int G = 96;                            // the field: 96 x 96 cells (8 x 8 radar pixels each)
    const int Px = Tiles * 256, Cell = Px / G;
    const string CountriesUrl = ""https://raw.githubusercontent.com/nvkelso/natural-earth-vector/master/geojson/ne_50m_admin_0_countries.geojson"";

    static readonly (string Name, float Lat, float Lon)[] Places =
    {
        (""Miami"", 25.76f, -80.19f), (""Houston"", 29.76f, -95.37f), (""London"", 51.51f, -0.13f),
        (""Madrid"", 40.42f, -3.70f), (""Tokyo"", 35.68f, 139.69f), (""Sydney"", -33.87f, 151.21f),
    };

    // ---- data ---------------------------------------------------------------------------
    int place;
    Request<JSONObject> mapsReq, windReq;
    Request<GeoMap> countriesReq;
    string host = """";
    readonly List<(long Time, string Path)> frames = new();     // newest last
    Request<PImage>[,] tiles;                                    // [frame, tile]
    float[][] grids;                                             // per frame: intensity 0..1 per cell
    Color[][] tints;                                             // per frame: the radar's color per cell
    readonly Queue<(int Frame, int Tile)> pending = new();
    long lastRequest;
    bool[] land;
    int landDone;
    float windKmh, windFromDeg, temperature = float.NaN;
    bool demo;

    // ---- view ---------------------------------------------------------------------------
    PFont mono, font;
    readonly PointCloud ground = new();
    float yaw = -0.35f, pitch = 0.55f, zoom = 1;
    float clock;                                                  // radar loop position, in frames
    bool paused;
    int ox, oy;                                                  // the top-left tile

    public override void Setup()
    {
        Size(1000, 620);
        mono = CreateFont(""Consolas"", 32);
        font = CreateFont(""Segoe UI"", 14);
        TextSizeSnap(1);                                         // thousands of glyph sizes -> a few
        countriesReq = RequestGeoJSON(CountriesUrl, cacheFor: TimeSpan.FromDays(365));
        Go(0);
    }

    void Go(int p)
    {
        place = p;
        var (_, lat, lon) = Places[p];
        string la = lat.ToString(System.Globalization.CultureInfo.InvariantCulture), lo = lon.ToString(System.Globalization.CultureInfo.InvariantCulture);
        mapsReq = RequestJSONObject(""https://api.rainviewer.com/public/weather-maps.json"", cacheFor: TimeSpan.FromMinutes(5));
        windReq = RequestJSONObject($""https://api.open-meteo.com/v1/forecast?latitude={la}&longitude={lo}&current=wind_speed_10m,wind_direction_10m,temperature_2m&wind_speed_unit=kmh"", cacheFor: TimeSpan.FromMinutes(15));
        float tx = TileX(lon), ty = TileY(lat);
        ox = (int)Math.Floor(tx) - 1;
        oy = (int)Math.Floor(ty) - 1;
        frames.Clear();
        pending.Clear();
        tiles = null; grids = null; tints = null;
        land = new bool[G * G];
        landDone = 0;
        windKmh = 0; temperature = float.NaN;
        clock = 0;
    }

    // ---- map math: Web Mercator tiles, like every web map ----------------------------------
    static float TileX(float lon) => (lon + 180) / 360 * (1 << Zoom);
    static float TileY(float lat)
    {
        double r = lat * Math.PI / 180;
        return (float)((1 - Math.Log(Math.Tan(r) + 1 / Math.Cos(r)) / Math.PI) / 2 * (1 << Zoom));
    }
    static float LonOf(float tx) => tx / (1 << Zoom) * 360 - 180;
    static float LatOf(float ty) => (float)(Math.Atan(Math.Sinh(Math.PI * (1 - 2 * ty / (1 << Zoom)))) * 180 / Math.PI);

    /// <summary>A cell of the field as world x (east) and z (north); the field is centered on the origin.</summary>
    static (float X, float Z) CellPos(float cx, float cy) => (cx - G / 2f, G / 2f - cy);

    // =====================================================================================
    // Loading, a little every frame
    // =====================================================================================
    void Load()
    {
        // The radar's frame list -> 9 tile requests per frame, newest frame first.
        if (frames.Count == 0 && mapsReq.Ready)
        {
            var m = mapsReq.Value;
            host = m.GetString(""host"");
            var past = m.GetJSONObject(""radar"")?.GetJSONArray(""past"");
            for (int i = 0; past != null && i < past.Size(); i++)
                frames.Add((past.GetJSONObject(i).GetInt(""time""), past.GetJSONObject(i).GetString(""path"")));
            tiles = new Request<PImage>[frames.Count, Tiles * Tiles];
            grids = new float[frames.Count][];
            tints = new Color[frames.Count][];
            for (int f = frames.Count - 1; f >= 0; f--)
                for (int t = 0; t < Tiles * Tiles; t++)
                    pending.Enqueue((f, t));
            clock = frames.Count - 1;
        }
        if (mapsReq.Failed && !demo) demo = true;

        // RainViewer allows 100 requests a minute: one every 0.65 s (cached ones don't count).
        while (pending.Count > 0 && Millis() - lastRequest > 650)
        {
            var (f, t) = pending.Dequeue();
            int tx = ox + t % Tiles, ty = oy + t / Tiles;
            tiles[f, t] = RequestPImage($""{host}{frames[f].Path}/256/{Zoom}/{tx}/{ty}/2/1_0.png"", cacheFor: TimeSpan.FromHours(3));
            lastRequest = Millis();
            if (tiles[f, t].FromCache) continue;
            break;
        }

        // A frame whose 9 tiles are in becomes a grid of intensities.
        for (int f = 0; tiles != null && f < frames.Count; f++)
        {
            if (grids[f] != null) continue;
            bool all = true;
            for (int t = 0; t < Tiles * Tiles; t++)
                all &= tiles[f, t] != null && tiles[f, t].Done;
            if (all) Grid(f);
        }

        // Land or sea under each cell: a few rows a frame, once the countries are in.
        if (countriesReq.Ready && landDone < G)
        {
            var map = countriesReq.Value;
            for (int row = landDone; row < Math.Min(G, landDone + 4); row++)
                for (int c = 0; c < G; c++)
                {
                    float lat = LatOf(oy + (row + 0.5f) * Cell / 256f), lon = LonOf(ox + (c + 0.5f) * Cell / 256f);
                    land[row * G + c] = map.FeatureAt(lat, lon) != null;
                }
            landDone = Math.Min(G, landDone + 4);
            BuildGround();
        }

        if (windReq.Ready && windKmh == 0)
        {
            var cur = windReq.Value.GetJSONObject(""current"");
            windKmh = cur?.GetFloat(""wind_speed_10m"") ?? 0;
            windFromDeg = cur?.GetFloat(""wind_direction_10m"") ?? 0;
            temperature = cur?.GetFloat(""temperature_2m"", float.NaN) ?? float.NaN;
            if (windKmh == 0) windKmh = 0.01f;
        }
    }

    /// <summary>Reads a frame's 9 tiles: each cell is the strongest of a few of its pixels.</summary>
    void Grid(int f)
    {
        var grid = new float[G * G];
        var tint = new Color[G * G];
        for (int t = 0; t < Tiles * Tiles; t++)
        {
            if (!tiles[f, t].Ready) continue;
            var img = tiles[f, t].Value;
            img.LoadPixels();
            var px = img.Pixels;
            int baseX = (t % Tiles) * 256, baseY = (t / Tiles) * 256;
            for (int cy = 0; cy < 256 / Cell; cy++)
                for (int cx = 0; cx < 256 / Cell; cx++)
                {
                    float best = 0; Color bestColor = default;
                    for (int sy = 1; sy < Cell; sy += 3)
                        for (int sx = 1; sx < Cell; sx += 3)
                        {
                            var c = px[(cy * Cell + sy) * 256 + cx * Cell + sx];
                            float v = Intensity(c);
                            if (v > best) { best = v; bestColor = c; }
                        }
                    int gi = (baseY / Cell + cy) * G + baseX / Cell + cx;
                    grid[gi] = best;
                    tint[gi] = bestColor;
                }
        }
        grids[f] = grid;
        tints[f] = tint;
    }

    /// <summary>
    /// How hard it rains, 0..1, from a pixel of RainViewer's ""Universal Blue"" scheme:
    /// transparent where it's dry, light to deep blue for light to moderate rain, then
    /// yellow, orange, red and magenta for heavy rain and storms.
    /// </summary>
    float Intensity(Color c)
    {
        if (c.A < 40) return 0;
        float h = Hue(c), s = Saturation(c), b = Brightness(c);
        float v;
        if (h >= 170 && h <= 260) v = 0.12f + 0.4f * (1 - b / 100f) + 0.15f * (s / 100f);  // blues: darker = more (s, b: 0..100)
        else if (h > 260) v = 0.95f;                                                          // magenta / purple
        else if (h >= 40) v = 0.6f;                                                           // yellow
        else if (h >= 15) v = 0.72f;                                                          // orange
        else v = 0.85f;                                                                       // red
        return Math.Clamp(v * (c.A / 255f), 0, 1);
    }

    // =====================================================================================
    // Each frame
    // =====================================================================================
    public override void Draw()
    {
        Load();
        int loaded = 0;
        for (int f = 0; grids != null && f < grids.Length; f++) if (grids[f] != null) loaded++;
        if (!paused && loaded > 1) clock += 1 / 40f;            // ~1.5 s per radar frame

        var (grid, tint) = demo ? Demo() : Current();
        var view = View3D.Orbit(yaw, pitch, 105 / zoom, new PVector(0, 3, 0), 50);

        Background(6, 10, 20);
        Points(ground, view);
        Coasts(view);
        if (grid != null) Rain(view, grid, tint);
        Hud(loaded);
    }

    /// <summary>The radar now: between the two loaded frames around the clock, blended.</summary>
    (float[] Grid, Color[] Tint) Current()
    {
        if (grids == null) return (null, null);
        var ready = new List<int>();
        for (int f = 0; f < grids.Length; f++) if (grids[f] != null) ready.Add(f);
        if (ready.Count == 0) return (null, null);
        float pos = ((clock % ready.Count) + ready.Count) % ready.Count;
        int a = (int)pos, b = (a + 1) % ready.Count;
        float k = Smooth(pos - a);
        var ga = grids[ready[a]]; var gb = grids[ready[b]];
        var blend = new float[G * G];
        for (int i = 0; i < blend.Length; i++) blend[i] = ga[i] + (gb[i] - ga[i]) * k;
        shownFrame = ready[k < 0.5f ? a : b];
        return (blend, tints[shownFrame]);
    }
    int shownFrame;

    static float Smooth(float x) => x * x * (3 - 2 * x);

    void BuildGround()
    {
        ground.Clear();
        ground.DepthSort = false;
        for (int r = 0; r < G; r++)
            for (int c = 0; c < G; c++)
            {
                if ((r + c) % 2 == 1) continue;                  // a checkerboard of dots is plenty
                var (x, z) = CellPos(c + 0.5f, r + 0.5f);
                bool isLand = land[r * G + c];
                ground.Add(x, 0, z, isLand ? Color(70, 100, 85) : Color(22, 40, 70), isLand ? 0.35f : 0.25f);
            }
    }

    void Coasts(View3D view)
    {
        if (!countriesReq.Ready) return;
        Stroke(120, 170, 160, 150);
        StrokeWeight(1);
        foreach (var f in countriesReq.Value.Features)
        {
            if (f.MaxLon < LonOf(ox) - 1 || f.MinLon > LonOf(ox + Tiles) + 1) continue;
            foreach (var ring in f.Rings)
            {
                float px = 0, py = 0; bool prev = false;
                for (int i = 0; i < ring.Count; i++)
                {
                    float cx = (TileX(ring.Lon[i]) - ox) * 256 / Cell, cy = (TileY(ring.Lat[i]) - oy) * 256 / Cell;
                    bool inside = cx >= 0 && cy >= 0 && cx <= G && cy <= G;
                    var (wx, wz) = CellPos(cx, cy);
                    var (sx, sy, d) = view.Project(new PVector(wx, 0.05f, wz), Width, Height);
                    bool ok = inside && d > 0;
                    if (ok && prev) Line(px, py, sx, sy);
                    px = sx; py = sy; prev = ok;
                }
            }
        }
    }

    void Rain(View3D view, float[] grid, Color[] tint)
    {
        // The wind: where it blows *to* (from + 180), as an east/north vector; stronger wind, more lean.
        float to = Radians(windFromDeg + 180);
        float lean = Math.Min(windKmh, 80) / 80f * 0.9f;
        float wx = Sin(to) * lean, wz = Cos(to) * lean;
        float t = Millis() / 1000f;

        var glyphs = new List<(float Depth, float X, float Y, float Size, char G, int Rgb, float A)>(8000);
        void Put(float x, float y, float z, char g, int rgb, float a, float scale)
        {
            var (sx, sy, d) = view.Project(new PVector(x, y, z), Width, Height);
            if (d <= 0.5f) return;
            float size = view.Scale(d, Height) * scale;
            if (size < 2.5f) return;
            glyphs.Add((d, sx, sy, Math.Min(size, 80), g, rgb, a));
        }

        for (int i = 0; i < G * G; i++)
        {
            float v = grid[i];
            if (v < 0.06f) continue;
            int r = i / G, c = i % G;
            var (x, z) = CellPos(c + 0.5f, r + 0.5f);
            var tc = tint != null && tint[i].A > 0 ? tint[i] : Color(120, 170, 255);
            // The cloud: higher and denser where it rains harder.
            float top = 7 + v * 22;
            char cloud = v > 0.8f ? '@' : v > 0.6f ? '%' : v > 0.4f ? '#' : v > 0.2f ? '*' : '+';
            Put(x, top, z, cloud, Pack(tc.R * 0.6f + 90, tc.G * 0.6f + 90, tc.B * 0.6f + 100), 0.55f + 0.45f * v, 1.3f);
            // The drops: one to four per cell, falling from the cloud, pushed by the wind.
            int drops = 1 + (int)(v * 3.5f);
            for (int k = 0; k < drops; k++)
            {
                float phase = Hash(i * 7 + k);
                float fall = (t * (0.9f + v) / 1.2f + phase) % 1;          // 0 at the cloud, 1 at the ground
                float y = top * (1 - fall);
                float dx = wx * (top - y), dz = wz * (top - y);
                Put(x + dx + (phase - 0.5f) * 0.6f, y, z + dz, DropGlyph(view, wx, wz), Pack(170, 205, 255), 0.85f, 1.4f);
                if (v > 0.35f && fall > 0.92f)
                    Put(x + wx * top, 0.1f, z + wz * top, 'o', Pack(190, 220, 255), (fall - 0.92f) * 10, 0.8f);  // splash
            }
        }

        glyphs.Sort((p, q) => q.Depth.CompareTo(p.Depth));       // far first
        TextFont(mono, 16);
        TextAlign(TextAlignH.Center, TextAlignV.Center);
        foreach (var g in glyphs)
        {
            Fill((g.Rgb >> 16) & 255, (g.Rgb >> 8) & 255, g.Rgb & 255, 255 * Math.Clamp(g.A, 0, 1));
            TextSize(g.Size);
            Text(g.G.ToString(), g.X, g.Y);
        }
    }

    /// <summary>Which glyph draws a drop: | when it falls straight on screen, / or \ when the wind leans it.</summary>
    char DropGlyph(View3D view, float wx, float wz)
    {
        var (x0, y0, _) = view.Project(new PVector(0, 1, 0), Width, Height);
        var (x1, y1, _) = view.Project(new PVector(wx, 0, wz), Width, Height);
        float slope = (x1 - x0) / Math.Max(1, y1 - y0);
        return slope > 0.25f ? '\\' : slope < -0.25f ? '/' : '|';
    }

    static int Pack(float r, float g, float b) =>
        ((int)Math.Clamp(r, 0, 255) << 16) | ((int)Math.Clamp(g, 0, 255) << 8) | (int)Math.Clamp(b, 0, 255);

    static float Hash(int n)
    {
        unchecked
        {
            uint x = (uint)n * 747796405u + 2891336453u;
            x = ((x >> (int)((x >> 28) + 4)) ^ x) * 277803737u;
            return ((x >> 22) ^ x) / 4294967296f;
        }
    }

    /// <summary>A made-up storm (M, or when the radar can't be reached): a spiral band that turns.</summary>
    (float[] Grid, Color[] Tint) Demo()
    {
        var grid = new float[G * G];
        float t = Millis() / 1000f * 0.15f;
        for (int r = 0; r < G; r++)
            for (int c = 0; c < G; c++)
            {
                float x = (c - G / 2f) / (G / 2f), y = (r - G / 2f) / (G / 2f);
                float d = Sqrt(x * x + y * y), a = Atan2(y, x);
                float band = Sin(a * 2 + d * 9 - t * 4) * 0.5f + 0.5f;
                float v = band * Math.Max(0, 1 - d) * (0.6f + 0.8f * Noise(c * 0.08f + t, r * 0.08f));
                grid[r * G + c] = Math.Clamp(v - 0.15f, 0, 1);
            }
        return (grid, null);
    }

    // =====================================================================================
    // Text on screen
    // =====================================================================================
    void Hud(int loaded)
    {
        var (name, lat, lon) = Places[place];
        TextFont(font, 22);
        TextAlign(TextAlignH.Left, TextAlignV.Top);
        Fill(235, 240, 255);
        Text(name, 20, 16);
        TextFont(font, 13);
        Fill(170, 185, 215);
        string when = demo ? ""MADE-UP STORM (demo, not real data)""
            : frames.Count > 0 && grids?[shownFrame] != null ? $""radar {DateTimeOffset.FromUnixTimeSeconds(frames[shownFrame].Time).LocalDateTime:HH:mm}  ·  frame {shownFrame + 1}/{frames.Count}{(paused ? ""  ·  paused"" : """")}""
            : ""radar: "" + (mapsReq.Done ? $""loading tiles ({loaded}/{frames.Count} frames)"" : mapsReq.Status);
        Text(when, 20, 46);
        string wind = windReq.Ready
            ? $""wind {windKmh:0} km/h from {Compass(windFromDeg)}"" + (float.IsNaN(temperature) ? """" : $""  ·  {temperature:0} °C"")
            : ""wind: "" + windReq.Status;
        Text(wind, 20, 66);

        // A small compass: where the wind blows to.
        float cx = Width - 60, cy = 60;
        NoFill(); Stroke(255, 255, 255, 60); StrokeWeight(1);
        Circle(cx, cy, 70);
        float to = Radians(windFromDeg + 180 - Degrees(yaw));
        Stroke(170, 205, 255); StrokeWeight(3);
        Line(cx, cy, cx + Sin(to) * 30, cy - Cos(to) * 30);
        NoStroke(); Fill(255, 255, 255, 120);
        TextAlign(TextAlignH.Center, TextAlignV.Center);
        Text(""wind"", cx, cy + 48);

        TextAlign(TextAlignH.Left, TextAlignV.Bottom);
        Fill(255, 255, 255, 90);
        Text(""drag: turn · wheel: zoom · 1-6: place · Space: pause · ←/→: frames · M: made-up storm"", 20, Height - 14);
        Fill(255, 255, 255, 60);
        Text(""Radar: RainViewer · Wind: Open-Meteo · Coasts: Natural Earth"", 20, Height - 32);
    }

    static string Compass(float deg) => new[] { ""N"", ""NE"", ""E"", ""SE"", ""S"", ""SW"", ""W"", ""NW"" }[(int)Math.Round(((deg % 360) + 360) % 360 / 45) % 8];

    // =====================================================================================
    // Input
    // =====================================================================================
    public override void MouseDragged()
    {
        yaw -= (MouseX - PMouseX) * 0.008f;
        pitch = Constrain(pitch - (MouseY - PMouseY) * 0.008f, 0.15f, 1.45f);
    }

    public override void MouseWheel(float delta) => zoom = Constrain(zoom * (delta > 0 ? 1 / 1.1f : 1.1f), 0.6f, 3.5f);

    public override void KeyPressed()
    {
        if (Key >= '1' && Key <= '6') Go(Key - '1');
        else if (Key == ' ') paused = !paused;
        else if (Key == 'm' || Key == 'M') demo = !demo;
        else if (KeyCode == RIGHT) clock = (float)Math.Floor(clock) + 1;
        else if (KeyCode == LEFT) clock = (float)Math.Floor(clock) - 1;
    }
}
";

        private const string TerrainAnywhere =
@"// The relief of any place on Earth, as a 3D terrain of glyphs -- or as contour lines you
// can send to a pen plotter.
//
// The heights come from Open Topo Data (https://www.opentopodata.org), free and keyless:
// you ask for up to 100 points per call, one call a second. The sketch asks for a 40 x 40
// grid around the place in 16 calls, paced, each one cached on disk -- so the terrain
// builds itself row by row the first time, and is there at once the next.
//
// - T: the terrain. Each point is a glyph at its height: ~ water, . , lowlands, + * hills,
//   # steep rock, ^ snow; colored by altitude and lit from the north-west.
// - C: contour lines, every N meters (thicker every 5th), computed with marching squares
//   and drawn at their height.
// - S: save the contours as a plotter-ready SVG, seen from above: BeginRaw(path, plotter:
//   true) puts each pen color on its own layer and orders the strokes so the pen barely
//   travels in the air.
//
// 1-6: another place.  Drag: turn.  Wheel: zoom.  Up/Down: vertical exaggeration.
public class TerrainAnywhere : Sketch
{
    // ---- settings -----------------------------------------------------------------------
    const string Dataset = ""mapzen"";             // global, with the sea floor; also srtm90m, aster30m, etopo1...
    const int N = 40;                            // N x N heights -> N*N/100 calls
    const int M = 72;                            // drawn as M x M glyphs (smoothly interpolated)
    const float W = 60;                          // the terrain's width in world units

    static readonly (string Name, float Lat, float Lon, float Span)[] Places =
    {
        (""Pico Turquino, Cuba"", 19.99f, -76.84f, 0.30f),
        (""Everest"", 27.988f, 86.925f, 0.20f),
        (""Grand Canyon"", 36.10f, -112.11f, 0.25f),
        (""Teide, Tenerife"", 28.27f, -16.64f, 0.30f),
        (""Fuji"", 35.36f, 138.73f, 0.25f),
        (""Aconcagua"", -32.65f, -70.01f, 0.30f),
    };

    // ---- data ---------------------------------------------------------------------------
    int place;
    readonly float[] height = new float[N * N];               // meters; NaN until it arrives
    readonly Request<JSONObject>[] calls = new Request<JSONObject>[N * N / 100];
    int nextCall;
    long lastCall = -10000;
    int arrived, failed;
    bool[] read;
    float lo, hi;                                            // elevation range so far
    bool made;                                               // made-up terrain (offline)

    // ---- view ---------------------------------------------------------------------------
    PFont mono, font;
    bool contours;
    float yaw = -0.6f, pitch = 0.55f, zoom = 1, exaggeration = 2.5f;
    string saved = """";
    long savedAt;

    public override void Setup()
    {
        Size(1000, 640);
        mono = CreateFont(""Consolas"", 32);
        font = CreateFont(""Segoe UI"", 14);
        TextSizeSnap(1);
        Go(0);
    }

    void Go(int p)
    {
        place = p;
        Array.Fill(height, float.NaN);
        Array.Clear(calls);
        read = new bool[calls.Length];
        nextCall = 0; arrived = 0; failed = 0; made = false;
        lo = float.MaxValue; hi = float.MinValue;
        segments = null;
    }

    (float Lat, float Lon) LatLonOf(int i)
    {
        var (_, lat, lon, span) = Places[place];
        int r = i / N, c = i % N;
        // Keep the area square on the ground: a degree of longitude shrinks with latitude.
        float lonSpan = span / MathF.Cos(lat * MathF.PI / 180);
        return (lat + span / 2 - span * r / (N - 1), lon - lonSpan / 2 + lonSpan * c / (N - 1));
    }

    // =====================================================================================
    // Loading: one call a second, 100 points each
    // =====================================================================================
    void Load()
    {
        if (nextCall < calls.Length && Millis() - lastCall > 1100 && !made)
        {
            var points = new System.Text.StringBuilder();
            for (int k = 0; k < 100; k++)
            {
                var (la, lo2) = LatLonOf(nextCall * 100 + k);
                if (k > 0) points.Append(""%7C"");                // ""|"", escaped
                points.Append(la.ToString(""0.#####"", System.Globalization.CultureInfo.InvariantCulture)).Append(',')
                      .Append(lo2.ToString(""0.#####"", System.Globalization.CultureInfo.InvariantCulture));
            }
            calls[nextCall] = RequestJSONObject($""https://api.opentopodata.org/v1/{Dataset}?locations={points}"", cacheFor: TimeSpan.FromDays(365));
            nextCall++;
            lastCall = Millis();
        }
        for (int k = 0; k < calls.Length; k++)
        {
            var call = calls[k];
            if (call == null || read[k] || !call.Done) continue;
            read[k] = true;
            if (call.Failed) { failed++; continue; }
            if (call.FromCache) lastCall -= 1100;               // cached: no need to wait for the next one
            var results = call.Value.GetJSONArray(""results"");
            for (int j = 0; results != null && j < results.Size(); j++)
            {
                // ""elevation"" is null where the dataset has no data: count it as sea level.
                float e = results.GetJSONObject(j).GetFloat(""elevation"", 0);
                int i = k * 100 + j;
                height[i] = e;
                lo = Math.Min(lo, e); hi = Math.Max(hi, e);
                arrived++;
            }
        }
        // Offline: after two failed calls, a made-up terrain, clearly labelled.
        if (failed >= 2 && !made)
        {
            made = true;
            for (int i = 0; i < N * N; i++)
            {
                float x = i % N / (float)N, y = i / N / (float)N;
                float d = Dist(x, y, 0.5f, 0.5f);
                height[i] = 3000 * Math.Max(0, 1 - d * 1.8f) * (0.6f + 0.6f * Noise(x * 4, y * 4)) + 400 * Noise(x * 12, y * 12);
            }
            lo = 0; hi = 0;
            foreach (var h in height) hi = Math.Max(hi, h);
            arrived = N * N;
        }
    }

    /// <summary>The height (m) at a fractional grid position, bilinear; NaN where not loaded yet.</summary>
    float At(float gx, float gy)
    {
        int x0 = Math.Clamp((int)gx, 0, N - 2), y0 = Math.Clamp((int)gy, 0, N - 2);
        float fx = Math.Clamp(gx - x0, 0, 1), fy = Math.Clamp(gy - y0, 0, 1);
        float a = height[y0 * N + x0], b = height[y0 * N + x0 + 1], c = height[(y0 + 1) * N + x0], d = height[(y0 + 1) * N + x0 + 1];
        return Lerp(Lerp(a, b, fx), Lerp(c, d, fx), fy);
    }

    /// <summary>World units per meter of height: true scale times the exaggeration.</summary>
    float VerticalScale => W / (Places[place].Span * 111_000f) * exaggeration;

    PVector World(float gx, float gy, float meters) =>
        new PVector(gx / (M - 1) * W - W / 2, (meters - Math.Min(lo, 0)) * VerticalScale, W / 2 - gy / (M - 1) * W);

    // =====================================================================================
    // Each frame
    // =====================================================================================
    public override void Draw()
    {
        Load();
        Background(8, 12, 20);
        var view = View3D.Orbit(yaw, pitch, 64 / zoom, new PVector(0, 3, 0), 50);
        if (arrived > 0)
        {
            if (contours) DrawContours(view);
            else DrawTerrain(view);
            DrawPeak(view);
        }
        Hud();
    }

    void DrawTerrain(View3D view)
    {
        var glyphs = new List<(float D, float X, float Y, float S, char G, int Rgb)>(M * M);
        float step = (N - 1f) / (M - 1);
        for (int r = 0; r < M; r++)
            for (int c = 0; c < M; c++)
            {
                float gx = c * step, gy = r * step;
                float h = At(gx, gy);
                if (float.IsNaN(h)) continue;
                // Slope and light: how the height changes east-west and north-south.
                float hx = At(gx + step, gy) - At(gx - step, gy), hy = At(gx, gy + step) - At(gx, gy - step);
                if (float.IsNaN(hx)) hx = 0;
                if (float.IsNaN(hy)) hy = 0;
                float metersPerStep = Places[place].Span * 111_000f / (M - 1);
                float slope = MathF.Sqrt(hx * hx + hy * hy) / (2 * metersPerStep);
                float light = Math.Clamp(1 + (hy - hx) / (2 * metersPerStep) * 1.5f, 0.45f, 1.5f);   // lit from the north-west
                float t = hi > lo ? (h - Math.Max(lo, 0)) / (hi - Math.Max(lo, 0)) : 0;
                char g; float cr, cg, cb;
                if (h <= 0) { g = '~'; cr = 40; cg = 90; cb = 170; }
                else if (t > 0.82f) { g = '^'; cr = 235; cg = 240; cb = 250; }
                else if (slope > 0.6f) { g = '#'; cr = 150; cg = 135; cb = 120; }
                else if (t > 0.45f) { g = '*'; cr = 165; cg = 150; cb = 105; }
                else if (t > 0.2f) { g = '+'; cr = 120; cg = 160; cb = 95; }
                else { g = slope > 0.15f ? ',' : '.'; cr = 80; cg = 170; cb = 110; }
                var (sx, sy, d) = view.Project(World(c, r, h), Width, Height);
                if (d <= 0.5f) continue;
                glyphs.Add((d, sx, sy, Math.Min(view.Scale(d, Height) * 1.1f, 60), g, Pack(cr * light, cg * light, cb * light)));
            }
        glyphs.Sort((a, b) => b.D.CompareTo(a.D));
        TextFont(mono, 16);
        TextAlign(TextAlignH.Center, TextAlignV.Center);
        foreach (var gl in glyphs)
        {
            Fill((gl.Rgb >> 16) & 255, (gl.Rgb >> 8) & 255, gl.Rgb & 255);
            TextSize(gl.S);
            Text(gl.G.ToString(), gl.X, gl.Y);
        }
    }

    /// <summary>Contour spacing: a round number giving ~16 lines over the range (10, 20, 50, 100, 200 m...).</summary>
    float Interval()
    {
        float raw = Math.Max(1, hi - Math.Max(lo, 0)) / 16;
        float p = MathF.Pow(10, MathF.Floor(MathF.Log10(raw)));
        return raw / p < 1.5f ? p : raw / p < 3.5f ? 2 * p : raw / p < 7.5f ? 5 * p : 10 * p;
    }

    /// <summary>
    /// Marching squares: for each level, every grid square whose corners straddle it gets a
    /// segment between the edge crossings. Calls <paramref name=""segment""/>(level index,
    /// height, start, end) with grid coordinates.
    /// </summary>
    void MarchingSquares(Action<int, float, (float X, float Y), (float X, float Y)> segment)
    {
        float interval = Interval();
        float step = (N - 1f) / (M - 1);
        var h = new float[M * M];
        for (int r = 0; r < M; r++)
            for (int c = 0; c < M; c++)
                h[r * M + c] = At(c * step, r * step);
        int first = (int)MathF.Ceiling(Math.Max(lo, 0) / interval), last = (int)MathF.Floor(hi / interval);
        for (int li = first; li <= last; li++)
        {
            float level = li * interval;
            for (int r = 0; r < M - 1; r++)
                for (int c = 0; c < M - 1; c++)
                {
                    float a = h[r * M + c], b = h[r * M + c + 1], d = h[(r + 1) * M + c], e = h[(r + 1) * M + c + 1];
                    if (float.IsNaN(a) || float.IsNaN(b) || float.IsNaN(d) || float.IsNaN(e)) continue;
                    int code = (a > level ? 8 : 0) | (b > level ? 4 : 0) | (e > level ? 2 : 0) | (d > level ? 1 : 0);
                    if (code == 0 || code == 15) continue;
                    // Where the level crosses each edge: top (a-b), right (b-e), bottom (d-e), left (a-d).
                    (float, float) Top() => (c + (level - a) / (b - a), r);
                    (float, float) Right() => (c + 1, r + (level - b) / (e - b));
                    (float, float) Bottom() => (c + (level - d) / (e - d), r + 1);
                    (float, float) Left() => (c, r + (level - a) / (d - a));
                    switch (code)
                    {
                        case 1: case 14: segment(li, level, Left(), Bottom()); break;
                        case 2: case 13: segment(li, level, Bottom(), Right()); break;
                        case 3: case 12: segment(li, level, Left(), Right()); break;
                        case 4: case 11: segment(li, level, Top(), Right()); break;
                        case 6: case 9: segment(li, level, Top(), Bottom()); break;
                        case 7: case 8: segment(li, level, Left(), Top()); break;
                        case 5: segment(li, level, Left(), Top()); segment(li, level, Bottom(), Right()); break;
                        case 10: segment(li, level, Top(), Right()); segment(li, level, Left(), Bottom()); break;
                    }
                }
        }
    }

    void DrawContours(View3D view)
    {
        // A faint floor of dots, so the lines have something to stand over.
        NoStroke();
        Fill(255, 255, 255, 25);
        for (int r = 0; r < M; r += 4)
            for (int c = 0; c < M; c += 4)
            {
                var (sx, sy, d) = view.Project(World(c, r, Math.Min(lo, 0)), Width, Height);
                if (d > 0) Circle(sx, sy, 2);
            }
        foreach (var (li, level, p, q) in Segments())
        {
            var (x0, y0, d0) = view.Project(World(p.X, p.Y, level), Width, Height);
            var (x1, y1, d1) = view.Project(World(q.X, q.Y, level), Width, Height);
            if (d0 <= 0 || d1 <= 0) continue;
            float t = hi > 0 ? level / hi : 0;
            bool index = li % 5 == 0;
            Stroke(Lerp(90, 255, t), Lerp(200, 220, t), Lerp(160, 140, t), index ? 255 : 170);
            StrokeWeight(index ? 2 : 1);
            Line(x0, y0, x1, y1);
        }
    }

    // The contour segments only change when new heights arrive: computed once, then reused.
    List<(int Li, float Level, (float X, float Y) P, (float X, float Y) Q)> segments;
    int segmentsFor = -1;

    List<(int Li, float Level, (float X, float Y) P, (float X, float Y) Q)> Segments()
    {
        if (segmentsFor != arrived || segments == null)
        {
            segments = new();
            MarchingSquares((li, level, p, q) => segments.Add((li, level, p, q)));
            segmentsFor = arrived;
        }
        return segments;
    }

    /// <summary>The highest point: a marker and its height.</summary>
    void DrawPeak(View3D view)
    {
        int best = -1;
        for (int i = 0; i < N * N; i++)
            if (!float.IsNaN(height[i]) && (best < 0 || height[i] > height[best])) best = i;
        if (best < 0) return;
        float gx = best % N * (M - 1f) / (N - 1), gy = best / N * (M - 1f) / (N - 1);
        var (sx, sy, d) = view.Project(World(gx, gy, height[best]), Width, Height);
        if (d <= 0) return;
        Stroke(255, 200, 90); StrokeWeight(1.5f);
        Line(sx, sy, sx, sy - 30);
        NoStroke(); Fill(255, 200, 90);
        Triangle(sx - 6, sy - 30, sx + 6, sy - 30, sx, sy - 40);
        TextFont(font, 13);
        TextAlign(TextAlignH.Center, TextAlignV.Bottom);
        Fill(255, 235, 200);
        Text($""{height[best]:N0} m"", sx, sy - 44);
    }

    // =====================================================================================
    // The plotter SVG: the contours seen from straight above, two pens
    // =====================================================================================
    void SaveSvg()
    {
        string name = Places[place].Name.Split(',')[0].Replace(' ', '-').ToLowerInvariant();
        string file = $""contours-{name}.svg"";
        float margin = 40, size = Math.Min(Width, Height) - 2 * margin;
        float left = (Width - size) / 2;
        BeginRaw(file, plotter: true);
        Background(255);
        NoFill();
        foreach (var (li, level, p, q) in Segments())
        {
            bool index = li % 5 == 0;
            Stroke(index ? Color(120, 60, 20) : Color(0, 0, 0));            // two pens: brown index lines, black the rest
            StrokeWeight(index ? 1.2f : 0.6f);
            Line(left + p.X / (M - 1) * size, margin + p.Y / (M - 1) * size, left + q.X / (M - 1) * size, margin + q.Y / (M - 1) * size);
        }
        var (n, lat, lon, _) = Places[place];
        Fill(0);
        TextFont(font, 14);
        TextAlign(TextAlignH.Left, TextAlignV.Top);
        Text($""{n}  ·  {lat:0.000}, {lon:0.000}  ·  contours every {Interval():0} m"", left, 12);
        EndRaw();
        saved = file;
        savedAt = Millis();
    }

    // =====================================================================================
    // Text on screen
    // =====================================================================================
    void Hud()
    {
        var (name, lat, lon, span) = Places[place];
        TextFont(font, 22);
        TextAlign(TextAlignH.Left, TextAlignV.Top);
        Fill(235, 240, 255);
        Text(name, 20, 16);
        TextFont(font, 13);
        Fill(170, 185, 215);
        Text($""{lat:0.000}, {lon:0.000}  ·  {span * 111:0} x {span * 111:0} km  ·  heights x{exaggeration:0.#}"", 20, 46);
        if (arrived > 0)
            Text($""from {Math.Max(lo, 0):N0} to {hi:N0} m{(contours ? $""  ·  contours every {Interval():0} m"" : """")}"", 20, 66);
        if (made)
        {
            Fill(255, 170, 110);
            Text(""no connection: MADE-UP terrain (not real data)"", 20, 86);
        }
        else if (arrived < N * N)
        {
            // Loading: the points that came in, and the call that's out.
            int i = Math.Max(0, nextCall - 1);
            var call = calls[i];
            Fill(255, 255, 255, 30);
            Rect(20, 92, 260, 5, 2);
            Fill(90, 190, 255);
            Rect(20, 92, 260f * arrived / (N * N), 5, 2);
            Fill(170, 185, 215);
            Text($""heights {arrived:N0} / {N * N:N0}  ·  call {nextCall}/{calls.Length}: {call?.Status ?? ""...""}"", 20, 102);
        }
        if (saved != """" && Millis() - savedAt < 4000)
        {
            Fill(150, 230, 170);
            TextAlign(TextAlignH.Right, TextAlignV.Top);
            Text($""saved {saved} (plotter-ready: 2 pens)"", Width - 20, 20);
        }
        TextAlign(TextAlignH.Left, TextAlignV.Bottom);
        Fill(255, 255, 255, 90);
        Text(""1-6: place · T: terrain · C: contours · S: save plotter SVG · drag: turn · wheel: zoom · Up/Down: heights"", 20, Height - 14);
        Fill(255, 255, 255, 60);
        Text($""Elevation: Open Topo Data ({Dataset})"", 20, Height - 32);
    }

    static int Pack(float r, float g, float b) =>
        ((int)Math.Clamp(r, 0, 255) << 16) | ((int)Math.Clamp(g, 0, 255) << 8) | (int)Math.Clamp(b, 0, 255);

    // =====================================================================================
    // Input
    // =====================================================================================
    public override void MouseDragged()
    {
        yaw -= (MouseX - PMouseX) * 0.008f;
        pitch = Constrain(pitch - (MouseY - PMouseY) * 0.008f, 0.1f, 1.5f);
    }

    public override void MouseWheel(float delta) => zoom = Constrain(zoom * (delta > 0 ? 1 / 1.1f : 1.1f), 0.5f, 4);

    public override void KeyPressed()
    {
        if (Key >= '1' && Key <= '6') Go(Key - '1');
        else if (Key == 't' || Key == 'T') contours = false;
        else if (Key == 'c' || Key == 'C') contours = true;
        else if ((Key == 's' || Key == 'S') && arrived > 0) SaveSvg();
        else if (KeyCode == UP) exaggeration = Math.Min(6, exaggeration + 0.5f);
        else if (KeyCode == DOWN) exaggeration = Math.Max(0.5f, exaggeration - 0.5f);
    }
}
";

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
