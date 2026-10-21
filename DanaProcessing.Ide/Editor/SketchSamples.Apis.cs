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
                Loc.Tr("El radar de lluvia real de las últimas dos horas, en cualquier lugar del mundo, convertido en una tormenta 3D: donde llueve flotan nubes de puntos suaves, más altas y gruesas cuanto más fuerte llueve, y de ellas caen trazos de lluvia inclinados por el viento real, con salpicaduras donde diluvia y relámpagos donde hay tormenta. Es un mapa: arrastrá para moverte, la rueda va del planeta entero (zoom 2) a una región (zoom 7) y los tiles ya vistos quedan guardados. Los cuadros del radar se reproducen en loop (Espacio pausa todo). Radar de RainViewer (pedido de a poco para respetar su límite de 100 por minuto), viento y temperatura de Open-Meteo donde mirás, costas de Natural Earth; todo gratis y sin clave. Clic derecho o Q/E/R/F giran, 1-6 saltan a ciudades, , y . recorren los cuadros y M muestra una tormenta inventada (avisando).",
                       "The real rain radar of the last two hours, anywhere in the world, turned into a 3D storm: where it rains, clouds of soft points float, higher and thicker the harder it rains, and streaks of rain fall from them leaning with the real wind, splashing where it pours and flashing where it storms. It's a map: drag to move, the wheel goes from the whole planet (zoom 2) to a region (zoom 7), and tiles already seen are kept. The radar frames play in a loop (Space pauses everything). Radar from RainViewer (requested a little at a time to respect its 100-a-minute limit), wind and temperature from Open-Meteo where you look, coasts from Natural Earth; all free and keyless. Right-drag or Q/E/R/F turn, 1-6 jump to cities, , and . step through the frames and M shows a made-up storm (labelled as such)."),
                StormFromAbove,
                new[] { SampleTag.Data, SampleTag.ThreeD, SampleTag.Interaction }),

            new SketchSample(
                Loc.Tr("El relieve de cualquier país (Open Topo Data)", "The relief of any country (Open Topo Data)"),
                Loc.Tr("Elegí un país en el mapa del mundo y el sketch pone sobre él una grilla de unos 2.000 puntos (más juntos en países chicos, más separados en grandes), se queda con los que caen dentro de su contorno y le pide sus alturas a Open Topo Data: 100 puntos por pedido, uno por segundo como pide la API, cada pedido guardado en disco un mes. El país aparece plano con su forma real y se levanta por partes a medida que llegan las alturas, como una malla con colores de atlas y sombreado. C dibuja curvas de nivel (marching squares) a su altura y S las guarda con el borde como SVG listo para plotter con BeginRaw(archivo, plotter: true). M vuelve al mapa, R elige un país al azar, ↑/↓ exageran las alturas.",
                       "Pick a country on the world map and the sketch lays a grid of about 2,000 points over it (closer together for small countries, farther apart for big ones), keeps the ones inside its outline and asks Open Topo Data for their heights: 100 points per call, one a second as the API asks, each call kept on disk for a month. The country appears flat with its real shape and rises piece by piece as the heights arrive, as a mesh with atlas colors and hill shading. C draws contour lines (marching squares) at their height and S saves them with the border as a plotter-ready SVG with BeginRaw(file, plotter: true). M goes back to the map, R picks a random country, Up/Down exaggerate the heights."),
                TerrainAnywhere,
                new[] { SampleTag.Data, SampleTag.ThreeD, SampleTag.Generative, SampleTag.Interaction }),
        };

        private const string StormFromAbove =
@"// A real storm, seen from above -- anywhere on Earth.
//
// Two free, keyless sources, downloaded in the background while the sketch keeps drawing:
// - RainViewer: the weather radar of the last two hours, as map tiles (10-minute steps).
//   https://www.rainviewer.com/api.html
// - Open-Meteo: the wind right now where you're looking (speed and where it blows from).
//   https://open-meteo.com
// and Natural Earth's country outlines for the coasts.
//
// The radar becomes a 3D field: where it rains, a cloud of soft points floats -- higher and
// thicker the harder it rains -- and streaks of rain fall from it, leaning with the real
// wind, splashing where it pours and flashing where it storms. The radar frames play in a
// loop, so you watch the storm move over the last two hours.
//
// It's a map: drag to move over the world, the wheel zooms from the whole planet (zoom 2)
// down to a region (zoom 7, the most RainViewer's free tiles give). Tiles already seen are
// kept, so going back is instant.
//
// Drag: move.  Right-drag (or Q/E, R/F): turn and tilt.  Wheel or +/-: zoom.
// Arrows: move.  1-6: jump to a city.  Space: pause.  , and .: step through the frames.
// M: a made-up storm (clearly labelled), for when it isn't raining anywhere you look.
public class StormFromAbove : Sketch
{
    // ---- settings -----------------------------------------------------------------------
    const int MinZoom = 2, MaxZoom = 7;          // RainViewer's free tiles go up to 7
    const int Tiles = 3;                         // 3 x 3 tiles of 256 px around the center
    const int G = 96;                            // the field: 96 x 96 cells (8 x 8 radar pixels each)
    const int Px = Tiles * 256, Cell = Px / G;
    const float Window = 40;                     // cells drawn around the center (a round window)
    const string CountriesUrl = ""https://raw.githubusercontent.com/nvkelso/natural-earth-vector/master/geojson/ne_50m_admin_0_countries.geojson"";

    static readonly (string Name, float Lat, float Lon)[] Places =
    {
        (""Miami"", 25.76f, -80.19f), (""Houston"", 29.76f, -95.37f), (""London"", 51.51f, -0.13f),
        (""Madrid"", 40.42f, -3.70f), (""Tokyo"", 35.68f, 139.69f), (""Sydney"", -33.87f, 151.21f),
    };

    // ---- where we look --------------------------------------------------------------------
    int zoomLevel = 5;
    float lat = 25.76f, lon = -80.19f;           // the center of the view
    int ox, oy;                                  // the top-left tile of the 3 x 3 block
    bool haveBlock;

    // ---- data ---------------------------------------------------------------------------
    Request<JSONObject> mapsReq, windReq;
    Request<GeoMap> countriesReq;
    string host = """";
    readonly List<(long Time, string Path)> frames = new();       // newest last
    readonly Dictionary<string, Request<PImage>> tileCache = new(); // every tile ever asked for
    float[][] grids;                                               // per frame: intensity 0..1 per cell
    Color[][] tints;                                               // per frame: the radar's color per cell
    readonly Queue<(int Frame, int Tile)> pending = new();
    long lastRequest = -10000;
    bool[] land;
    int landDone;
    float windKmh, windFromDeg, temperature = float.NaN;
    float windLat = float.NaN, windLon = float.NaN;
    bool demo;

    // ---- view and time ------------------------------------------------------------------
    PFont font;
    readonly PointCloud ground = new(), clouds = new();
    float yaw = -0.35f, pitch = 0.75f;
    float clock;                                 // radar loop position, in frames
    float animT;                                 // the animation's own time: stops when paused
    bool paused;
    int shownFrame;

    public override void Setup()
    {
        Size(1000, 620);
        font = CreateFont(""Segoe UI"", 14);
        countriesReq = RequestGeoJSON(CountriesUrl, cacheFor: TimeSpan.FromDays(365));
        mapsReq = RequestJSONObject(""https://api.rainviewer.com/public/weather-maps.json"", cacheFor: TimeSpan.FromMinutes(5));
        ground.DepthSort = false;
        Retile();
    }

    // =====================================================================================
    // Map math: Web Mercator tiles, like every web map
    // =====================================================================================
    float TileX(float lo) => (lo + 180) / 360 * (1 << zoomLevel);
    float TileY(float la)
    {
        double r = Math.Clamp(la, -85, 85) * Math.PI / 180;
        return (float)((1 - Math.Log(Math.Tan(r) + 1 / Math.Cos(r)) / Math.PI) / 2 * (1 << zoomLevel));
    }
    float LonOf(float tx) => tx / (1 << zoomLevel) * 360 - 180;
    float LatOf(float ty) => (float)(Math.Atan(Math.Sinh(Math.PI * (1 - 2 * ty / (1 << zoomLevel)))) * 180 / Math.PI);

    /// <summary>How far the center is from the middle of the tile block, in cells (it moves smoothly as you drag).</summary>
    (float X, float Y) Offset() => ((TileX(lon) - (ox + 1.5f)) * 256 / Cell, (TileY(lat) - (oy + 1.5f)) * 256 / Cell);

    /// <summary>A cell of the field as world x (east) and z (north), with the view's center at the origin.</summary>
    (float X, float Z) CellPos(float cx, float cy, (float X, float Y) off) => (cx - G / 2f - off.X, G / 2f + off.Y - cy);

    /// <summary>0 outside the round window, 1 well inside it, a soft edge between.</summary>
    static float Fade(float x, float z)
    {
        float d = MathF.Sqrt(x * x + z * z);
        return Math.Clamp((Window - d) / 6, 0, 1);
    }

    /// <summary>When the center crosses into another tile, the 3 x 3 block moves with it.</summary>
    void Retile()
    {
        int nx = (int)Math.Floor(TileX(lon)) - 1, ny = (int)Math.Floor(TileY(lat)) - 1;
        if (haveBlock && nx == ox && ny == oy) return;
        haveBlock = true;
        ox = nx; oy = ny;
        grids = frames.Count > 0 ? new float[frames.Count][] : null;
        tints = frames.Count > 0 ? new Color[frames.Count][] : null;
        land = new bool[G * G];
        landDone = 0;
        ground.Clear();
        QueueTiles();
    }

    string TileUrl(int f, int t, out bool exists)
    {
        int n = 1 << zoomLevel;
        int tx = ((ox + t % Tiles) % n + n) % n, ty = oy + t / Tiles;   // wraps around the world east-west
        exists = ty >= 0 && ty < n;
        return $""{host}{frames[f].Path}/256/{zoomLevel}/{tx}/{ty}/2/1_0.png"";
    }

    void QueueTiles()
    {
        pending.Clear();
        for (int f = frames.Count - 1; f >= 0; f--)              // newest frame first
            for (int t = 0; t < Tiles * Tiles; t++)
                pending.Enqueue((f, t));
    }

    void Jump(float la, float lo, int z)
    {
        lat = la; lon = lo; zoomLevel = z;
        haveBlock = false;                                       // force a new block
        Retile();
        windLat = float.NaN;
    }

    // =====================================================================================
    // Loading, a little every frame
    // =====================================================================================
    void Load()
    {
        // The radar's frame list.
        if (frames.Count == 0 && mapsReq.Ready)
        {
            var m = mapsReq.Value;
            host = m.GetString(""host"");
            var past = m.GetJSONObject(""radar"")?.GetJSONArray(""past"");
            for (int i = 0; past != null && i < past.Size(); i++)
                frames.Add((past.GetJSONObject(i).GetInt(""time""), past.GetJSONObject(i).GetString(""path"")));
            clock = frames.Count - 1;
            haveBlock = false;
            Retile();
        }
        if (mapsReq.Failed && !demo) demo = true;

        // RainViewer allows 100 requests a minute: one every 0.65 s. Tiles seen before cost nothing.
        while (pending.Count > 0)
        {
            var (f, t) = pending.Peek();
            string url = TileUrl(f, t, out bool exists);
            if (!exists || tileCache.ContainsKey(url)) { pending.Dequeue(); continue; }
            if (Millis() - lastRequest < 650) break;
            pending.Dequeue();
            var req = RequestPImage(url, cacheFor: TimeSpan.FromHours(3));
            tileCache[url] = req;
            if (!req.FromCache) { lastRequest = Millis(); break; }
        }

        // A frame whose 9 tiles are in becomes a grid of intensities (a few per frame, so moving never stutters).
        int built = 0;
        for (int f = frames.Count - 1; grids != null && f >= 0 && built < 3; f--)
        {
            if (grids[f] != null) continue;
            bool all = true;
            for (int t = 0; t < Tiles * Tiles && all; t++)
            {
                string url = TileUrl(f, t, out bool exists);
                all = !exists || (tileCache.TryGetValue(url, out var r) && r.Done);
            }
            if (all) { Grid(f); built++; }
        }

        // Land or sea under each cell: a few rows a frame, once the countries are in.
        if (countriesReq.Ready && landDone < G)
        {
            var map = countriesReq.Value;
            for (int row = landDone; row < Math.Min(G, landDone + 6); row++)
                for (int c = 0; c < G; c++)
                {
                    float la = LatOf(oy + (row + 0.5f) * Cell / 256f), lo = LonOf(ox + (c + 0.5f) * Cell / 256f);
                    lo = ((lo + 540) % 360) - 180;
                    land[row * G + c] = map.FeatureAt(la, lo) != null;
                }
            landDone = Math.Min(G, landDone + 6);
        }

        // The wind where we look: asked again when the view has moved far and the mouse is up.
        bool moved = float.IsNaN(windLat) || Geo.DistanceKm(lat, lon, windLat, windLon) > 150 * (1 << (7 - zoomLevel));
        if (moved && !IsMousePressed)
        {
            windLat = MathF.Round(lat * 2) / 2; windLon = MathF.Round(lon * 2) / 2;   // rounded: nearby views share the cache
            string la = windLat.ToString(System.Globalization.CultureInfo.InvariantCulture), lo = windLon.ToString(System.Globalization.CultureInfo.InvariantCulture);
            windReq = RequestJSONObject($""https://api.open-meteo.com/v1/forecast?latitude={la}&longitude={lo}&current=wind_speed_10m,wind_direction_10m,temperature_2m&wind_speed_unit=kmh"", cacheFor: TimeSpan.FromMinutes(15));
            windRead = false;
        }
        if (windReq != null && windReq.Ready && !windRead)
        {
            windRead = true;
            var cur = windReq.Value.GetJSONObject(""current"");
            windKmh = cur?.GetFloat(""wind_speed_10m"") ?? 0;
            windFromDeg = cur?.GetFloat(""wind_direction_10m"") ?? 0;
            temperature = cur?.GetFloat(""temperature_2m"", float.NaN) ?? float.NaN;
        }
    }
    bool windRead;

    /// <summary>Reads a frame's 9 tiles: each cell is the strongest of a few of its pixels.</summary>
    void Grid(int f)
    {
        var grid = new float[G * G];
        var tint = new Color[G * G];
        for (int t = 0; t < Tiles * Tiles; t++)
        {
            string url = TileUrl(f, t, out bool exists);
            if (!exists || !tileCache[url].Ready) continue;
            var img = tileCache[url].Value;
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
        if (!paused)
        {
            animT += 1 / 60f;
            if (loaded > 1) clock += 1 / 40f;                    // ~1.5 s per radar frame
        }
        if (KeyPressedNow(LEFT)) Move(-4, 0);
        if (KeyPressedNow(RIGHT)) Move(4, 0);
        if (KeyPressedNow(UP)) Move(0, -4);
        if (KeyPressedNow(DOWN)) Move(0, 4);

        var off = Offset();
        var (grid, tint) = demo ? Demo() : Current();
        var view = View3D.Orbit(yaw, pitch, 88, new PVector(0, 4, 0), 50);

        Background(6, 10, 20);
        Ground(view, off);
        Coasts(view, off);
        if (grid != null) Rain(view, grid, tint, off);
        Hud(loaded);
    }

    bool KeyPressedNow(int code) => IsKeyPressed && KeyCode == code;

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

    static float Smooth(float x) => x * x * (3 - 2 * x);

    void Ground(View3D view, (float X, float Y) off)
    {
        ground.Clear();
        for (int r = 0; r < G; r++)
            for (int c = 0; c < G; c++)
            {
                if ((r + c) % 2 == 1) continue;                  // a checkerboard of dots is plenty
                var (x, z) = CellPos(c + 0.5f, r + 0.5f, off);
                float a = Fade(x, z);
                if (a <= 0) continue;
                bool isLand = r * G + c < landDone * G && land[r * G + c];
                var col = isLand ? Color(95, 150, 110, 255 * a) : Color(25, 45, 80, 200 * a);
                ground.Add(x, 0, z, col, isLand ? 0.5f : 0.28f);
            }
        Points(ground, view);
    }

    void Coasts(View3D view, (float X, float Y) off)
    {
        if (!countriesReq.Ready) return;
        StrokeWeight(1);
        int n = 1 << zoomLevel;
        float cx0 = TileX(lon);
        float left = LonOf(ox) - 2, right = LonOf(ox + Tiles) + 2;
        foreach (var f in countriesReq.Value.Features)
        {
            // Near the date line the block spans it: test the feature shifted by a turn too.
            if (f.MaxLat < LatOf(oy + Tiles) - 2 || f.MinLat > LatOf(oy) + 2) continue;
            bool near = (f.MaxLon >= left && f.MinLon <= right) || (f.MaxLon + 360 >= left && f.MinLon + 360 <= right) || (f.MaxLon - 360 >= left && f.MinLon - 360 <= right);
            if (!near) continue;
            foreach (var ring in f.Rings)
            {
                float px = 0, py = 0; bool prev = false;
                for (int i = 0; i < ring.Count; i++)
                {
                    float tx = TileX(ring.Lon[i]);
                    tx += MathF.Round((cx0 - tx) / n) * n;       // the copy of the world nearest the center
                    float cx = (tx - ox) * 256 / Cell, cy = (TileY(ring.Lat[i]) - oy) * 256 / Cell;
                    var (wx, wz) = CellPos(cx, cy, off);
                    float a = Fade(wx, wz);
                    var (sx, sy, d) = view.Project(new PVector(wx, 0.05f, wz), Width, Height);
                    bool ok = a > 0 && d > 0;
                    if (ok && prev)
                    {
                        Stroke(130, 185, 170, 170 * a);
                        Line(px, py, sx, sy);
                    }
                    px = sx; py = sy; prev = ok;
                }
            }
        }
    }

    void Rain(View3D view, float[] grid, Color[] tint, (float X, float Y) off)
    {
        // The wind: where it blows *to* (from + 180), as an east/north vector; stronger wind, more lean.
        float to = Radians(windFromDeg + 180);
        float lean = Math.Min(windKmh, 80) / 80f * 0.9f;
        float wx = Sin(to) * lean, wz = Cos(to) * lean;

        clouds.Clear();
        var light = new List<(float, float, float, float)>(6000);
        var heavy = new List<(float, float, float, float)>(2000);
        var splashes = new List<(float X, float Y, float R, float A)>();
        var bolts = new List<(float X, float Z, float Top, int Seed)>();

        for (int i = 0; i < G * G; i++)
        {
            float v = grid[i];
            if (v < 0.06f) continue;
            int r = i / G, c = i % G;
            var (x, z) = CellPos(c + 0.5f, r + 0.5f, off);
            float fade = Fade(x, z);
            if (fade <= 0) continue;
            var tc = tint != null && tint[i].A > 0 ? tint[i] : Color(120, 170, 255);

            // The cloud: a few soft puffs, higher and thicker where it rains harder.
            float top = 7 + v * 20;
            int puffs = 1 + (int)(v * 3);
            for (int k = 0; k < puffs; k++)
            {
                float h1 = Hash(i * 13 + k), h2 = Hash(i * 31 + k + 7);
                float shade = 0.55f + 0.45f * h2;
                clouds.Add(x + (h1 - 0.5f) * 0.9f, top + (h2 - 0.3f) * 3 * v, z + (h2 - 0.5f) * 0.9f,
                           Color(tc.R * 0.45f * shade + 120, tc.G * 0.45f * shade + 120, tc.B * 0.35f * shade + 140, (60 + 140 * v) * fade),
                           1.4f + 1.6f * v);
            }

            // The rain: streaks falling from the cloud, pushed by the wind.
            int drops = 1 + (int)(v * 3.5f);
            for (int k = 0; k < drops; k++)
            {
                float phase = Hash(i * 7 + k);
                float fall = (animT * (0.9f + v) / 1.2f + phase) % 1;   // 0 at the cloud, 1 at the ground
                float y = top * (1 - fall);
                float sx0 = x + wx * (top - y) + (phase - 0.5f) * 0.7f, sz0 = z + wz * (top - y);
                float len = 1.2f + 1.5f * v;
                var p = view.Project(new PVector(sx0, y, sz0), Width, Height);
                var q = view.Project(new PVector(sx0 - wx * len, Math.Min(top, y + len), sz0 - wz * len), Width, Height);
                if (p.Depth <= 0.5f || q.Depth <= 0.5f) continue;
                (v > 0.55f ? heavy : light).Add((p.X, p.Y, q.X, q.Y));
                if (v > 0.3f && fall > 0.9f)
                {
                    var g = view.Project(new PVector(x + wx * top + (phase - 0.5f) * 0.7f, 0.05f, z + wz * top), Width, Height);
                    if (g.Depth > 0.5f)
                        splashes.Add((g.X, g.Y, view.Scale(g.Depth, Height) * (fall - 0.9f) * 6, (1 - (fall - 0.9f) * 10) * fade));
                }
            }

            // Lightning where it storms: rare, short, tied to the animation's time.
            if (v > 0.8f)
            {
                int slot = (int)(animT * 3);
                if (Hash(i * 17 + slot * 101) < 0.004f) bolts.Add((x, z, top, i + slot));
            }
        }

        // Splashes: little rings on the ground.
        NoFill();
        StrokeWeight(1);
        foreach (var s in splashes)
        {
            Stroke(190, 220, 255, 200 * s.A);
            Ellipse(s.X, s.Y, s.R * 2, s.R * 2 * Math.Max(0.25f, Sin(pitch)));
        }

        // Rain streaks: two batches of lines.
        StrokeWeight(1);
        Stroke(160, 195, 255, 120);
        DrawLines(light);
        StrokeWeight(1.5f);
        Stroke(200, 225, 255, 190);
        DrawLines(heavy);

        // Lightning: a jagged line and a flash in the cloud.
        foreach (var (bx, bz, top, seed) in bolts)
        {
            StrokeWeight(2);
            Stroke(235, 235, 255, 230);
            float px = bx, pz = bz, py = top;
            var a = view.Project(new PVector(px, py, pz), Width, Height);
            while (py > 0)
            {
                py -= 2;
                px += (Hash(seed * 3 + (int)py) - 0.5f) * 1.2f;
                pz += (Hash(seed * 5 + (int)py) - 0.5f) * 1.2f;
                var b = view.Project(new PVector(px, Math.Max(0, py), pz), Width, Height);
                if (a.Depth > 0.5f && b.Depth > 0.5f) Line(a.X, a.Y, b.X, b.Y);
                a = b;
            }
            clouds.Add(bx, top, bz, Color(255, 255, 255, 200), 9);
        }

        Points(clouds, view);
    }

    void DrawLines(List<(float X0, float Y0, float X1, float Y1)> lines)
    {
        if (lines.Count == 0) return;
        BeginShape(ShapeKind.Lines);
        foreach (var (x0, y0, x1, y1) in lines) { Vertex(x0, y0); Vertex(x1, y1); }
        EndShape();
    }

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
        float t = animT * 0.15f;
        var off = Offset();
        for (int r = 0; r < G; r++)
            for (int c = 0; c < G; c++)
            {
                var (wx, wz) = CellPos(c + 0.5f, r + 0.5f, off);
                float x = wx / Window, y = -wz / Window;
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
        string where = ""the open sea"";
        if (countriesReq.Ready)
        {
            var f = countriesReq.Value.FeatureAt(lat, lon);
            if (f != null) where = f.Name;
        }
        float km = 2 * Window * Cell / 256f * 40075f * MathF.Cos(lat * MathF.PI / 180) / (1 << zoomLevel);
        TextFont(font, 22);
        TextAlign(TextAlignH.Left, TextAlignV.Top);
        Fill(235, 240, 255);
        Text(where, 20, 16);
        TextFont(font, 13);
        Fill(170, 185, 215);
        Text($""{Math.Abs(lat):0.0}°{(lat >= 0 ? ""N"" : ""S"")} {Math.Abs(lon):0.0}°{(lon >= 0 ? ""E"" : ""W"")}  ·  zoom {zoomLevel}  ·  ~{km:N0} km across"", 20, 46);
        string when = demo ? ""MADE-UP STORM (demo, not real data)""
            : frames.Count > 0 && grids?[shownFrame] != null ? $""radar {DateTimeOffset.FromUnixTimeSeconds(frames[shownFrame].Time).LocalDateTime:HH:mm}  ·  frame {shownFrame + 1}/{frames.Count}""
            : ""radar: "" + (mapsReq.Done ? $""loading tiles ({loaded}/{frames.Count} frames)"" : mapsReq.Status);
        Text(when, 20, 66);
        string wind = windReq == null ? """" : windReq.Ready
            ? $""wind {windKmh:0} km/h from {Compass(windFromDeg)}"" + (float.IsNaN(temperature) ? """" : $""  ·  {temperature:0} °C"")
            : ""wind: "" + windReq.Status;
        Text(wind, 20, 86);
        if (paused)
        {
            Fill(255, 210, 120);
            Text(""PAUSED"", 20, 106);
        }

        // A small compass: where the wind blows to, and north.
        float cx = Width - 60, cy = 60;
        NoFill(); Stroke(255, 255, 255, 60); StrokeWeight(1);
        Circle(cx, cy, 70);
        float north = -Degrees(yaw);
        NoStroke(); Fill(255, 255, 255, 120);
        TextAlign(TextAlignH.Center, TextAlignV.Center);
        Text(""N"", cx + Sin(Radians(north)) * 44, cy - Cos(Radians(north)) * 44);
        float to = Radians(windFromDeg + 180 + north);
        Stroke(170, 205, 255); StrokeWeight(3);
        Line(cx, cy, cx + Sin(to) * 30, cy - Cos(to) * 30);
        NoStroke(); Fill(255, 255, 255, 120);
        Text(""wind"", cx, cy + 48);

        TextAlign(TextAlignH.Left, TextAlignV.Bottom);
        Fill(255, 255, 255, 90);
        Text(""drag: move · right-drag/Q E R F: turn · wheel/+ -: zoom · arrows: move · 1-6: cities · Space: pause · , .: frames · M: made-up storm"", 20, Height - 14);
        Fill(255, 255, 255, 60);
        Text(""Radar: RainViewer · Wind: Open-Meteo · Coasts: Natural Earth"", 20, Height - 32);
    }

    static string Compass(float deg) => new[] { ""N"", ""NE"", ""E"", ""SE"", ""S"", ""SW"", ""W"", ""NW"" }[(int)Math.Round(((deg % 360) + 360) % 360 / 45) % 8];

    // =====================================================================================
    // Input
    // =====================================================================================

    /// <summary>Moves the center by a screen distance: the ground under the mouse follows the mouse.</summary>
    void Move(float dxScreen, float dyScreen)
    {
        // How the screen moves for one cell east and one cell north, near the center: a 2x2 matrix to invert.
        var view = View3D.Orbit(yaw, pitch, 88, new PVector(0, 4, 0), 50);
        var o = view.Project(new PVector(0, 0, 0), Width, Height);
        var e = view.Project(new PVector(1, 0, 0), Width, Height);
        var n = view.Project(new PVector(0, 0, 1), Width, Height);
        float a = e.X - o.X, b = n.X - o.X, c = e.Y - o.Y, d = n.Y - o.Y;
        float det = a * d - b * c;
        if (Math.Abs(det) < 1e-4f) return;
        float east = (d * dxScreen - b * dyScreen) / det, northCells = (-c * dxScreen + a * dyScreen) / det;
        float tx = TileX(lon) - east * Cell / 256f, ty = TileY(lat) + northCells * Cell / 256f;
        int nTiles = 1 << zoomLevel;
        ty = Math.Clamp(ty, 0.5f, nTiles - 0.5f);
        lon = ((LonOf(tx) + 540) % 360) - 180;
        lat = LatOf(ty);
        Retile();
    }

    public override void MouseDragged()
    {
        float dx = MouseX - PMouseX, dy = MouseY - PMouseY;
        if (MouseButton == MouseButtonKind.Right)
        {
            yaw -= dx * 0.008f;
            pitch = Constrain(pitch - dy * 0.008f, 0.2f, 1.5f);
        }
        else Move(dx, dy);
    }

    public override void MouseWheel(float delta) => SetZoom(zoomLevel + (delta > 0 ? -1 : 1));

    void SetZoom(int z)
    {
        z = Math.Clamp(z, MinZoom, MaxZoom);
        if (z == zoomLevel) return;
        Jump(lat, lon, z);
    }

    public override void KeyPressed()
    {
        if (Key >= '1' && Key <= '6') { var p = Places[Key - '1']; Jump(p.Lat, p.Lon, 6); }
        else if (Key == ' ') paused = !paused;
        else if (Key == 'm' || Key == 'M') demo = !demo;
        else if (Key == '+' || Key == '=') SetZoom(zoomLevel + 1);
        else if (Key == '-' || Key == '_') SetZoom(zoomLevel - 1);
        else if (Key == '.') clock = (float)Math.Floor(clock) + 1;
        else if (Key == ',') clock = (float)Math.Floor(clock) - 1;
        else if (Key == 'q' || Key == 'Q') yaw += 0.15f;
        else if (Key == 'e' || Key == 'E') yaw -= 0.15f;
        else if (Key == 'r' || Key == 'R') pitch = Constrain(pitch + 0.1f, 0.2f, 1.5f);
        else if (Key == 'f' || Key == 'F') pitch = Constrain(pitch - 0.1f, 0.2f, 1.5f);
    }
}
";

        private const string TerrainAnywhere =
@"// The relief of any country on Earth, as a lit 3D model -- or as contour lines you can
// send to a pen plotter.
//
// Pick a country on the world map. The sketch lays a grid of about 2,000 points over it
// (closer together for small countries, farther apart for big ones), keeps the ones that
// fall inside its outline, and asks Open Topo Data (https://www.opentopodata.org, free and
// keyless) for their heights: 100 points per call, one call a second, each call cached on
// disk for a month. The country appears flat at once, with its real shape, and rises piece
// by piece as the heights arrive.
//
// - T: the terrain, a mesh colored by altitude and lit from the north-west.
// - C: contour lines every N meters (thicker every 5th), computed with marching squares
//   and drawn at their height.
// - S: save the contours as a plotter-ready SVG, seen from above: BeginRaw(path, plotter:
//   true) puts each pen color on its own layer and orders the strokes so the pen barely
//   travels in the air.
//
// Click a country to fly there.  M (or the little map): back to the world.  R: a random
// country.  Drag: turn.  Wheel: zoom.  Up/Down: heights.
public class TerrainAnywhere : Sketch
{
    // ---- settings -----------------------------------------------------------------------
    const string Dataset = ""mapzen"";             // global, with the sea floor; also srtm90m, aster30m, etopo1...
    const int Target = 2000;                     // about this many heights per country -> ~20-25 calls
    const float W = 60;                          // the country's longest side, in world units
    const string CountriesUrl = ""https://raw.githubusercontent.com/nvkelso/natural-earth-vector/master/geojson/ne_50m_admin_0_countries.geojson"";

    // ---- the world map ------------------------------------------------------------------
    Request<GeoMap> countriesReq;
    PGraphics worldMap;
    float mapY = 70;                             // where the big map starts
    GeoFeature hover, country;
    bool picking = true;

    // ---- the chosen country -------------------------------------------------------------
    readonly List<(float[] Lat, float[] Lon)> outline = new();    // its rings, longitudes unwrapped near the date line
    readonly List<bool> holes = new();
    float lat0, lon0, minLat, maxLat, minLon, maxLon;
    float kmX, kmZ;                              // km per degree of longitude / latitude here
    float scale;                                 // world units per km
    int nx, ny;                                  // grid size
    float dLat, dLon;                            // grid step in degrees
    bool[] inside, wanted;                       // in the country / worth asking (inside, or right next to it)
    int[] asked;                                 // grid indices, in the order they're asked for
    float[] height;                              // meters; NaN until it arrives
    long[] arrivedAt;
    Request<JSONObject>[] calls;
    bool[] read;
    int nextCall, arrived, failed;
    long lastCall = -10000;
    float lo, hi;
    bool made;                                   // made-up terrain (offline)

    // ---- view ---------------------------------------------------------------------------
    PFont font;
    bool contours;
    float yaw = -0.5f, pitch = 0.75f, zoom = 1, heightMul = 1, shownEx = -1;
    string saved = """";
    long savedAt;
    float pressX, pressY;

    public override void Setup()
    {
        Size(1000, 640);
        font = CreateFont(""Segoe UI"", 14);
        countriesReq = RequestGeoJSON(CountriesUrl, cacheFor: TimeSpan.FromDays(365));
    }

    // =====================================================================================
    // Choosing a country
    // =====================================================================================

    /// <summary>
    /// Keeps the part of the country around the click (or its biggest piece) and the islands
    /// near it -- so France is France, not France plus French Guiana -- and lays the grid.
    /// </summary>
    void Choose(GeoFeature f, float clickLat, float clickLon)
    {
        country = f;
        picking = false;
        outline.Clear(); holes.Clear();

        // The main piece: the ring under the click, or the biggest.
        GeoRing main = null; float best = -1;
        foreach (var ring in f.Rings)
        {
            if (ring.IsHole) continue;
            if (InRing(ring.Lat, ring.Lon, clickLat, clickLon)) { main = ring; break; }
            float area = Area(ring);
            if (area > best) { best = area; main = ring; }
        }
        if (main == null) return;
        var (mLat0, mLat1, mLon0, mLon1) = Box(main.Lat, main.Lon);
        float mainLon = (mLon0 + mLon1) / 2;

        // Other pieces close to it come along (shifted a whole turn if they're across the date line).
        float reach = Math.Max(1.5f, 0.4f * Math.Max(mLat1 - mLat0, mLon1 - mLon0));
        foreach (var ring in f.Rings)
        {
            if (ring.IsHole) continue;
            var (a0, a1, o0, o1) = Box(ring.Lat, ring.Lon);
            float shift = 360 * MathF.Round((mainLon - (o0 + o1) / 2) / 360);
            if (a1 < mLat0 - reach || a0 > mLat1 + reach || o1 + shift < mLon0 - reach || o0 + shift > mLon1 + reach) continue;
            var lon = (float[])ring.Lon.Clone();
            for (int i = 0; i < lon.Length; i++) lon[i] += shift;
            outline.Add((ring.Lat, lon)); holes.Add(false);
        }
        // Holes (lakes, enclaves) that fall inside what we kept.
        foreach (var ring in f.Rings)
            if (ring.IsHole && In(ring.Lat[0], ring.Lon[0]))
            {
                outline.Add((ring.Lat, ring.Lon)); holes.Add(true);
            }

        minLat = minLon = float.MaxValue; maxLat = maxLon = float.MinValue;
        for (int k = 0; k < outline.Count; k++)
        {
            if (holes[k]) continue;
            var (a0, a1, o0, o1) = Box(outline[k].Lat, outline[k].Lon);
            minLat = Math.Min(minLat, a0); maxLat = Math.Max(maxLat, a1);
            minLon = Math.Min(minLon, o0); maxLon = Math.Max(maxLon, o1);
        }
        lat0 = (minLat + maxLat) / 2; lon0 = (minLon + maxLon) / 2;
        kmX = 111.32f * MathF.Cos(lat0 * MathF.PI / 180); kmZ = 110.57f;
        float wKm = (maxLon - minLon) * kmX, hKm = (maxLat - minLat) * kmZ;
        scale = W / Math.Max(wKm, hKm);

        // How much of the box is the country? Then the spacing that gives ~Target points inside it.
        int hits = 0;
        for (int i = 0; i < 40; i++)
            for (int j = 0; j < 40; j++)
                if (In(minLat + (maxLat - minLat) * (i + 0.5f) / 40, minLon + (maxLon - minLon) * (j + 0.5f) / 40)) hits++;
        float fraction = Math.Max(hits / 1600f, 0.03f);
        float spacing = Math.Max(0.3f, MathF.Sqrt(fraction * wKm * hKm / Target));
        nx = Math.Clamp((int)MathF.Ceiling(wKm / spacing) + 1, 3, 300);
        ny = Math.Clamp((int)MathF.Ceiling(hKm / spacing) + 1, 3, 300);
        dLon = (maxLon - minLon) / (nx - 1); dLat = (maxLat - minLat) / (ny - 1);

        inside = new bool[nx * ny];
        for (int r = 0; r < ny; r++)
            for (int c = 0; c < nx; c++)
                inside[r * nx + c] = In(minLat + r * dLat, minLon + c * dLon);
        // Also ask for the points just outside the border, so the mesh reaches the coast.
        wanted = new bool[nx * ny];
        var list = new List<int>();
        for (int r = 0; r < ny; r++)
            for (int c = 0; c < nx; c++)
            {
                bool w = false;
                for (int dr = -1; dr <= 1 && !w; dr++)
                    for (int dc = -1; dc <= 1 && !w; dc++)
                    {
                        int rr = r + dr, cc = c + dc;
                        w = rr >= 0 && cc >= 0 && rr < ny && cc < nx && inside[rr * nx + cc];
                    }
                wanted[r * nx + c] = w;
                if (w) list.Add(r * nx + c);
            }
        asked = list.ToArray();
        height = new float[nx * ny];
        Array.Fill(height, float.NaN);
        arrivedAt = new long[nx * ny];
        calls = new Request<JSONObject>[(asked.Length + 99) / 100];
        read = new bool[calls.Length];
        nextCall = 0; arrived = 0; failed = 0; made = false;
        lo = 0; hi = 0;
        segments = null;
        shownEx = -1;
    }

    static float Area(GeoRing ring)
    {
        float a = 0;
        for (int i = 0, j = ring.Count - 1; i < ring.Count; j = i++)
            a += (ring.Lon[j] - ring.Lon[i]) * (ring.Lat[j] + ring.Lat[i]);
        return Math.Abs(a) * MathF.Cos(ring.Lat[0] * MathF.PI / 180);
    }

    static (float, float, float, float) Box(float[] la, float[] lo)
    {
        float a0 = float.MaxValue, a1 = float.MinValue, o0 = float.MaxValue, o1 = float.MinValue;
        for (int i = 0; i < la.Length; i++)
        {
            a0 = Math.Min(a0, la[i]); a1 = Math.Max(a1, la[i]);
            o0 = Math.Min(o0, lo[i]); o1 = Math.Max(o1, lo[i]);
        }
        return (a0, a1, o0, o1);
    }

    static bool InRing(float[] la, float[] lo, float lat, float lon)
    {
        bool inside = false;
        for (int i = 0, j = la.Length - 1; i < la.Length; j = i++)
            if ((la[i] > lat) != (la[j] > lat) && lon < (lo[j] - lo[i]) * (lat - la[i]) / (la[j] - la[i]) + lo[i])
                inside = !inside;
        return inside;
    }

    /// <summary>Inside the kept part of the country (even-odd over its rings, so holes stay out).</summary>
    bool In(float lat, float lon)
    {
        bool inside = false;
        foreach (var (la, lo) in outline)
            if (InRing(la, lo, lat, lon)) inside = !inside;
        return inside;
    }

    // =====================================================================================
    // Loading: one call a second, 100 points each
    // =====================================================================================
    string CallUrl(string locations) => $""https://api.opentopodata.org/v1/{Dataset}?locations={locations}"";

    void Load()
    {
        if (country == null || made) return;
        if (nextCall < calls.Length && Millis() - lastCall > 1100)
        {
            var points = new System.Text.StringBuilder();
            for (int k = nextCall * 100; k < Math.Min(asked.Length, nextCall * 100 + 100); k++)
            {
                int i = asked[k];
                float la = minLat + i / nx * dLat, lo2 = minLon + i % nx * dLon;
                lo2 = ((lo2 + 540) % 360) - 180;                 // back to -180..180 for the API
                if (points.Length > 0) points.Append(""%7C"");     // ""|"", escaped
                points.Append(la.ToString(""0.####"", System.Globalization.CultureInfo.InvariantCulture)).Append(',')
                      .Append(lo2.ToString(""0.####"", System.Globalization.CultureInfo.InvariantCulture));
            }
            calls[nextCall] = RequestJSONObject(CallUrl(points.ToString()), cacheFor: TimeSpan.FromDays(30));
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
            for (int j = 0; results != null && j < results.Size() && k * 100 + j < asked.Length; j++)
            {
                // ""elevation"" is null where the dataset has no data: count it as sea level.
                float e = results.GetJSONObject(j).GetFloat(""elevation"", 0);
                int i = asked[k * 100 + j];
                height[i] = e;
                arrivedAt[i] = Millis();
                if (inside[i]) { lo = Math.Min(lo, e); hi = Math.Max(hi, e); }
                arrived++;
            }
        }
        // Offline: after two failed calls, a made-up terrain, clearly labelled.
        if (failed >= 2)
        {
            made = true;
            foreach (int i in asked)
            {
                float x = i % nx / (float)nx, y = i / nx / (float)ny;
                height[i] = 2500 * MathF.Pow(Noise(x * 3, y * 3), 2) + 300 * Noise(x * 14, y * 14);
                arrivedAt[i] = Millis();
                hi = Math.Max(hi, height[i]);
            }
            arrived = asked.Length;
        }
    }

    // =====================================================================================
    // Geometry
    // =====================================================================================

    /// <summary>The height to draw (m): rising from 0 for a second after it arrives; NaN if not asked.</summary>
    float Shown(int i)
    {
        if (!wanted[i]) return float.NaN;
        float h = height[i];
        if (float.IsNaN(h)) return 0;
        float t = Math.Clamp((Millis() - arrivedAt[i]) / 900f, 0, 1);
        h = inside[i] ? h : Math.Max(h, 0);                      // just outside: no sea floor
        return h * (1 - (1 - t) * (1 - t) * (1 - t));
    }

    /// <summary>The automatic exaggeration: the highest peak stands at ~10% of the width (flat countries get more).</summary>
    float Exaggeration()
    {
        float auto = Math.Clamp(0.1f * W / (Math.Max(hi, 300) / 1000 * scale), 1, 120);
        float target = auto * heightMul;
        shownEx = shownEx < 0 ? target : Lerp(shownEx, target, 0.08f);
        return shownEx;
    }

    PVector World(float c, float r, float meters, float ex) =>
        new PVector((minLon + c * dLon - lon0) * kmX * scale, meters / 1000 * scale * ex, (minLat + r * dLat - lat0) * kmZ * scale);

    PVector WorldLatLon(float lat, float lon, float meters, float ex) =>
        new PVector((lon - lon0) * kmX * scale, meters / 1000 * scale * ex, (lat - lat0) * kmZ * scale);

    /// <summary>The drawn height (m) at a fractional grid position, bilinear.</summary>
    float At(float gx, float gy, float[] shown)
    {
        int x0 = Math.Clamp((int)gx, 0, nx - 2), y0 = Math.Clamp((int)gy, 0, ny - 2);
        float fx = Math.Clamp(gx - x0, 0, 1), fy = Math.Clamp(gy - y0, 0, 1);
        float H(int c, int r) { float v = shown[r * nx + c]; return float.IsNaN(v) ? 0 : v; }
        return Lerp(Lerp(H(x0, y0), H(x0 + 1, y0), fx), Lerp(H(x0, y0 + 1), H(x0 + 1, y0 + 1), fx), fy);
    }

    // =====================================================================================
    // Each frame
    // =====================================================================================
    public override void Draw()
    {
        Background(8, 12, 20);
        if (!countriesReq.Ready)
        {
            Loading();
            return;
        }
        if (worldMap == null) BuildWorldMap();
        if (picking) { DrawPicker(); return; }

        Load();
        float ex = Exaggeration();
        var view = View3D.Orbit(yaw, pitch, 72 / zoom, new PVector(0, 2, 0), 45);
        var shown = new float[nx * ny];
        for (int i = 0; i < shown.Length; i++) shown[i] = Shown(i);

        DrawOutline(view, shown, ex, ground: true);
        if (contours) DrawContours(view, shown, ex);
        else DrawMesh(view, shown, ex);
        DrawOutline(view, shown, ex, ground: false);
        DrawPeak(view, shown, ex);
        Hud(ex);
        MiniMap();
    }

    /// <summary>The terrain as triangles, far ones first, each corner colored by height and lit by its slope.</summary>
    void DrawMesh(View3D view, float[] shown, float ex)
    {
        var sx = new float[nx * ny]; var sy = new float[nx * ny]; var depth = new float[nx * ny];
        var col = new Color[nx * ny];
        float stepX = dLon * kmX * scale, stepZ = dLat * kmZ * scale;
        float hScale = scale * ex / 1000;
        var light = new PVector(-0.55f, 0.75f, 0.45f);            // from the north-west, above
        light.Normalize();
        float top = Math.Max(hi, 400);
        for (int i = 0; i < nx * ny; i++)
        {
            if (!wanted[i]) continue;
            int c = i % nx, r = i / nx;
            var (x, y, d) = view.Project(World(c, r, shown[i], ex), Width, Height);
            sx[i] = x; sy[i] = y; depth[i] = d;
            // The slope from the neighbors, in world units -> a normal -> how much light it catches.
            float hl = Neighbor(shown, c - 1, r, shown[i]), hr = Neighbor(shown, c + 1, r, shown[i]);
            float hd = Neighbor(shown, c, r - 1, shown[i]), hu = Neighbor(shown, c, r + 1, shown[i]);
            var n = new PVector(-(hr - hl) * hScale / (2 * stepX), 1, -(hu - hd) * hScale / (2 * stepZ));
            n.Normalize();
            float lit = 0.35f + 0.8f * Math.Max(0, n.X * light.X + n.Y * light.Y + n.Z * light.Z);
            col[i] = float.IsNaN(height[i]) ? Color(38, 52, 72) : Shade(Hypsometric(shown[i], top), lit, inside[i] ? 255 : 150);
        }

        var quads = new List<(float D, int I)>(nx * ny);
        for (int r = 0; r < ny - 1; r++)
            for (int c = 0; c < nx - 1; c++)
            {
                int a = r * nx + c, b = a + 1, e = a + nx, f = e + 1;
                if (!wanted[a] || !wanted[b] || !wanted[e] || !wanted[f]) continue;
                if (!inside[a] && !inside[b] && !inside[e] && !inside[f]) continue;
                if (depth[a] <= 0.5f || depth[b] <= 0.5f || depth[e] <= 0.5f || depth[f] <= 0.5f) continue;
                quads.Add((depth[a] + depth[b] + depth[e] + depth[f], a));
            }
        quads.Sort((p, q) => q.D.CompareTo(p.D));                // far first: near triangles paint over

        NoStroke();
        BeginShape(ShapeKind.Triangles);
        foreach (var (_, a) in quads)
        {
            int b = a + 1, e = a + nx, f = e + 1;
            V(a); V(b); V(f);
            V(a); V(f); V(e);
        }
        EndShape();

        void V(int i) { Fill(col[i]); Vertex(sx[i], sy[i]); }
    }

    float Neighbor(float[] shown, int c, int r, float fallback)
    {
        if (c < 0 || r < 0 || c >= nx || r >= ny) return fallback;
        float v = shown[r * nx + c];
        return float.IsNaN(v) ? fallback : v;
    }

    /// <summary>Altitude colors, like a school atlas: green lowlands, yellow and brown hills, grey rock, snow.</summary>
    Color Hypsometric(float h, float top)
    {
        float k = Math.Clamp(top / 4500, 0.25f, 1);              // a low country still gets the whole range
        (float H, int R, int G, int B)[] stops =
        {
            (0, 60, 130, 85), (250, 105, 160, 90), (700, 190, 185, 110), (1400, 175, 130, 80),
            (2300, 140, 110, 95), (3300, 205, 205, 210), (4500, 250, 250, 255),
        };
        for (int s = 1; s < stops.Length; s++)
        {
            float h1 = stops[s].H * k;
            if (h <= h1 || s == stops.Length - 1)
            {
                float h0 = stops[s - 1].H * k;
                float t = Math.Clamp((h - h0) / Math.Max(1, h1 - h0), 0, 1);
                return Color(Lerp(stops[s - 1].R, stops[s].R, t), Lerp(stops[s - 1].G, stops[s].G, t), Lerp(stops[s - 1].B, stops[s].B, t));
            }
        }
        return Color(255, 255, 255);
    }

    Color Shade(Color c, float lit, float alpha) =>
        Color(Math.Min(255, c.R * lit), Math.Min(255, c.G * lit), Math.Min(255, c.B * lit), alpha);

    /// <summary>The border: on the ground as a faint shadow, and on the surface as a bright line.</summary>
    void DrawOutline(View3D view, float[] shown, float ex, bool ground)
    {
        var segs = new List<(float, float, float, float)>();
        for (int k = 0; k < outline.Count; k++)
        {
            var (la, lo) = outline[k];
            int step = Math.Max(1, la.Length / 1500);
            (float X, float Y, float D) prev = default;
            for (int i = 0; i <= la.Length; i += step)
            {
                int j = Math.Min(i, la.Length - 1);
                float m = ground ? 0 : At((lo[j] - minLon) / dLon, (la[j] - minLat) / dLat, shown) + 15;
                var p = view.Project(WorldLatLon(la[j], lo[j], m, ex), Width, Height);
                if (i > 0 && p.Depth > 0.5f && prev.D > 0.5f) segs.Add((prev.X, prev.Y, p.X, p.Y));
                prev = p;
            }
        }
        StrokeWeight(ground ? 1 : 1.2f);
        if (ground) Stroke(120, 150, 190, 60); else Stroke(255, 255, 255, contours ? 120 : 150);
        BeginShape(ShapeKind.Lines);
        foreach (var (x0, y0, x1, y1) in segs) { Vertex(x0, y0); Vertex(x1, y1); }
        EndShape();
    }

    /// <summary>The highest point: a marker and its height.</summary>
    void DrawPeak(View3D view, float[] shown, float ex)
    {
        int best = -1;
        for (int i = 0; i < nx * ny; i++)
            if (inside[i] && !float.IsNaN(height[i]) && (best < 0 || height[i] > height[best])) best = i;
        if (best < 0 || height[best] <= 0) return;
        var (sx, sy, d) = view.Project(World(best % nx, best / nx, shown[best], ex), Width, Height);
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
    // Contours
    // =====================================================================================

    /// <summary>Contour spacing: a round number giving ~16 lines over the range (10, 20, 50, 100, 200 m...).</summary>
    float Interval()
    {
        float raw = Math.Max(1, hi) / 16;
        float p = MathF.Pow(10, MathF.Floor(MathF.Log10(raw)));
        return raw / p < 1.5f ? p : raw / p < 3.5f ? 2 * p : raw / p < 7.5f ? 5 * p : 10 * p;
    }

    /// <summary>
    /// Marching squares: for each level, every grid square whose corners straddle it gets a
    /// segment between the edge crossings. Calls <paramref name=""segment""/>(level index,
    /// height, start, end) with grid coordinates. Only squares inside the country count.
    /// </summary>
    void MarchingSquares(Action<int, float, (float X, float Y), (float X, float Y)> segment)
    {
        float interval = Interval();
        float H(int c, int r) { int i = r * nx + c; return wanted[i] ? height[i] : float.NaN; }
        int last = (int)MathF.Floor(hi / interval);
        for (int li = 1; li <= last; li++)
        {
            float level = li * interval;
            for (int r = 0; r < ny - 1; r++)
                for (int c = 0; c < nx - 1; c++)
                {
                    int i = r * nx + c;
                    if (!inside[i] && !inside[i + 1] && !inside[i + nx] && !inside[i + nx + 1]) continue;
                    float a = H(c, r), b = H(c + 1, r), d = H(c, r + 1), e = H(c + 1, r + 1);
                    if (float.IsNaN(a) || float.IsNaN(b) || float.IsNaN(d) || float.IsNaN(e)) continue;
                    int code = (a > level ? 8 : 0) | (b > level ? 4 : 0) | (e > level ? 2 : 0) | (d > level ? 1 : 0);
                    if (code == 0 || code == 15) continue;
                    // Where the level crosses each edge: a-b, b-e, d-e, a-d.
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

    void DrawContours(View3D view, float[] shown, float ex)
    {
        float top = Math.Max(hi, 1);
        foreach (var (li, level, p, q) in Segments())
        {
            float rise = Math.Min(Shown(Nearest(p)) / Math.Max(1, height[Nearest(p)]), 1);   // contours rise with the land
            if (float.IsNaN(rise) || rise < 0) rise = 1;
            var (x0, y0, d0) = view.Project(World(p.X, p.Y, level * rise, ex), Width, Height);
            var (x1, y1, d1) = view.Project(World(q.X, q.Y, level * rise, ex), Width, Height);
            if (d0 <= 0 || d1 <= 0) continue;
            float t = level / top;
            bool index = li % 5 == 0;
            Stroke(Lerp(90, 255, t), Lerp(200, 220, t), Lerp(160, 140, t), index ? 255 : 170);
            StrokeWeight(index ? 2 : 1);
            Line(x0, y0, x1, y1);
        }
    }

    int Nearest((float X, float Y) p) => Math.Clamp((int)MathF.Round(p.Y), 0, ny - 1) * nx + Math.Clamp((int)MathF.Round(p.X), 0, nx - 1);

    // =====================================================================================
    // The plotter SVG: the contours and the border seen from straight above, two pens
    // =====================================================================================
    void SaveSvg()
    {
        string name = string.Concat(country.Name.Where(char.IsLetterOrDigit)).ToLowerInvariant();
        string file = $""contours-{name}.svg"";
        float margin = 50;
        float wKm = (maxLon - minLon) * kmX, hKm = (maxLat - minLat) * kmZ;
        float s = Math.Min((Width - 2 * margin) / wKm, (Height - 2 * margin) / hKm);   // pixels per km, north up
        float left = (Width - wKm * s) / 2, topY = (Height - hKm * s) / 2;
        float PX(float lon) => left + (lon - minLon) * kmX * s;
        float PY(float lat) => topY + (maxLat - lat) * kmZ * s;

        BeginRaw(file, plotter: true);
        Background(255);
        NoFill();
        foreach (var (li, level, p, q) in Segments())
        {
            bool index = li % 5 == 0;
            Stroke(index ? Color(120, 60, 20) : Color(0, 0, 0));    // two pens: brown index lines, black the rest
            StrokeWeight(index ? 1.2f : 0.6f);
            Line(PX(minLon + p.X * dLon), PY(minLat + p.Y * dLat), PX(minLon + q.X * dLon), PY(minLat + q.Y * dLat));
        }
        Stroke(0, 0, 0);
        StrokeWeight(1);
        for (int k = 0; k < outline.Count; k++)
        {
            var (la, lo) = outline[k];
            for (int i = 1; i < la.Length; i++) Line(PX(lo[i - 1]), PY(la[i - 1]), PX(lo[i]), PY(la[i]));
        }
        Fill(0);
        TextFont(font, 14);
        TextAlign(TextAlignH.Left, TextAlignV.Top);
        Text($""{country.Name}  ·  contours every {Interval():N0} m"", margin, 16);
        EndRaw();
        saved = file;
        savedAt = Millis();
    }

    // =====================================================================================
    // The world map: pick a country
    // =====================================================================================
    void BuildWorldMap()
    {
        worldMap = CreateGraphics(Width, Width / 2);
        worldMap.BeginDraw();
        worldMap.Background(10, 16, 28);
        worldMap.Stroke(255, 255, 255, 18);
        for (int lo = -180; lo <= 180; lo += 30) worldMap.Line((lo + 180) / 360f * Width, 0, (lo + 180) / 360f * Width, Width / 2);
        for (int la = -60; la <= 60; la += 30) worldMap.Line(0, (90 - la) / 180f * Width / 2, Width, (90 - la) / 180f * Width / 2);
        worldMap.Fill(48, 70, 92);
        worldMap.Stroke(110, 140, 170);
        worldMap.StrokeWeight(0.5f);
        foreach (var f in countriesReq.Value.Features) Shape(worldMap, f, Width, Width / 2, 0, 0);
        worldMap.EndDraw();
    }

    /// <summary>A country's outlines, filled, on an equirectangular map of size w x h at (x, y).</summary>
    static void Shape(GraphicsContext g, GeoFeature f, float w, float h, float x, float y)
    {
        foreach (var ring in f.Rings)
        {
            if (ring.IsHole) continue;
            g.BeginShape();
            for (int i = 0; i < ring.Count; i++)
            {
                var (px, py) = Geo.Equirectangular(ring.Lat[i], ring.Lon[i], w, h);
                g.Vertex(x + px, y + py);
            }
            g.EndShape(true);
        }
    }

    void DrawPicker()
    {
        Image(worldMap, 0, mapY);
        hover = MouseY > mapY && MouseY < mapY + Width / 2 ? FeatureUnder(MouseX, MouseY - mapY, Width) : null;
        if (hover != null)
        {
            Fill(255, 200, 90);
            Stroke(255, 240, 200);
            StrokeWeight(1);
            Shape(this, hover, Width, Width / 2, 0, mapY);
        }
        TextFont(font, 22);
        TextAlign(TextAlignH.Left, TextAlignV.Top);
        Fill(235, 240, 255);
        Text(hover?.Name ?? ""Pick a country"", 20, 16);
        TextFont(font, 13);
        Fill(170, 185, 215);
        Text(hover != null ? ""click to see its relief"" : ""click any country to see its relief · R: a random one"", 20, 46);
        TextAlign(TextAlignH.Left, TextAlignV.Bottom);
        Fill(255, 255, 255, 60);
        Text($""Borders: Natural Earth · Elevation: Open Topo Data ({Dataset}) · 1 call/s, 1,000 calls a day: each country is cached for a month"", 20, Height - 14);
    }

    GeoFeature FeatureUnder(float x, float y, float w)
    {
        var (la, lo) = Geo.FromEquirectangular(x, y, w, w / 2);
        return countriesReq.Value.FeatureAt(la, lo);
    }

    /// <summary>The world, small, in a corner: the chosen country lit. Click it to go back.</summary>
    void MiniMap()
    {
        float mw = 200, mh = 100, mx = Width - mw - 16, my = 16;
        Tint(255, 255, 255, 200);
        Image(worldMap, mx, my, mw, mh);
        NoTint();
        Fill(255, 200, 90);
        NoStroke();
        Shape(this, country, mw, mh, mx, my);
        NoFill();
        Stroke(255, 255, 255, 60);
        Rect(mx, my, mw, mh, 4);
        Fill(255, 255, 255, 110);
        TextFont(font, 11);
        TextAlign(TextAlignH.Right, TextAlignV.Top);
        Text(""M: world map"", mx + mw, my + mh + 4);
    }

    void Loading()
    {
        TextFont(font, 16);
        TextAlign(TextAlignH.Center, TextAlignV.Center);
        Fill(170, 185, 215);
        Text(""countries: "" + countriesReq.Status, Width / 2, Height / 2);
    }

    // =====================================================================================
    // Text on screen
    // =====================================================================================
    void Hud(float ex)
    {
        float wKm = (maxLon - minLon) * kmX, hKm = (maxLat - minLat) * kmZ;
        float spacing = dLon * kmX;
        TextFont(font, 22);
        TextAlign(TextAlignH.Left, TextAlignV.Top);
        Fill(235, 240, 255);
        Text(country.Name, 20, 16);
        TextFont(font, 13);
        Fill(170, 185, 215);
        Text($""~{wKm:N0} x {hKm:N0} km  ·  a height every {spacing:0.#} km  ·  heights x{ex:0.#}"", 20, 46);
        if (arrived > 0)
            Text($""up to {hi:N0} m{(contours ? $""  ·  contours every {Interval():N0} m"" : """")}"", 20, 66);
        if (made)
        {
            Fill(255, 170, 110);
            Text(""no connection: MADE-UP terrain (not real data)"", 20, 86);
        }
        else if (arrived < asked.Length)
        {
            // Loading: the points that came in, and the call that's out.
            var call = calls[Math.Max(0, nextCall - 1)];
            Fill(255, 255, 255, 30);
            Rect(20, 92, 260, 5, 2);
            Fill(90, 190, 255);
            Rect(20, 92, 260f * arrived / asked.Length, 5, 2);
            Fill(170, 185, 215);
            Text($""heights {arrived:N0} / {asked.Length:N0}  ·  call {nextCall}/{calls.Length}: {call?.Status ?? ""...""}"", 20, 102);
        }
        if (saved != """" && Millis() - savedAt < 4000)
        {
            Fill(150, 230, 170);
            TextAlign(TextAlignH.Right, TextAlignV.Top);
            Text($""saved {saved} (plotter-ready: 2 pens)"", Width - 20, 140);
        }
        TextAlign(TextAlignH.Left, TextAlignV.Bottom);
        Fill(255, 255, 255, 90);
        Text(""M: world map · R: random country · T: terrain · C: contours · S: save plotter SVG · drag: turn · wheel: zoom · Up/Down: heights"", 20, Height - 14);
        Fill(255, 255, 255, 60);
        Text($""Elevation: Open Topo Data ({Dataset}) · Borders: Natural Earth"", 20, Height - 32);
    }

    // =====================================================================================
    // Input
    // =====================================================================================
    public override void MousePressed() { pressX = MouseX; pressY = MouseY; }

    public override void MouseReleased()
    {
        if (Dist(pressX, pressY, MouseX, MouseY) > 5 || !countriesReq.Ready) return;    // a drag, not a click
        if (picking)
        {
            if (MouseY < mapY || MouseY > mapY + Width / 2) return;
            var (la, lo) = Geo.FromEquirectangular(MouseX, MouseY - mapY, Width, Width / 2);
            var f = countriesReq.Value.FeatureAt(la, lo);
            if (f != null) Choose(f, la, lo);
        }
        else if (MouseX > Width - 216 && MouseY < 116) picking = true;
    }

    public override void MouseDragged()
    {
        if (picking) return;
        yaw -= (MouseX - PMouseX) * 0.008f;
        pitch = Constrain(pitch - (MouseY - PMouseY) * 0.008f, 0.1f, 1.5f);
    }

    public override void MouseWheel(float delta) => zoom = Constrain(zoom * (delta > 0 ? 1 / 1.1f : 1.1f), 0.5f, 6);

    public override void KeyPressed()
    {
        if (Key == 'm' || Key == 'M' || Key == '\t') { if (country != null) picking = !picking; }
        else if ((Key == 'r' || Key == 'R') && countriesReq.Ready) RandomCountry();
        else if (picking) return;
        else if (Key == 't' || Key == 'T') contours = false;
        else if (Key == 'c' || Key == 'C') contours = true;
        else if ((Key == 's' || Key == 'S') && arrived > 0) SaveSvg();
        else if (KeyCode == UP) heightMul = Math.Min(4, heightMul * 1.25f);
        else if (KeyCode == DOWN) heightMul = Math.Max(0.25f, heightMul / 1.25f);
    }

    void RandomCountry()
    {
        var all = countriesReq.Value.Features;
        var f = all[(int)Random(all.Count)];
        Choose(f, float.NaN, float.NaN);
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
