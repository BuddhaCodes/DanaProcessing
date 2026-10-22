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
            new SketchSample(
                Loc.Tr("Un país vivo: relieve, clima y agua", "A living country: relief, weather and water"),
                Loc.Tr("Elegí un país y aparece su relieve en detalle (unas 180 alturas a lo largo, leídas de los tiles de terreno de AWS/Mapzen con img.Pixels), con el clima real de Open-Meteo para ~25 puntos: temperatura, nubes, lluvia y viento ahora y hora por hora durante 48 horas, que se reproducen solas. Las nubes flotan y derivan con el viento, llueve donde dice el pronóstico y el agua cae sobre el terreno: una simulación de \"tubos virtuales\" la hace correr cuesta abajo, formar ríos, llenar lagos y salir al mar, mientras el sol evapora más donde hace calor. Hacé clic en la tierra para soltar un lago, D desata un diluvio, L cambia la capa (relieve, temperatura, lluvia), , y . mueven las horas, W seca todo, M vuelve al mapa y R elige un país al azar.",
                       "Pick a country and its relief appears in detail (about 180 heights along it, read from AWS/Mapzen terrain tiles with img.Pixels), with the real weather from Open-Meteo for ~25 points: temperature, clouds, rain and wind now and hour by hour for 48 hours, which play by themselves. Clouds float and drift with the wind, it rains where the forecast says, and the water lands on the terrain: a \"virtual pipes\" simulation makes it run downhill, form rivers, fill lakes and drain to the sea, while the sun evaporates more where it's hot. Click the land to drop a lake, D unleashes a deluge, L switches layers (relief, temperature, rain), , and . move through the hours, W dries everything, M goes back to the map and R picks a random country."),
                LivingCountry,
                new[] { SampleTag.Data, SampleTag.ThreeD, SampleTag.Generative, SampleTag.Interaction }),
            new SketchSample(
                Loc.Tr("Dentro de Kestrel: los signos vitales de una app ASP.NET Core, en 3D", "Inside Kestrel: the vital signs of an ASP.NET Core app, in 3D"),
                Loc.Tr("Una app ASP.NET Core de verdad, en 3D, filmada y escuchada mientras corre: los servicios en profundidad por el camino de un pedido, y un director de cámara que corta entre plano general, vuelo a través, seguir un pedido, grúa y primeros planos, con profundidad de campo, niebla y cámara lenta cuando el GC detiene el mundo. PulseShop (en showcase/PulseShop: arrancala con dotnet run) es una tiendita de cinco servicios que se llaman por HTTP real, más una base de datos; se observa a sí misma con lo que trae .NET (un ActivityListener en cada request, el GC y el thread pool) y lo transmite por SignalR. Este sketch se conecta con ConnectSignalR(), sin paquetes. Cada request es una chispa de luz que va y vuelve, tan lenta como fue de verdad; cada servicio, una esfera que crece con su tráfico y arde con sus errores; los hilos del thread pool orbitan el gateway y el trabajo que espera se enrosca en la puerta. Suena como un monitor cardíaco: un pitido cada 25 pedidos servidos (el ritmo ES el rendimiento), un zumbido por cada error, un golpe grave por cada GC completo, un zumbido que sube con la cola del thread pool, y el pitido continuo cuando la app deja de atender. C prende o apaga el director (arrastrar toma la cámara). Rompela con el teclado (L latencia, F fallos, S sync-over-async, G tormenta de GC, Espacio pico de tráfico, R arreglar) o desde un celular en http://localhost:5199; la cámara va a lo que se rompió y un rótulo explica qué pasa. Sin PulseShop, D muestra una simulación (avisando).",
                       "A real ASP.NET Core app in 3D, filmed and heard as it runs: the services laid out in depth along a request's path, and a camera director cutting between a wide shot, a fly-through, riding a request, a crane and close-ups, with depth of field, fog and slow motion when the GC stops the world. PulseShop (in showcase/PulseShop: start it with dotnet run) is a little store of five services calling each other over real HTTP, plus a database; it watches itself with what .NET ships (an ActivityListener on every request, the GC and the thread pool) and streams it over SignalR. This sketch connects with ConnectSignalR(), no packages. Every request is a spark of light going there and back, as slow as it really was; each service a sphere that swells with its traffic and burns with its errors; the thread pool's threads orbit the gateway and waiting work coils up at the door. It sounds like a heart monitor: a beep every 25 requests served (the tempo IS the throughput), a buzz per error, a deep boom per full GC, a hum that rises with the thread pool queue, and the flatline tone when the app stops serving. C turns the director on or off (dragging takes the camera). Break it from the keyboard (L latency, F failures, S sync-over-async, G GC storm, Space traffic spike, R fix) or from a phone at http://localhost:5199; the camera goes to what broke and a caption explains what's happening. Without PulseShop, D shows a simulation (labelled)."),
                InsideKestrel,
                new[] { SampleTag.Data, SampleTag.ThreeD, SampleTag.Audio, SampleTag.Interaction }),
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

        private const string LivingCountry =
@"// A living country: its relief in detail, its weather right now and for the next two days,
// and water that falls as the real rain and runs downhill into rivers and lakes.
//
// Pick a country on the world map. Three free, keyless sources load in the background:
// - the relief: Terrain Tiles on AWS (Mapzen's ""terrarium"" PNGs), a height in every pixel
//   -- (red * 256 + green + blue / 256) - 32768 meters -- read with img.Pixels. About
//   180 heights along the country's longest side, for its real outline.
// - the weather: Open-Meteo, one call for ~25 points over the country: temperature,
//   clouds, rain and wind now, and hour by hour for 48 hours.
// - the outlines: Natural Earth.
//
// The water is a ""virtual pipes"" simulation: every cell holds some water and pushes it to
// its four neighbors through pipes, more the steeper the water surface. Rain adds water
// where the forecast says it rains (a lot faster than real life, so you see it), the sun
// takes some back (more where it's hot), and it drains out at the coast and the borders.
// Rivers find the valleys by themselves; lakes fill the basins.
//
// Click the land: drop a lake there.  D: a five-second deluge everywhere.
// L: layer -- relief / temperature / rain.  Space: pause.  , and .: hour back / forward.
// M (or the little map): the world.  R: a random country.  Drag: turn.  Wheel: zoom.
// Up/Down: heights.  W: empty the water.
public class LivingCountry : Sketch
{
    // ---- settings -----------------------------------------------------------------------
    const int Cells = 180;                       // grid points along the country's longest side
    const float W = 60;                          // that side, in world units
    const float RainSpeed = 0.0004f;             // world units of water per second, per mm/h of rain
    const string CountriesUrl = ""https://raw.githubusercontent.com/nvkelso/natural-earth-vector/master/geojson/ne_50m_admin_0_countries.geojson"";

    // ---- the world map ------------------------------------------------------------------
    Request<GeoMap> countriesReq;
    PGraphics worldMap;
    float mapY = 70;
    GeoFeature hover, country;
    bool picking = true;

    // ---- the chosen country -------------------------------------------------------------
    readonly List<(float[] Lat, float[] Lon)> outline = new();
    float lat0, lon0, minLat, maxLat, minLon, maxLon, kmX, kmZ, scale, dLat, dLon;
    int nx, ny;
    bool[] inside, drawn;                        // in the country / drawn (inside or touching it)
    float[] meters;                              // terrain height (m), NaN until its tile is in
    long[] arrivedAt;
    int tileZoom, tx0, ty0, tilesW, tilesH;
    Request<PImage>[] tiles;
    bool[] tileRead;
    int heightsIn;
    float hi = 1;

    // ---- the weather --------------------------------------------------------------------
    Request<JSONArray> weatherReq;
    readonly List<(float Lat, float Lon)> stations = new();
    float[][] hourTemp, hourRain, hourCloud;     // [station][hour]
    int[][] hourCode;
    string[] hourTimes;
    int nowHour;                                 // the forecast's index of the current hour
    float hour;                                  // the hour being shown (fractional: it plays)
    float windKmh, windFromDeg;
    bool weatherRead;
    float[] temp, rain, cloud;                   // per grid cell, at the shown hour
    int fieldsForHour = -1;
    float tMin, tMax, rainMax;

    // ---- the water ----------------------------------------------------------------------
    float[] b;                                   // terrain height, world units
    float[] d;                                   // water depth, world units
    float[] fE, fW, fN, fS;                      // flow through each cell's four pipes
    float cs;                                    // cell size, world units
    float deluge;                                // seconds of deluge left

    // ---- view ---------------------------------------------------------------------------
    PFont font;
    int layer;                                   // 0 relief, 1 temperature, 2 rain
    float yaw = -0.5f, pitch = 0.8f, zoom = 1, heightMul = 1, ex = -1;
    float animT;
    bool paused;
    float pressX, pressY;
    float[] sx, sy;                              // each grid point on screen (for clicks)
    readonly PointCloud clouds = new();

    public override void Setup()
    {
        Size(1000, 640);
        font = CreateFont(""Segoe UI"", 14);
        countriesReq = RequestGeoJSON(CountriesUrl, cacheFor: TimeSpan.FromDays(365));
    }

    // =====================================================================================
    // Choosing a country: its main piece and the islands near it, then a grid over it
    // =====================================================================================
    void Choose(GeoFeature f, float clickLat, float clickLon)
    {
        country = f;
        picking = false;
        outline.Clear();
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
        float reach = Math.Max(1.5f, 0.4f * Math.Max(mLat1 - mLat0, mLon1 - mLon0));
        foreach (var ring in f.Rings)
        {
            var (a0, a1, o0, o1) = Box(ring.Lat, ring.Lon);
            float shift = 360 * MathF.Round((mainLon - (o0 + o1) / 2) / 360);   // across the date line
            if (a1 < mLat0 - reach || a0 > mLat1 + reach || o1 + shift < mLon0 - reach || o0 + shift > mLon1 + reach) continue;
            var lon = (float[])ring.Lon.Clone();
            for (int i = 0; i < lon.Length; i++) lon[i] += shift;
            outline.Add((ring.Lat, lon));
        }

        minLat = minLon = float.MaxValue; maxLat = maxLon = float.MinValue;
        foreach (var (la, lo) in outline)
        {
            var (a0, a1, o0, o1) = Box(la, lo);
            minLat = Math.Min(minLat, a0); maxLat = Math.Max(maxLat, a1);
            minLon = Math.Min(minLon, o0); maxLon = Math.Max(maxLon, o1);
        }
        float padLat = (maxLat - minLat) * 0.03f + 0.02f, padLon = (maxLon - minLon) * 0.03f + 0.02f;
        minLat -= padLat; maxLat += padLat; minLon -= padLon; maxLon += padLon;
        lat0 = (minLat + maxLat) / 2; lon0 = (minLon + maxLon) / 2;
        kmX = 111.32f * MathF.Cos(lat0 * MathF.PI / 180); kmZ = 110.57f;
        float wKm = (maxLon - minLon) * kmX, hKm = (maxLat - minLat) * kmZ;
        scale = W / Math.Max(wKm, hKm);
        float spacing = Math.Max(wKm, hKm) / (Cells - 1);
        nx = Math.Max(3, (int)MathF.Round(wKm / spacing) + 1);
        ny = Math.Max(3, (int)MathF.Round(hKm / spacing) + 1);
        dLon = (maxLon - minLon) / (nx - 1); dLat = (maxLat - minLat) / (ny - 1);
        cs = spacing * scale;

        Mask();
        int n = nx * ny;
        meters = new float[n]; Array.Fill(meters, float.NaN);
        arrivedAt = new long[n];
        b = new float[n]; d = new float[n];
        fE = new float[n]; fW = new float[n]; fN = new float[n]; fS = new float[n];
        temp = new float[n]; rain = new float[n]; cloud = new float[n];
        sx = new float[n]; sy = new float[n];
        heightsIn = 0; hi = 1; ex = -1;
        RequestTiles();
        RequestWeather();
    }

    /// <summary>
    /// Which grid points are inside the country, row by row: where each row crosses the
    /// outline, sorted, every other stretch is inside (much faster than testing each point).
    /// </summary>
    void Mask()
    {
        inside = new bool[nx * ny];
        var xs = new List<float>();
        for (int r = 0; r < ny; r++)
        {
            float lat = minLat + r * dLat;
            xs.Clear();
            foreach (var (la, lo) in outline)
                for (int i = 0, j = la.Length - 1; i < la.Length; j = i++)
                    if ((la[i] > lat) != (la[j] > lat))
                        xs.Add(lo[i] + (lat - la[i]) / (la[j] - la[i]) * (lo[j] - lo[i]));
            xs.Sort();
            for (int k = 0; k + 1 < xs.Count; k += 2)
            {
                int c0 = Math.Max(0, (int)MathF.Ceiling((xs[k] - minLon) / dLon));
                int c1 = Math.Min(nx - 1, (int)MathF.Floor((xs[k + 1] - minLon) / dLon));
                for (int c = c0; c <= c1; c++) inside[r * nx + c] = true;
            }
        }
        drawn = new bool[nx * ny];
        for (int r = 0; r < ny; r++)
            for (int c = 0; c < nx; c++)
                for (int dr = -1; dr <= 1 && !drawn[r * nx + c]; dr++)
                    for (int dc = -1; dc <= 1; dc++)
                    {
                        int rr = r + dr, cc = c + dc;
                        if (rr >= 0 && cc >= 0 && rr < ny && cc < nx && inside[rr * nx + cc]) { drawn[r * nx + c] = true; break; }
                    }
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

    // =====================================================================================
    // The relief: terrain tiles, Web Mercator like every web map
    // =====================================================================================
    static float TileX(float lon, int z) => (lon + 180) / 360 * (1 << z);
    static float TileY(float lat, int z)
    {
        double r = Math.Clamp(lat, -85, 85) * Math.PI / 180;
        return (float)((1 - Math.Log(Math.Tan(r) + 1 / Math.Cos(r)) / Math.PI) / 2 * (1 << z));
    }

    string TileUrl(int z, int x, int y) => $""https://s3.amazonaws.com/elevation-tiles-prod/terrarium/{z}/{x}/{y}.png"";

    void RequestTiles()
    {
        // The deepest zoom whose tiles over the country are 16 or fewer (plenty for 180 points).
        for (tileZoom = 10; tileZoom > 1; tileZoom--)
        {
            tx0 = (int)MathF.Floor(TileX(minLon, tileZoom)); ty0 = (int)MathF.Floor(TileY(maxLat, tileZoom));
            tilesW = (int)MathF.Floor(TileX(maxLon, tileZoom)) - tx0 + 1;
            tilesH = (int)MathF.Floor(TileY(minLat, tileZoom)) - ty0 + 1;
            if (tilesW * tilesH <= 16) break;
        }
        tiles = new Request<PImage>[tilesW * tilesH];
        tileRead = new bool[tiles.Length];
        int n = 1 << tileZoom;
        for (int t = 0; t < tiles.Length; t++)
        {
            int x = ((tx0 + t % tilesW) % n + n) % n, y = ty0 + t / tilesW;
            if (y >= 0 && y < n) tiles[t] = RequestPImage(TileUrl(tileZoom, x, y), cacheFor: TimeSpan.FromDays(90));
        }
    }

    void ReadTiles()
    {
        for (int t = 0; t < tiles.Length; t++)
        {
            if (tileRead[t] || tiles[t] == null || !tiles[t].Done) continue;
            tileRead[t] = true;
            if (!tiles[t].Ready) continue;
            var img = tiles[t].Value;
            img.LoadPixels();
            var px = img.Pixels;
            int size = img.Width;
            float Height(int x, int y)
            {
                var c = px[Math.Clamp(y, 0, size - 1) * size + Math.Clamp(x, 0, size - 1)];
                return c.R * 256f + c.G + c.B / 256f - 32768;              // terrarium encoding
            }
            int tileX = tx0 + t % tilesW, tileY = ty0 + t / tilesW;
            for (int i = 0; i < nx * ny; i++)
            {
                if (!drawn[i]) continue;
                float lat = minLat + i / nx * dLat, lon = minLon + i % nx * dLon;
                float fx = (TileX(lon, tileZoom) - tileX) * size - 0.5f, fy = (TileY(lat, tileZoom) - tileY) * size - 0.5f;
                if (fx < -0.5f || fy < -0.5f || fx >= size - 0.5f || fy >= size - 0.5f) continue;
                int x0 = (int)MathF.Floor(fx), y0 = (int)MathF.Floor(fy);
                float ax = fx - x0, ay = fy - y0;
                float h = Lerp(Lerp(Height(x0, y0), Height(x0 + 1, y0), ax), Lerp(Height(x0, y0 + 1), Height(x0 + 1, y0 + 1), ax), ay);
                meters[i] = inside[i] ? h : Math.Max(h, 0);
                arrivedAt[i] = Millis();
                heightsIn++;
                if (inside[i]) hi = Math.Max(hi, h);
            }
        }
    }

    // =====================================================================================
    // The weather: one call for ~25 points, now and hour by hour
    // =====================================================================================
    void RequestWeather()
    {
        stations.Clear();
        weatherRead = false; hourTemp = null; fieldsForHour = -1;
        for (int i = 0; i < 6; i++)
            for (int j = 0; j < 6; j++)
            {
                float la = minLat + (maxLat - minLat) * (i + 0.5f) / 6, lo = minLon + (maxLon - minLon) * (j + 0.5f) / 6;
                int r = (int)MathF.Round((la - minLat) / dLat), c = (int)MathF.Round((lo - minLon) / dLon);
                if (inside[Math.Clamp(r, 0, ny - 1) * nx + Math.Clamp(c, 0, nx - 1)]) stations.Add((la, lo));
            }
        while (stations.Count < 2) stations.Add((lat0, lon0));    // two or more: the API answers with a list
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        string lats = string.Join("","", stations.Select(s => s.Lat.ToString(""0.###"", inv)));
        string lons = string.Join("","", stations.Select(s => (((s.Lon + 540) % 360) - 180).ToString(""0.###"", inv)));
        weatherReq = RequestJSONArray($""https://api.open-meteo.com/v1/forecast?latitude={lats}&longitude={lons}"" +
            ""&current=temperature_2m,wind_speed_10m,wind_direction_10m&hourly=temperature_2m,precipitation,cloud_cover,weather_code"" +
            ""&forecast_days=3&timezone=GMT&wind_speed_unit=kmh"", cacheFor: TimeSpan.FromMinutes(30));
    }

    void ReadWeather()
    {
        if (weatherRead || weatherReq == null || !weatherReq.Done) return;
        weatherRead = true;
        if (!weatherReq.Ready) return;
        var list = weatherReq.Value;
        int s = Math.Min(list.Size(), stations.Count);
        hourTemp = new float[s][]; hourRain = new float[s][]; hourCloud = new float[s][]; hourCode = new int[s][];
        float wsum = 0, wx = 0, wz = 0;
        for (int k = 0; k < s; k++)
        {
            var o = list.GetJSONObject(k);
            var h = o.GetJSONObject(""hourly"");
            var times = h.GetJSONArray(""time"");
            int count = times.Size();
            hourTemp[k] = Floats(h.GetJSONArray(""temperature_2m""), count);
            hourRain[k] = Floats(h.GetJSONArray(""precipitation""), count);
            hourCloud[k] = Floats(h.GetJSONArray(""cloud_cover""), count);
            var codes = h.GetJSONArray(""weather_code"");
            hourCode[k] = new int[count];
            for (int i = 0; i < count; i++) hourCode[k][i] = codes.GetInt(i);
            if (k == 0)
            {
                hourTimes = new string[count];
                for (int i = 0; i < count; i++) hourTimes[i] = times.GetString(i);
                string now = o.GetJSONObject(""current"").GetString(""time"");
                nowHour = Math.Max(0, Array.FindIndex(hourTimes, t => now.Length >= 13 && t.StartsWith(now.Substring(0, 13))));
                hour = nowHour;
            }
            // The wind: the average over the stations, as a vector.
            var cur = o.GetJSONObject(""current"");
            float sp = cur.GetFloat(""wind_speed_10m""), dir = Radians(cur.GetFloat(""wind_direction_10m""));
            wx += sp * Sin(dir); wz += sp * Cos(dir); wsum++;
        }
        windKmh = Mag(wx, wz) / Math.Max(1, wsum);
        windFromDeg = (Degrees(Atan2(wx, wz)) + 360) % 360;
    }

    static float[] Floats(JSONArray a, int count)
    {
        var f = new float[count];
        for (int i = 0; i < count && i < a.Size(); i++) f[i] = a.GetFloat(i);
        return f;
    }

    /// <summary>The weather on the grid at the shown hour: each cell weighs the stations by 1 / distance².</summary>
    void Fields()
    {
        int h = (int)hour;
        if (hourTemp == null || h == fieldsForHour) return;
        fieldsForHour = h;
        tMin = float.MaxValue; tMax = float.MinValue; rainMax = 0;
        for (int i = 0; i < nx * ny; i++)
        {
            if (!drawn[i]) continue;
            float la = minLat + i / nx * dLat, lo = minLon + i % nx * dLon;
            float wsum = 0, t = 0, r = 0, c = 0;
            for (int k = 0; k < hourTemp.Length; k++)
            {
                float dx = (lo - stations[k].Lon) * kmX, dz = (la - stations[k].Lat) * kmZ;
                float w = 1 / (dx * dx + dz * dz + 25);
                int hh = Math.Min(h, hourTemp[k].Length - 1);
                t += w * hourTemp[k][hh]; r += w * hourRain[k][hh]; c += w * hourCloud[k][hh];
                wsum += w;
            }
            temp[i] = t / wsum; rain[i] = r / wsum; cloud[i] = c / wsum;
            if (inside[i]) { tMin = Math.Min(tMin, temp[i]); tMax = Math.Max(tMax, temp[i]); rainMax = Math.Max(rainMax, rain[i]); }
        }
    }

    // =====================================================================================
    // The water: virtual pipes
    // =====================================================================================
    void Simulate(float dt)
    {
        const float g = 9.8f, damping = 0.995f;
        int n = nx * ny;
        float area = cs * cs;
        // Sources and sinks: rain, the sun, the sea.
        for (int i = 0; i < n; i++)
        {
            if (!inside[i]) { d[i] = 0; continue; }
            float mmh = (hourTemp != null ? rain[i] : 0) + (deluge > 0 ? 40 : 0);
            d[i] += mmh * RainSpeed * dt;
            float evaporate = (0.0003f + 0.00003f * Math.Max(0, temp[i])) * dt;
            d[i] = Math.Max(0, d[i] - evaporate);
        }
        // Each pipe's flow grows with the difference of water surface across it.
        for (int r = 0; r < ny; r++)
            for (int c = 0; c < nx; c++)
            {
                int i = r * nx + c;
                if (!inside[i]) { fE[i] = fW[i] = fN[i] = fS[i] = 0; continue; }
                float h = b[i] + d[i];
                float Pipe(float f, int j) => j < 0 ? 0 : Math.Max(0, f * damping + dt * g * cs * (h - b[j] - d[j]));
                fE[i] = Pipe(fE[i], c + 1 < nx ? i + 1 : -1);
                fW[i] = Pipe(fW[i], c > 0 ? i - 1 : -1);
                fN[i] = Pipe(fN[i], r + 1 < ny ? i + nx : -1);
                fS[i] = Pipe(fS[i], r > 0 ? i - nx : -1);
                float out1 = (fE[i] + fW[i] + fN[i] + fS[i]) * dt;
                if (out1 > d[i] * area && out1 > 0)
                {
                    float k = d[i] * area / out1;                // never send more than the cell has
                    fE[i] *= k; fW[i] *= k; fN[i] *= k; fS[i] *= k;
                }
            }
        for (int r = 0; r < ny; r++)
            for (int c = 0; c < nx; c++)
            {
                int i = r * nx + c;
                if (!inside[i]) continue;
                float inflow = (c > 0 ? fE[i - 1] : 0) + (c + 1 < nx ? fW[i + 1] : 0) + (r > 0 ? fN[i - nx] : 0) + (r + 1 < ny ? fS[i + nx] : 0);
                float outflow = fE[i] + fW[i] + fN[i] + fS[i];
                d[i] = Math.Max(0, d[i] + dt * (inflow - outflow) / area);
            }
    }

    // =====================================================================================
    // Each frame
    // =====================================================================================
    public override void Draw()
    {
        Background(8, 12, 20);
        if (!countriesReq.Ready) { Centered(""countries: "" + countriesReq.Status); return; }
        if (worldMap == null) BuildWorldMap();
        if (picking) { DrawPicker(); return; }

        ReadTiles();
        ReadWeather();
        Fields();
        float dt = 1 / 60f;
        if (!paused)
        {
            animT += dt;
            if (hourTemp != null) hour = Math.Min(hour + dt / 4, hourTimes.Length - 1);   // 4 s per forecast hour
            deluge = Math.Max(0, deluge - dt);
            for (int k = 0; k < 3; k++) Simulate(dt);
        }

        // Terrain heights in world units, rising as they arrive; the exaggeration eases in.
        float target = Math.Clamp(0.09f * W / (Math.Max(hi, 300) / 1000 * scale), 1, 120) * heightMul;
        ex = ex < 0 ? target : Lerp(ex, target, 0.08f);
        float k1 = scale * ex / 1000;
        for (int i = 0; i < nx * ny; i++)
        {
            if (!drawn[i]) continue;
            float m = float.IsNaN(meters[i]) ? 0 : meters[i];
            float t = Math.Clamp((Millis() - arrivedAt[i]) / 900f, 0, 1);
            b[i] = m * k1 * (float.IsNaN(meters[i]) ? 0 : 1 - (1 - t) * (1 - t) * (1 - t));
        }

        var view = View3D.Orbit(yaw, pitch, 62 / zoom, new PVector(0, 1.5f, 0), 45);
        DrawLand(view);
        DrawSky(view);
        DrawBorder(view);
        Hud();
        Legend();
        MiniMap();
    }

    PVector World(int i, float y) =>
        new PVector((minLon + i % nx * dLon - lon0) * kmX * scale, y, (minLat + i / nx * dLat - lat0) * kmZ * scale);

    /// <summary>
    /// The terrain and its water, as one batch of triangles. No depth sorting: walking the
    /// grid from the side far from the camera to the near side paints a height field in the
    /// right order; each cell's water is drawn right over its ground.
    /// </summary>
    void DrawLand(View3D view)
    {
        int n = nx * ny;
        var wsx = new float[n]; var wsy = new float[n];
        var ground = new Color[n]; var water = new Color[n];
        var light = new PVector(-0.55f, 0.75f, 0.45f);
        light.Normalize();
        float top = Math.Max(hi, 400);
        for (int i = 0; i < n; i++)
        {
            if (!drawn[i]) continue;
            var p = view.Project(World(i, b[i]), Width, Height);
            sx[i] = p.X; sy[i] = p.Y;
            int c = i % nx, r = i / nx;
            float hl = b[c > 0 ? i - 1 : i], hr = b[c + 1 < nx ? i + 1 : i], hd = b[r > 0 ? i - nx : i], hu = b[r + 1 < ny ? i + nx : i];
            var nrm = new PVector(-(hr - hl) / (2 * cs), 1, -(hu - hd) / (2 * cs));
            nrm.Normalize();
            float lit = 0.35f + 0.8f * Math.Max(0, nrm.X * light.X + nrm.Y * light.Y + nrm.Z * light.Z);
            Color baseColor = float.IsNaN(meters[i]) ? Color(38, 52, 72)
                : layer == 1 && hourTemp != null ? TempColor(temp[i])
                : layer == 2 && hourTemp != null ? RainColor(rain[i], meters[i], top)
                : Hypsometric(meters[i], top);
            ground[i] = Color(Math.Min(255, baseColor.R * lit), Math.Min(255, baseColor.G * lit), Math.Min(255, baseColor.B * lit), inside[i] ? 255 : 120);
            if (d[i] > 0.003f)
            {
                var q = view.Project(World(i, b[i] + d[i]), Width, Height);
                wsx[i] = q.X; wsy[i] = q.Y;
                float deep = Math.Clamp(d[i] / 0.5f, 0, 1);
                float speed = Math.Clamp((fE[i] + fW[i] + fN[i] + fS[i]) / (cs * Math.Max(d[i], 0.02f)) * 0.6f, 0, 1);
                water[i] = Color(Lerp(Lerp(110, 25, deep), 235, speed * 0.6f), Lerp(Lerp(185, 85, deep), 245, speed * 0.6f), Lerp(Lerp(240, 175, deep), 255, speed * 0.6f),
                                 Lerp(120, 225, deep));
            }
            else { wsx[i] = sx[i]; wsy[i] = sy[i]; water[i] = Color(90, 170, 240, 0); }
        }

        // Far to near: which way that is depends on where the camera is.
        var eye = view.Eye;
        bool rowsUp = eye.Z < 0, colsUp = eye.X < 0;       // camera south -> start with the north rows
        NoStroke();
        BeginShape(ShapeKind.Triangles);
        for (int rr = 0; rr < ny - 1; rr++)
        {
            int r = rowsUp ? ny - 2 - rr : rr;
            for (int cc = 0; cc < nx - 1; cc++)
            {
                int c = colsUp ? nx - 2 - cc : cc;
                int a = r * nx + c, e = a + 1, f = a + nx, g = f + 1;
                if (!drawn[a] || !drawn[e] || !drawn[f] || !drawn[g]) continue;
                if (!inside[a] && !inside[e] && !inside[f] && !inside[g]) continue;
                Fill(ground[a]); Vertex(sx[a], sy[a]); Fill(ground[e]); Vertex(sx[e], sy[e]); Fill(ground[g]); Vertex(sx[g], sy[g]);
                Fill(ground[a]); Vertex(sx[a], sy[a]); Fill(ground[g]); Vertex(sx[g], sy[g]); Fill(ground[f]); Vertex(sx[f], sy[f]);
                if (water[a].A + water[e].A + water[f].A + water[g].A == 0) continue;
                Fill(water[a]); Vertex(wsx[a], wsy[a]); Fill(water[e]); Vertex(wsx[e], wsy[e]); Fill(water[g]); Vertex(wsx[g], wsy[g]);
                Fill(water[a]); Vertex(wsx[a], wsy[a]); Fill(water[g]); Vertex(wsx[g], wsy[g]); Fill(water[f]); Vertex(wsx[f], wsy[f]);
            }
        }
        EndShape();
    }

    /// <summary>Clouds where the forecast has them, drifting with the wind; rain under the rainy ones.</summary>
    void DrawSky(View3D view)
    {
        if (hourTemp == null && deluge <= 0) return;
        clouds.Clear();
        float to = Radians(windFromDeg + 180);
        float drift = windKmh / 30f * animT;                     // world units the clouds have moved
        float dx = Sin(to) * drift, dz = Cos(to) * drift;
        float lean = Math.Min(windKmh, 60) / 60f * 0.5f;
        float skyY = Math.Max(hi, 400) / 1000 * scale * ex + 4;
        float spanX = (maxLon - minLon) * kmX * scale, spanZ = (maxLat - minLat) * kmZ * scale;
        var streaks = new List<(float, float, float, float)>();
        int lattice = 52;
        for (int a = 0; a < lattice; a++)
            for (int c = 0; c < lattice; c++)
            {
                // A cloud slot fixed to the air, moved by the wind and wrapped around the country.
                float h1 = Hash(a * 977 + c), h2 = Hash(a * 131 + c * 7 + 3);
                float x = Wrap((c + h1) / lattice * spanX + dx, spanX) - spanX / 2;
                float z = Wrap((a + h2) / lattice * spanZ + dz, spanZ) - spanZ / 2;
                int col = (int)MathF.Round((x / (kmX * scale) + lon0 - minLon) / dLon), row = (int)MathF.Round((z / (kmZ * scale) + lat0 - minLat) / dLat);
                if (col < 0 || row < 0 || col >= nx || row >= ny) continue;
                int i = row * nx + col;
                if (!drawn[i]) continue;
                float cover = deluge > 0 ? 100 : cloud[i];
                if (h1 * 100 > cover) continue;
                float wet = deluge > 0 ? 40 : rain[i];
                float dark = Math.Clamp(wet / 4, 0, 1);
                float y = skyY + h2 * 2;
                clouds.Add(x, y, z, Color(Lerp(235, 120, dark), Lerp(240, 130, dark), Lerp(250, 150, dark), 60 + 90 * dark), 0.9f + 0.9f * h2);
                // Rain: a few streaks per rainy cloud, falling to the ground.
                int drops = wet > 0.1f ? 1 + (int)Math.Min(3, wet / 2) : 0;
                for (int k = 0; k < drops; k++)
                {
                    float ph = Hash(a * 31 + c * 17 + k);
                    float fall = (animT * 0.9f + ph) % 1;
                    float yy = y * (1 - fall);
                    float px = x + (ph - 0.5f) * 1.2f - Sin(to) * lean * (y - yy), pz = z + (Hash(k + c) - 0.5f) * 1.2f - Cos(to) * lean * (y - yy);
                    var p = view.Project(new PVector(px, yy, pz), Width, Height);
                    var q = view.Project(new PVector(px + Sin(to) * lean, yy + 1.2f, pz + Cos(to) * lean), Width, Height);
                    if (p.Depth > 0.5f && q.Depth > 0.5f) streaks.Add((p.X, p.Y, q.X, q.Y));
                }
            }
        StrokeWeight(1);
        Stroke(170, 205, 255, 150);
        BeginShape(ShapeKind.Lines);
        foreach (var (x0, y0, x1, y1) in streaks) { Vertex(x0, y0); Vertex(x1, y1); }
        EndShape();
        Points(clouds, view);
    }

    static float Wrap(float v, float m) => ((v % m) + m) % m;

    void DrawBorder(View3D view)
    {
        var segs = new List<(float, float, float, float)>();
        foreach (var (la, lo) in outline)
        {
            int step = Math.Max(1, la.Length / 1500);
            (float X, float Y, float Depth) prev = default;
            for (int i = 0; i <= la.Length; i += step)
            {
                int j = Math.Min(i, la.Length - 1);
                int c = Math.Clamp((int)MathF.Round((lo[j] - minLon) / dLon), 0, nx - 1), r = Math.Clamp((int)MathF.Round((la[j] - minLat) / dLat), 0, ny - 1);
                float y = b[r * nx + c] + 0.05f;
                var p = view.Project(new PVector((lo[j] - lon0) * kmX * scale, y, (la[j] - lat0) * kmZ * scale), Width, Height);
                if (i > 0 && p.Depth > 0.5f && prev.Depth > 0.5f) segs.Add((prev.X, prev.Y, p.X, p.Y));
                prev = p;
            }
        }
        StrokeWeight(1.2f);
        Stroke(255, 255, 255, 140);
        BeginShape(ShapeKind.Lines);
        foreach (var (x0, y0, x1, y1) in segs) { Vertex(x0, y0); Vertex(x1, y1); }
        EndShape();
    }

    // ---- colors ---------------------------------------------------------------------------
    Color Hypsometric(float h, float top)
    {
        float k = Math.Clamp(top / 4500, 0.25f, 1);
        (float H, int R, int G, int B)[] stops =
        {
            (0, 60, 130, 85), (250, 105, 160, 90), (700, 190, 185, 110), (1400, 175, 130, 80),
            (2300, 140, 110, 95), (3300, 205, 205, 210), (4500, 250, 250, 255),
        };
        return Ramp(stops, h, k);
    }

    Color TempColor(float t)
    {
        (float H, int R, int G, int B)[] stops =
        {
            (-20, 120, 60, 200), (-5, 90, 140, 235), (5, 120, 210, 230), (15, 150, 220, 120),
            (25, 245, 200, 70), (33, 235, 100, 50), (42, 170, 30, 60),
        };
        return Ramp(stops, t, 1);
    }

    Color RainColor(float mmh, float meters, float top)
    {
        var g = Hypsometric(meters, top);
        float gray = (g.R + g.G + g.B) / 3f * 0.6f;
        float k = Math.Clamp(mmh / 8, 0, 1);
        return mmh < 0.05f ? Color(gray, gray, gray) : Color(Lerp(gray, 60, k), Lerp(gray + 30, 120, k), Lerp(gray + 60, 255, k));
    }

    Color Ramp((float H, int R, int G, int B)[] stops, float v, float k)
    {
        for (int s = 1; s < stops.Length; s++)
        {
            float h1 = stops[s].H * k;
            if (v <= h1 || s == stops.Length - 1)
            {
                float h0 = stops[s - 1].H * k;
                float t = Math.Clamp((v - h0) / Math.Max(1e-3f, h1 - h0), 0, 1);
                return Color(Lerp(stops[s - 1].R, stops[s].R, t), Lerp(stops[s - 1].G, stops[s].G, t), Lerp(stops[s - 1].B, stops[s].B, t));
            }
        }
        return Color(255, 255, 255);
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

    // =====================================================================================
    // The world map
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
        hover = null;
        if (MouseY > mapY && MouseY < mapY + Width / 2)
        {
            var (la, lo) = Geo.FromEquirectangular(MouseX, MouseY - mapY, Width, Width / 2);
            hover = countriesReq.Value.FeatureAt(la, lo);
        }
        if (hover != null)
        {
            Fill(255, 200, 90); Stroke(255, 240, 200); StrokeWeight(1);
            Shape(this, hover, Width, Width / 2, 0, mapY);
        }
        TextFont(font, 22);
        TextAlign(TextAlignH.Left, TextAlignV.Top);
        Fill(235, 240, 255);
        Text(hover?.Name ?? ""Pick a country"", 20, 16);
        TextFont(font, 13);
        Fill(170, 185, 215);
        Text(hover != null ? ""click: its relief, its weather and its water"" : ""click any country · R: a random one"", 20, 46);
        TextAlign(TextAlignH.Left, TextAlignV.Bottom);
        Fill(255, 255, 255, 60);
        Text(""Relief: Terrain Tiles on AWS (Mapzen) · Weather: Open-Meteo · Borders: Natural Earth"", 20, Height - 14);
    }

    void MiniMap()
    {
        float mw = 200, mh = 100, mx = Width - mw - 16, my = 16;
        Tint(255, 255, 255, 200);
        Image(worldMap, mx, my, mw, mh);
        NoTint();
        Fill(255, 200, 90); NoStroke();
        Shape(this, country, mw, mh, mx, my);
        NoFill(); Stroke(255, 255, 255, 60); StrokeWeight(1);
        Rect(mx, my, mw, mh, 4);
    }

    // =====================================================================================
    // Text on screen
    // =====================================================================================
    void Centered(string s)
    {
        TextFont(font, 16);
        TextAlign(TextAlignH.Center, TextAlignV.Center);
        Fill(170, 185, 215);
        Text(s, Width / 2, Height / 2);
    }

    void Hud()
    {
        TextFont(font, 22);
        TextAlign(TextAlignH.Left, TextAlignV.Top);
        Fill(235, 240, 255);
        Text(country.Name, 20, 16);
        TextFont(font, 13);
        Fill(170, 185, 215);
        int total = 0; foreach (bool v in drawn) if (v) total++;
        float y = 46;
        if (heightsIn < total)
        {
            int done = 0; foreach (var t in tiles) if (t == null || t.Done) done++;
            Text($""relief: {done}/{tiles.Length} tiles (zoom {tileZoom})"", 20, y); y += 20;
        }
        else
        {
            Text($""up to {hi:N0} m  ·  a height every {dLon * kmX:0.#} km  ·  heights x{ex:0.#}"", 20, y); y += 20;
        }
        if (hourTemp != null)
        {
            int h = (int)hour;
            var when = DateTime.Parse(hourTimes[h], System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal).ToLocalTime();
            string rel = h == nowHour ? ""now"" : h > nowHour ? $""in {h - nowHour} h"" : $""{nowHour - h} h ago"";
            Fill(235, 240, 255);
            Text($""{when:ddd HH:00} ({rel})  ·  {Condition(h)}"", 20, y); y += 20;
            Fill(170, 185, 215);
            Text($""{tMin:0}° to {tMax:0}°C  ·  rain up to {rainMax:0.#} mm/h  ·  wind {windKmh:0} km/h from {Compass(windFromDeg)}"", 20, y); y += 20;
        }
        else Text(""weather: "" + (weatherReq?.Status ?? ""...""), 20, y);
        if (deluge > 0) { Fill(140, 200, 255); Text(""DELUGE!"", 20, y + 4); }
        else if (paused) { Fill(255, 210, 120); Text(""PAUSED"", 20, y + 4); }
        else if (hourTemp != null && rainMax < 0.1f && WaterVolume() < 0.5f)
        {
            Fill(150, 220, 255);
            Text(""no rain here now: D for a deluge, or click the land to drop a lake"", 20, y + 4);
        }
        TextAlign(TextAlignH.Left, TextAlignV.Bottom);
        Fill(255, 255, 255, 90);
        Text(""click: drop water · D: deluge · L: layer · Space: pause · , .: hours · W: dry · M: world · R: random · drag: turn · wheel: zoom · Up/Down: heights"", 20, Height - 14);
        Fill(255, 255, 255, 60);
        Text(""Relief: Terrain Tiles on AWS (Mapzen) · Weather: Open-Meteo · Borders: Natural Earth"", 20, Height - 32);
    }

    void Legend()
    {
        if (layer == 0 || hourTemp == null) return;
        float x = Width - 216, y = 140, w = 200;
        for (int i = 0; i < w; i++)
        {
            float t = i / (w - 1);
            Stroke(layer == 1 ? TempColor(Lerp(-10, 40, t)) : RainColor(t * 10, 0, 1000));
            Line(x + i, y, x + i, y + 10);
        }
        NoStroke();
        Fill(200, 210, 230);
        TextFont(font, 11);
        TextAlign(TextAlignH.Left, TextAlignV.Top);
        Text(layer == 1 ? ""-10 °C"" : ""0"", x, y + 14);
        TextAlign(TextAlignH.Right, TextAlignV.Top);
        Text(layer == 1 ? ""40 °C  temperature"" : ""10 mm/h  rain"", x + w, y + 14);
    }

    string Condition(int h)
    {
        // The most common weather code over the stations at that hour, in words.
        var counts = new Dictionary<int, int>();
        foreach (var codes in hourCode) { int c = codes[Math.Min(h, codes.Length - 1)]; counts[c] = counts.GetValueOrDefault(c) + 1; }
        int code = counts.OrderByDescending(kv => kv.Value).First().Key;
        return code switch
        {
            0 => ""clear sky"", 1 => ""mostly clear"", 2 => ""partly cloudy"", 3 => ""overcast"",
            45 or 48 => ""fog"", >= 51 and <= 57 => ""drizzle"", >= 61 and <= 67 => ""rain"",
            >= 71 and <= 77 => ""snow"", >= 80 and <= 82 => ""showers"", 85 or 86 => ""snow showers"",
            >= 95 => ""thunderstorms"", _ => ""—"",
        };
    }

    float WaterVolume() { float v = 0; for (int i = 0; i < d.Length; i++) v += d[i]; return v * cs * cs; }

    static string Compass(float deg) => new[] { ""N"", ""NE"", ""E"", ""SE"", ""S"", ""SW"", ""W"", ""NW"" }[(int)Math.Round(((deg % 360) + 360) % 360 / 45) % 8];

    // =====================================================================================
    // Input
    // =====================================================================================
    public override void MousePressed() { pressX = MouseX; pressY = MouseY; }

    public override void MouseReleased()
    {
        if (Dist(pressX, pressY, MouseX, MouseY) > 5 || !countriesReq.Ready) return;   // a drag, not a click
        if (picking)
        {
            if (MouseY < mapY || MouseY > mapY + Width / 2) return;
            var (la, lo) = Geo.FromEquirectangular(MouseX, MouseY - mapY, Width, Width / 2);
            var f = countriesReq.Value.FeatureAt(la, lo);
            if (f != null) Choose(f, la, lo);
        }
        else if (MouseX > Width - 216 && MouseY < 116) picking = true;
        else DropWater(MouseX, MouseY);
    }

    /// <summary>The grid point drawn nearest the click gets a mound of water: it runs downhill from there.</summary>
    void DropWater(float x, float y)
    {
        int best = -1; float bestD = 30 * 30;
        for (int i = 0; i < nx * ny; i++)
        {
            if (!inside[i]) continue;
            float dd = (sx[i] - x) * (sx[i] - x) + (sy[i] - y) * (sy[i] - y);
            if (dd < bestD) { bestD = dd; best = i; }
        }
        if (best < 0) return;
        int bc = best % nx, br = best / nx, radius = Math.Max(3, nx / 30);
        for (int r = br - radius; r <= br + radius; r++)
            for (int c = bc - radius; c <= bc + radius; c++)
            {
                if (r < 0 || c < 0 || r >= ny || c >= nx || !inside[r * nx + c]) continue;
                float k = 1 - Dist(c, r, bc, br) / radius;
                if (k > 0) d[r * nx + c] += 1.2f * k * k;
            }
    }

    public override void MouseDragged()
    {
        if (picking) return;
        yaw -= (MouseX - PMouseX) * 0.008f;
        pitch = Constrain(pitch - (MouseY - PMouseY) * 0.008f, 0.15f, 1.5f);
    }

    public override void MouseWheel(float delta) => zoom = Constrain(zoom * (delta > 0 ? 1 / 1.1f : 1.1f), 0.5f, 6);

    public override void KeyPressed()
    {
        if (Key == 'm' || Key == 'M' || Key == '\t') { if (country != null) picking = !picking; }
        else if ((Key == 'r' || Key == 'R') && countriesReq.Ready)
        {
            var all = countriesReq.Value.Features;
            Choose(all[(int)Random(all.Count)], float.NaN, float.NaN);
        }
        else if (picking) return;
        else if (Key == ' ') paused = !paused;
        else if (Key == 'd' || Key == 'D') deluge = 5;
        else if (Key == 'w' || Key == 'W') { Array.Clear(d); Array.Clear(fE); Array.Clear(fW); Array.Clear(fN); Array.Clear(fS); }
        else if (Key == 'l' || Key == 'L') layer = (layer + 1) % 3;
        else if (Key == '.' && hourTimes != null) hour = Math.Min(hourTimes.Length - 1, (int)hour + 1);
        else if (Key == ',' && hourTimes != null) hour = Math.Max(0, (int)hour - 1);
        else if (KeyCode == UP) heightMul = Math.Min(4, heightMul * 1.25f);
        else if (KeyCode == DOWN) heightMul = Math.Max(0.25f, heightMul / 1.25f);
    }
}
";

        private const string InsideKestrel =
@"// Inside Kestrel: the vital signs of a live ASP.NET Core app -- in 3D, shot like a film.
//
// PulseShop (in showcase/PulseShop: any ASP.NET Core app could do this) is a little store
// made of five services that call each other over real HTTP, plus a database. It watches
// itself with what .NET ships -- an ActivityListener on every request, the GC and the
// thread pool -- and streams it over SignalR. This sketch connects with ConnectSignalR():
// no packages, just Dana.
//
// The app is laid out in depth, the way a request travels: from the internet, through the
// thread pool's ring of threads at the gateway, out to four services and down to the
// database. Every request is a spark of light going there and back, as slow as it really
// was. A camera director cuts between shots -- a wide orbit, a flight through the app,
// riding a request, a crane up from below, close-ups -- and runs to whatever breaks. Depth
// of field, fog, drifting dust, and slow motion when the GC stops the world.
//
// What you hear -- a heart monitor:
//   beep        25 customer requests served (the tempo IS the throughput)
//   buzz        a request failed
//   boom        a full (gen 2) garbage collection
//   rising hum  work waiting for a thread: the louder and higher, the longer the queue
//   flatline    nothing is being served
//
// Start PulseShop first:  cd showcase/PulseShop && dotnet run
//
// 1-6: pick a service (6 is the database).  L: +200 ms on it.  F: make it fail 30%.
// S: starve the thread pool (sync over async).  G: GC storm.  Space: traffic spike.
// + / -: load.  R: everything back to normal.  C: camera director on/off (dragging takes
// the camera).  Wheel: zoom.  M: mute.  Not running? D shows a simulation (labelled).
public class InsideKestrel : Sketch
{
    const string HubUrl = ""http://localhost:5199/pulse"";
    const int ServedPerBeep = 25;
    const float Bar = 64;                            // the letterbox bars

    // ---- the services ---------------------------------------------------------------------
    class Service
    {
        public string Name;
        public PVector Pos;
        public Color Color;
        public float Rate, Errors, Latency;          // smoothed: hops/s, error share, ms
        public float Pulse;                          // a kick when something arrives
        public int Count, ErrorCount; public float LatencySum;
    }

    readonly Service[] services =
    {
        new() { Name = ""internet"" }, new() { Name = ""gateway"" }, new() { Name = ""catalog"" }, new() { Name = ""cart"" },
        new() { Name = ""inventory"" }, new() { Name = ""payments"" }, new() { Name = ""db"" },
    };
    int Index(string name) { for (int i = 0; i < services.Length; i++) if (services[i].Name == name) return i; return -1; }

    // ---- light in flight --------------------------------------------------------------------
    struct Spark { public int From, To; public float Start, Duration; public bool Error, Slow, Reply; public int Id; }
    readonly List<Spark> sparks = new();
    readonly List<(PVector Pos, PVector Vel, float Born, Color C)> debris = new();
    readonly float[,] linkTraffic = new float[7, 7];
    readonly PVector[] dust = new PVector[900];
    int nextSparkId;

    // ---- the runtime ----------------------------------------------------------------------
    SignalRFeed hub;
    JSONObject metrics, chaos;
    int lastGen2 = -1, lastGen0 = -1;
    float gen0Flash, shockwaveAt = -100;
    int queued, busy, threads;

    // ---- time: scene time can slow down (the GC stopping the world) ---------------------
    float sceneT, timeScale = 1, slowUntil = -10;

    // ---- the monitor ------------------------------------------------------------------------
    readonly float[] ecg = new float[700];
    readonly bool[] ecgRed = new bool[700];
    float ecgCursor;
    readonly Queue<(float At, bool Red)> beats = new();
    int served;
    float lastBeep = -10, lastBuzz = -10, lastBoom = -10, lastHum = -10, lastFlat = -10;
    float zeroSince = -1;
    bool flatline;

    // ---- the director -------------------------------------------------------------------------
    enum Shot { Wide, FlyThrough, Ride, Crane, CloseUp }
    Shot shot = Shot.Wide;
    int shotSubject = 1;
    float shotStart, shotLength = 9;
    int scheduleIndex;
    bool director = true;
    float manualUntil = -10, manualYaw, manualPitch = 0.3f, zoom = 1;
    PVector camEye = new(0, 30, -140), camTarget = new(0, 0, 0);
    float camFov = 50;
    int ridingId = -1;
    float shake;

    PFont font, mono, big;
    readonly PointCloud cloud = new(), haze = new(), lights = new();
    View3D view;
    string capTitle = """", capBody = """"; float capAt = -10;
    int selected = 5;
    bool simulate, muted;
    float startedAt = -1;

    public override void Setup()
    {
        Size(1280, 720);
        font = CreateFont(""Segoe UI"", 14);
        mono = CreateFont(""Consolas"", 14);
        big = CreateFont(""Segoe UI Semibold"", 40);
        TextSizeSnap(1);
        cloud.Glow = true;
        haze.Glow = true;
        haze.DepthSort = false;
        // Sparks are sized in pixels: crisp at any distance, and cheap however many there are.
        lights.Glow = true;
        lights.DepthSort = false;
        lights.SizeInWorld = false;
        // In depth, the way a request travels: the internet, the gateway, a ring of four services, the database.
        services[0].Pos = new PVector(0, 2, -52);    services[0].Color = Color(150, 160, 210);
        services[1].Pos = new PVector(0, 0, -28);    services[1].Color = Color(70, 190, 255);
        services[2].Pos = new PVector(0, 14, -4);    services[2].Color = Color(90, 235, 150);
        services[3].Pos = new PVector(14, 0, -2);    services[3].Color = Color(255, 205, 70);
        services[4].Pos = new PVector(0, -14, 0);    services[4].Color = Color(180, 120, 255);
        services[5].Pos = new PVector(-14, 0, 2);    services[5].Color = Color(255, 120, 90);
        services[6].Pos = new PVector(0, -4, 26);    services[6].Color = Color(90, 170, 255);
        for (int i = 0; i < dust.Length; i++)
            dust[i] = new PVector(Random(-120, 120), Random(-60, 70), Random(-140, 120));
        hub = ConnectSignalR(HubUrl);
    }

    float Now => Millis() / 1000f;

    // =====================================================================================
    // What arrives
    // =====================================================================================
    void Read()
    {
        while (hub.Next(out var msg))
        {
            if (msg.Target == ""hops"")
            {
                var list = msg.Arguments.GetJSONArray(0);
                int n = list.Size();
                for (int i = 0; i < n; i++)
                {
                    var h = list.GetJSONObject(i);
                    Hop(h.GetString(""from""), h.GetString(""to""), h.GetFloat(""ms""), h.GetInt(""status""), h.GetBoolean(""root""), i / (float)n * 0.1f);
                }
            }
            else if (msg.Target == ""metrics"") Metrics(msg.Object(0));
            else if (msg.Target == ""chaos"") Chaos(msg.Object(0));
        }
    }

    void Hop(string from, string to, float ms, int status, bool root, float delay)
    {
        int a = Index(from), b = Index(to);
        if (a < 0 || b < 0) return;
        var s = services[b];
        s.Count++; s.LatencySum += ms;
        bool error = status >= 500;
        if (error) s.ErrorCount++;
        linkTraffic[a, b] += 1;
        if (root)
        {
            if (error) Buzz();
            else if (++served >= ServedPerBeep) { served = 0; beats.Enqueue((Now + delay, false)); }
        }
        if (sparks.Count > 1400) return;                         // more than the eye can follow
        float duration = Math.Clamp(0.35f + MathF.Sqrt(ms) * 0.1f, 0.35f, 4.5f);
        sparks.Add(new Spark { From = a, To = b, Start = sceneT + delay, Duration = duration, Error = error, Slow = ms > 250, Id = nextSparkId++ });
    }

    void Metrics(JSONObject m)
    {
        metrics = m;
        int g0 = m.GetInt(""gen0""), g2 = m.GetInt(""gen2"");
        if (lastGen0 >= 0 && g0 > lastGen0) gen0Flash = 1;
        if (lastGen2 >= 0 && g2 > lastGen2) GcStopsTheWorld();
        lastGen0 = g0; lastGen2 = g2;
        queued = m.GetInt(""threadPoolQueue""); busy = m.GetInt(""busyThreads""); threads = m.GetInt(""threads"");
        float rps = m.GetFloat(""rps"");
        if (rps < 0.5f && m.GetInt(""load"") > 0) { if (zeroSince < 0) zeroSince = Now; }
        else zeroSince = -1;
        bool was = flatline;
        flatline = zeroSince >= 0 && Now - zeroSince > 0.8f;
        if (flatline && !was)
        {
            Caption(""Flatline"", ""Nothing is being served. The process is alive -- every thread is just waiting."");
            Cut(Shot.CloseUp, 1, 10);
        }
    }

    /// <summary>A full GC: a shockwave, a boom, and the scene slows down for a moment -- the world stopped.</summary>
    void GcStopsTheWorld()
    {
        shockwaveAt = sceneT;
        shake += 0.8f;
        slowUntil = Now + 0.9f;
        Boom();
    }

    // =====================================================================================
    // Chaos: what changed, told as it happens (from the keyboard here, or a phone)
    // =====================================================================================
    void Chaos(JSONObject next)
    {
        var prev = chaos;
        chaos = next;
        if (prev == null) return;
        var lat = next.GetJSONObject(""latency""); var pLat = prev.GetJSONObject(""latency"");
        var fail = next.GetJSONObject(""failure""); var pFail = prev.GetJSONObject(""failure"");
        for (int i = 0; i < services.Length; i++)
        {
            var s = services[i];
            int ms = lat != null && lat.HasKey(s.Name) ? lat.GetInt(s.Name) : 0, pms = pLat != null && pLat.HasKey(s.Name) ? pLat.GetInt(s.Name) : 0;
            if (ms > pms) { Cut(Shot.CloseUp, i, 9); Caption($""{s.Name} +{ms} ms"", $""Every call to {s.Name} now waits. Watch its sparks crawl -- and everything that calls it slow down with it.""); }
            float f = fail != null && fail.HasKey(s.Name) ? fail.GetFloat(s.Name) : 0, pf = pFail != null && pFail.HasKey(s.Name) ? pFail.GetFloat(s.Name) : 0;
            if (f > pf) { Cut(Shot.CloseUp, i, 9); Caption($""{s.Name} fails {f:P0}"", ""One failure cascades: the gateway turns it into an error for the customer. Listen for the buzz.""); }
        }
        if (next.GetBoolean(""starve"") && !prev.GetBoolean(""starve""))
        {
            Cut(Shot.CloseUp, 1, 11);
            Caption(""Thread pool starvation"", ""inventory blocks on Task.Wait(): each request holds a thread for 300 ms. The pool adds about one thread a second -- the rest of the work waits at the door. Listen to the hum rise."");
        }
        if (next.GetBoolean(""gcStorm"") && !prev.GetBoolean(""gcStorm""))
        {
            Cut(Shot.Wide, 1, 9);
            Caption(""GC storm"", ""Lots of short-lived garbage on every query. Gen 0 collects constantly; each full gen 2 collection stops the world -- and the film slows down with it."");
        }
        if (next.GetBoolean(""spike"") && !prev.GetBoolean(""spike"")) { Cut(Shot.FlyThrough, 1, 9); Caption(""Traffic spike ×8"", ""Eight times the customers for eight seconds. Listen to the heart race.""); }
        bool calm = (lat == null || lat.Size() == 0) && (fail == null || fail.Size() == 0) && !next.GetBoolean(""starve"") && !next.GetBoolean(""gcStorm"");
        bool wasCalm = (pLat == null || pLat.Size() == 0) && (pFail == null || pFail.Size() == 0) && !prev.GetBoolean(""starve"") && !prev.GetBoolean(""gcStorm"");
        if (calm && !wasCalm) { Cut(Shot.Wide, 1, 10); Caption(""Back to normal"", ""...and now see how long it really takes to recover.""); }
    }

    void Caption(string title, string body) { capTitle = title; capBody = body; capAt = Now; }

    // =====================================================================================
    // The sound: a heart monitor
    // =====================================================================================
    void Beep(bool red) { lastBeep = Now; if (!muted) Play(red ? 77 : 83, amp: 0.16, attack: 0.002, release: 0.13, synth: Synth.Sine); }

    void Buzz()
    {
        if (Now - lastBuzz < 0.25f) return;
        lastBuzz = Now;
        shake += 0.2f;
        if (!muted) Play(38, amp: 0.13, release: 0.22, cutoff: 75, synth: Synth.Saw);
    }

    void Boom()
    {
        lastBoom = Now;
        if (muted) return;
        Play(24, amp: 0.55, release: 0.5, synth: Synth.Kick);
        Play(31, amp: 0.2, attack: 0.01, release: 1.4, synth: Synth.Sine);
    }

    void Sound()
    {
        while (beats.Count > 0 && beats.Peek().At <= Now)
        {
            var (_, red) = beats.Dequeue();
            if (Now - lastBeep > 0.1f) { Beep(red); Beat(red); }
        }
        if (queued > 5 && Now - lastHum > 0.22f)
        {
            lastHum = Now;
            float k = Math.Min(1, MathF.Log10(queued) / 2.6f);
            if (!muted) Play(36 + k * 22, amp: 0.05 + 0.2 * k, attack: 0.08, sustain: 0.2, release: 0.15, cutoff: 45 + 40 * k, synth: Synth.Saw);
        }
        if (flatline && Now - lastFlat > 0.42f)
        {
            lastFlat = Now;
            if (!muted) Play(83, amp: 0.13, attack: 0.01, sustain: 0.42, release: 0.03, synth: Synth.Sine);
        }
    }

    // =====================================================================================
    // Each frame
    // =====================================================================================
    public override void Draw()
    {
        if (startedAt < 0) { startedAt = Now; shotStart = Now; }
        float dt = 1 / 60f;
        // Slow motion when the GC stops the world, easing back.
        float targetScale = Now < slowUntil ? 0.22f : 1;
        timeScale = Lerp(timeScale, targetScale, Now < slowUntil ? 0.3f : 0.06f);
        sceneT += dt * timeScale;

        Read();
        if (simulate && !hub.Connected) Simulate();
        Sound();
        foreach (var s in services)
        {
            float rate = s.Count / dt;
            s.Rate = Lerp(s.Rate, rate, 0.03f);
            if (s.Count > 0) s.Latency = Lerp(s.Latency, s.LatencySum / s.Count, 0.1f);
            s.Errors = Lerp(s.Errors, s.Count > 0 ? s.ErrorCount / (float)s.Count : s.Errors * 0.98f, 0.06f);
            s.Pulse = Math.Max(0, s.Pulse - dt * 2.5f);
            s.Count = 0; s.ErrorCount = 0; s.LatencySum = 0;
        }
        for (int a = 0; a < 7; a++) for (int b = 0; b < 7; b++) linkTraffic[a, b] *= 0.97f;
        gen0Flash *= 0.9f;
        shake *= 0.9f;

        view = Camera();
        Background(2, 3, 9);
        BlendMode(BlendModeKind.Add);
        Dust();
        Links();
        cloud.Clear();
        lights.Clear();
        Spheres();
        ThreadPool();
        Shockwave();
        SparksAndTrails();
        Debris();
        Points(cloud, view);
        Points(lights, view);
        BlendMode(BlendModeKind.Blend);
        Labels();
        if (flatline) Alarm();
        Letterbox();
        Monitor();
        Vitals();
        Legend();
        Captions();
        Title();
    }

    // =====================================================================================
    // The director
    // =====================================================================================
    readonly (Shot Shot, int Subject, float Length)[] schedule =
    {
        (Shot.Wide, 1, 9), (Shot.FlyThrough, 1, 9), (Shot.Ride, 1, 7), (Shot.CloseUp, 1, 7),
        (Shot.Crane, 6, 8), (Shot.CloseUp, 6, 7), (Shot.Ride, 1, 7), (Shot.CloseUp, 5, 7),
    };

    void Cut(Shot s, int subject, float length)
    {
        shot = s; shotSubject = subject; shotStart = Now; shotLength = length; ridingId = -1;
    }

    /// <summary>
    /// Where the camera wants to be for the current shot. The real camera glides toward it,
    /// so one shot flows into the next.
    /// </summary>
    (PVector Eye, PVector Target, float Fov) ShotCamera(float t)
    {
        var center = new PVector(0, 0, -12);
        var subject = services[shotSubject].Pos;
        switch (shot)
        {
            case Shot.Wide:
            {
                // From the side, so a request crosses the frame left to right, slowly turning.
                float yaw = 1.25f + t * 0.05f;
                return (new PVector(center.X + Sin(yaw) * 70, center.Y + 22, center.Z - Cos(yaw) * 70), new PVector(0, -2, -12), 48);
            }
            case Shot.FlyThrough:
            {
                // Down the length of the app, from behind the internet to past the database.
                float u = Math.Clamp(t / shotLength, 0, 1);
                float z = Lerp(-90, -4, u * u * (3 - 2 * u));
                var eye = new PVector(9 + Sin(t * 0.4f) * 4, 6 + Cos(t * 0.3f) * 3, z);
                return (eye, new PVector(0, 0, Math.Min(z + 34, 26)), 62);
            }
            case Shot.Ride:
            {
                // Follow one customer request, from the door to wherever it goes.
                var s = Riding();
                if (s == null) goto case Shot.Wide;
                var p = SparkPos(s.Value, 0);
                var ahead = SparkPos(s.Value, 0.2f);
                var dir = PVector.Sub(ahead, p);
                if (dir.Mag() < 1e-3f) dir = new PVector(0, 0, 1);
                dir.Normalize();
                var eye = new PVector(p.X - dir.X * 7, p.Y - dir.Y * 7 + 2.5f, p.Z - dir.Z * 7);
                return (eye, PVector.Add(p, PVector.Mult(dir, 10)), 70);
            }
            case Shot.Crane:
            {
                // From under the database, rising and looking back up the app.
                float u = Math.Clamp(t / shotLength, 0, 1);
                var eye = new PVector(-20 + u * 14, Lerp(-34, 18, u), 48 - u * 10);
                return (eye, new PVector(0, 0, -14), 55);
            }
            default:
            {
                float yaw = 2.2f + t * 0.16f + shotSubject;
                float dist = shotSubject == 6 ? 22 : shotSubject == 1 ? 26 : 16;
                var eye = new PVector(subject.X + Sin(yaw) * dist, subject.Y + 4.5f, subject.Z - Cos(yaw) * dist);
                return (eye, subject, 52);
            }
        }
    }

    Spark? Riding()
    {
        // Keep riding the same spark while it flies; then pick the newest root one.
        foreach (var s in sparks) if (s.Id == ridingId) return s;
        Spark? best = null;
        foreach (var s in sparks)
            if (!s.Reply && s.From == 0 && sceneT - s.Start < 0.15f && sceneT >= s.Start) { best = s; }
        if (best == null) foreach (var s in sparks) if (!s.Reply && s.From == 1) { best = s; break; }
        ridingId = best?.Id ?? -1;
        return best;
    }

    View3D Camera()
    {
        if (director && Now > manualUntil)
        {
            float t = Now - shotStart;
            if (t > shotLength)
            {
                var next = schedule[scheduleIndex++ % schedule.Length];
                Cut(next.Shot, next.Subject, next.Length);
                t = 0;
            }
            var (eye, target, fov) = ShotCamera(t);
            float intro = Math.Clamp((Now - startedAt) / 4, 0, 1);
            float k = shot == Shot.Ride ? 0.18f : 0.035f + 0.04f * (1 - intro);
            camEye = PVector.Lerp(camEye, eye, k);
            camTarget = PVector.Lerp(camTarget, target, shot == Shot.Ride ? 0.25f : 0.05f);
            camFov = Lerp(camFov, fov, 0.04f);
        }
        else
        {
            var orbit = View3D.Orbit(manualYaw, manualPitch, 80 / zoom, new PVector(0, 0, -12), 50);
            camEye = PVector.Lerp(camEye, orbit.Eye, 0.12f);
            camTarget = PVector.Lerp(camTarget, orbit.Target, 0.12f);
            camFov = Lerp(camFov, 50, 0.1f);
        }
        var v = new View3D
        {
            Eye = new PVector(camEye.X + Random(-1, 1) * shake, camEye.Y + Random(-1, 1) * shake, camEye.Z),
            Target = camTarget,
            FovY = camFov / (director ? zoom : 1),
        };
        return v;
    }

    // =====================================================================================
    // Depth: depth of field and fog on every point
    // =====================================================================================

    /// <summary>
    /// Adds a point the way a film camera sees it: in focus at the subject's distance, growing
    /// into a soft, faint disc nearer or farther (bokeh), and fading into the fog far away.
    /// </summary>
    void Add(PointCloud pc, float x, float y, float z, Color c, float size)
    {
        float dx = x - view.Eye.X, dy = y - view.Eye.Y, dz = z - view.Eye.Z;
        float d = MathF.Sqrt(dx * dx + dy * dy + dz * dz);
        float focus = PVector.Dist(view.Eye, view.Target);
        if (d < 4) return;                                   // right at the lens: skip it
        float blur = Math.Clamp(Math.Abs(d - focus) / Math.Max(8, focus) * 1.8f, 0, 2);
        float fog = Math.Clamp(1 - (d - 70) / 110, 0.12f, 1);
        float a = c.A * fog / (1 + blur * 1.6f);
        if (a < 3) return;
        // Bokeh, but never discs so big they cost more than they show.
        pc.Add(x, y, z, Color(c.R, c.G, c.B, a), Math.Min(size * (1 + blur * 1.3f), Math.Max(size, d * 0.05f)));
    }

    /// <summary>The same for a light whose size is in pixels: out of focus it only dims and softens a little.</summary>
    void AddLight(float x, float y, float z, Color c, float px)
    {
        float dx = x - view.Eye.X, dy = y - view.Eye.Y, dz = z - view.Eye.Z;
        float d = MathF.Sqrt(dx * dx + dy * dy + dz * dz);
        if (d < 2) return;
        float focus = PVector.Dist(view.Eye, view.Target);
        float blur = Math.Clamp(Math.Abs(d - focus) / Math.Max(8, focus) * 1.8f, 0, 2);
        float fog = Math.Clamp(1 - (d - 70) / 110, 0.12f, 1);
        // Nearer lights look bigger, as they would.
        float near = Math.Clamp(40 / d, 0.6f, 3);
        lights.Add(x, y, z, Color(c.R, c.G, c.B, c.A * fog / (1 + blur)), Math.Min(px * near * (1 + blur * 0.6f), 40));
    }

    void Dust()
    {
        haze.Clear();
        for (int i = 0; i < dust.Length; i++)
        {
            var p = dust[i];
            float drift = sceneT * (0.4f + (i % 7) * 0.08f);
            float z = ((p.Z + drift + 140) % 260) - 140;
            Add(haze, p.X, p.Y, z, i % 9 == 0 ? Color(120, 160, 255, 90) : Color(90, 110, 170, 60), 0.35f);
        }
        Points(haze, view);
    }

    PVector Curve(int a, int b, float t)
    {
        var A = services[a].Pos; var B = services[b].Pos;
        float d = PVector.Dist(A, B);
        // Bow each path outward from the app's long axis, so paths don't cross through each other.
        var mid = new PVector((A.X + B.X) / 2, (A.Y + B.Y) / 2, (A.Z + B.Z) / 2);
        var outward = new PVector(mid.X, mid.Y + 0.01f, 0);
        if (outward.Mag() < 2) outward = new PVector(0, 1, 0);
        outward.Normalize();
        var C = PVector.Add(mid, PVector.Mult(outward, d * 0.22f));
        float u = 1 - t;
        return new PVector(u * u * A.X + 2 * u * t * C.X + t * t * B.X, u * u * A.Y + 2 * u * t * C.Y + t * t * B.Y, u * u * A.Z + 2 * u * t * C.Z + t * t * B.Z);
    }

    PVector SparkPos(Spark s, float ahead)
    {
        float t = Math.Clamp((sceneT - s.Start) / s.Duration + ahead, 0, 1);
        float e = t * t * (3 - 2 * t);
        return Curve(s.From, s.To, e);
    }

    void Links()
    {
        StrokeWeight(1);
        for (int a = 0; a < 7; a++)
            for (int b = 0; b < 7; b++)
            {
                float k = Math.Min(1, linkTraffic[a, b] / 40);
                if (k < 0.01f) continue;
                var c = services[b].Color;
                Stroke(c.R * 0.5f, c.G * 0.5f, c.B * 0.5f, 14 + 40 * k);
                var segs = new List<(float, float, float, float)>();
                var prev = view.Project(Curve(a, b, 0), Width, Height);
                for (int i = 1; i <= 28; i++)
                {
                    var p = view.Project(Curve(a, b, i / 28f), Width, Height);
                    if (prev.Depth > 0.5f && p.Depth > 0.5f) segs.Add((prev.X, prev.Y, p.X, p.Y));
                    prev = p;
                }
                Lines(segs);
            }
    }

    /// <summary>Each service: a lit sphere of points (bright on the key-light side, a rim of light at its edge) around a glowing core.</summary>
    void Spheres()
    {
        var light = new PVector(-0.5f, 0.7f, -0.5f);
        light.Normalize();
        var toEye = PVector.Sub(view.Eye, view.Target);
        toEye.Normalize();
        for (int i = 0; i < services.Length; i++)
        {
            var s = services[i];
            bool isDb = s.Name == ""db"", isNet = s.Name == ""internet"";
            float r = (isDb ? 4.2f : isNet ? 2.4f : 2.8f) + MathF.Sqrt(s.Rate) * 0.12f + s.Pulse * 0.6f;
            float heat = Math.Clamp((s.Latency - 30) / 300, 0, 1);
            float err = Math.Clamp(s.Errors * 3, 0, 1);
            int count = isNet ? 160 : isDb ? 420 : 300;
            var c = s.Color;
            float R = Lerp(Lerp(c.R, 255, heat), 255, err), G = Lerp(Lerp(c.G, 140, heat), 50, err), B = Lerp(Lerp(c.B, 40, heat), 50, err);
            Add(cloud, s.Pos.X, s.Pos.Y, s.Pos.Z, Color(R, G, B, 60 + 70 * s.Pulse), r * 2.4f);
            for (int k = 0; k < count; k++)
            {
                float y = 1 - 2 * (k + 0.5f) / count, rad = MathF.Sqrt(1 - y * y), th = k * 2.39996f + sceneT * (0.25f + 0.05f * i);
                var n = new PVector(Cos(th) * rad, y, Sin(th) * rad);
                float wobble = 1 + 0.07f * Sin(k * 1.7f + sceneT * 3) * Math.Min(1, s.Rate / 30);
                float lit = 0.25f + 0.75f * Math.Max(0, n.X * light.X + n.Y * light.Y + n.Z * light.Z);
                float rim = MathF.Pow(1 - Math.Abs(n.X * toEye.X + n.Y * toEye.Y + n.Z * toEye.Z), 3);
                bool flicker = err > 0.05f && Hash(k + (int)(Now * 12)) < err * 0.5f;
                float a = (i == selected ? 210 : 160) * (lit * 0.8f + rim * 0.9f) * (flicker ? 0.25f : 1);
                Add(cloud, s.Pos.X + n.X * r * wobble, s.Pos.Y + n.Y * r * wobble, s.Pos.Z + n.Z * r * wobble, Color(R, G, B, Math.Min(255, a)), 0.42f);
            }
            if (ChaosText(s.Name) != """")
                for (int k = 0; k < 140; k++)
                {
                    float th = k / 140f * TWO_PI + sceneT * 1.5f;
                    Add(cloud, s.Pos.X + Cos(th) * (r + 1.6f), s.Pos.Y + Sin(th * 3 + sceneT) * 0.3f, s.Pos.Z + Sin(th) * (r + 1.6f), Color(255, 110, 50, 170), 0.38f);
                }
        }
    }

    /// <summary>The thread pool: a ring of threads around the gateway that every request passes through, lit when busy; work waiting for one coils back toward the internet.</summary>
    void ThreadPool()
    {
        var g = services[1].Pos;
        int n = Math.Min(threads, 120);
        for (int i = 0; i < n; i++)
        {
            float th = i / (float)Math.Max(1, n) * TWO_PI + sceneT * 0.5f;
            float ring = 6.5f + (i % 2) * 0.6f;
            bool isBusy = i < busy;
            Add(cloud, g.X + Cos(th) * ring, g.Y + Sin(th) * ring, g.Z - 2, isBusy ? Color(255, 190, 70, 240) : Color(90, 110, 160, 130), isBusy ? 0.8f : 0.5f);
        }
        int q = Math.Min(queued, 360);
        for (int k = 0; k < q; k++)
        {
            float s = k * 0.12f;
            float th = s * 2.0f - sceneT * 1.5f;
            float coil = 2.2f + s * 0.05f;
            Add(cloud, g.X + Cos(th) * coil, g.Y + Sin(th) * coil, g.Z - 4 - s * 0.9f, Color(255, 60, 50, 210), 0.55f);
        }
    }

    void SparksAndTrails()
    {
        var ok = new List<(float, float, float, float)>(); var slow = new List<(float, float, float, float)>(); var bad = new List<(float, float, float, float)>();
        for (int i = sparks.Count - 1; i >= 0; i--)
        {
            var s = sparks[i];
            float t = (sceneT - s.Start) / s.Duration;
            if (t < 0) continue;
            if (t >= 1)
            {
                sparks.RemoveAt(i);
                if (s.Reply) continue;
                services[s.To].Pulse = Math.Min(1, services[s.To].Pulse + 0.06f);
                if (s.Error) Burst(services[s.To].Pos);
                if (sparks.Count < 1100)
                    sparks.Add(new Spark { From = s.To, To = s.From, Start = sceneT, Duration = Math.Max(0.25f, s.Duration * 0.5f), Error = s.Error, Reply = true, Id = nextSparkId++ });
                continue;
            }
            var p = SparkPos(s, 0);
            var c = s.Error ? Color(255, 60, 50) : s.Slow ? Color(255, 160, 40) : services[s.Reply ? s.From : s.To].Color;
            float a = s.Reply ? 0.45f : 1;
            bool ridden = s.Id == ridingId && shot == Shot.Ride;
            AddLight(p.X, p.Y, p.Z, Color(Math.Min(255, c.R + 80), Math.Min(255, c.G + 80), Math.Min(255, c.B + 80), 255 * a), ridden ? 14 : s.Error ? 9 : 6);
            if (!s.Reply && (sparks.Count < 700 || ridden || s.Error)) AddLight(p.X, p.Y, p.Z, Color(c.R, c.G, c.B, ridden ? 120 : 45), ridden ? 40 : s.Error ? 22 : 14);
            if (s.Reply) continue;
            var q = SparkPos(s, -0.1f);
            var P = view.Project(p, Width, Height); var Q = view.Project(q, Width, Height);
            if (P.Depth > 0.5f && Q.Depth > 0.5f) (s.Error ? bad : s.Slow ? slow : ok).Add((P.X, P.Y, Q.X, Q.Y));
        }
        StrokeWeight(1.5f); Stroke(110, 190, 255, 110); Lines(ok);
        StrokeWeight(2); Stroke(255, 160, 40, 160); Lines(slow);
        StrokeWeight(2.5f); Stroke(255, 60, 50, 200); Lines(bad);
    }

    void Lines(List<(float X0, float Y0, float X1, float Y1)> lines)
    {
        if (lines.Count == 0) return;
        BeginShape(ShapeKind.Lines);
        foreach (var (x0, y0, x1, y1) in lines) { Vertex(x0, y0); Vertex(x1, y1); }
        EndShape();
    }

    void Burst(PVector at)
    {
        for (int k = 0; k < 18; k++)
        {
            var v = new PVector(Random(-1, 1), Random(-1, 1), Random(-1, 1));
            v.Normalize(); v.Mult(Random(4, 11));
            debris.Add((at.Copy(), v, sceneT, Color(255, Random(50, 130), 40)));
        }
        if (debris.Count > 3000) debris.RemoveRange(0, debris.Count - 3000);
    }

    void Debris()
    {
        for (int i = debris.Count - 1; i >= 0; i--)
        {
            var (p, v, born, c) = debris[i];
            float age = sceneT - born;
            if (age > 0.9f) { debris.RemoveAt(i); continue; }
            Add(cloud, p.X + v.X * age, p.Y + v.Y * age - 4 * age * age, p.Z + v.Z * age, Color(c.R, c.G, c.B, 255 * (1 - age / 0.9f)), 0.45f);
        }
    }

    /// <summary>A gen 2 garbage collection: a sphere of light expanding through the whole app.</summary>
    void Shockwave()
    {
        float age = sceneT - shockwaveAt;
        if (age > 1.4f || age < 0) return;
        float r = age * 50, a = 1 - age / 1.4f;
        var center = new PVector(0, 0, -6);
        int n = 500;
        for (int k = 0; k < n; k++)
        {
            float y = 1 - 2 * (k + 0.5f) / n, rad = MathF.Sqrt(1 - y * y), th = k * 2.39996f;
            Add(cloud, center.X + Cos(th) * rad * r, center.Y + y * r, center.Z + Sin(th) * rad * r, Color(190, 150, 255, 160 * a), 0.9f);
        }
    }

    void Labels()
    {
        TextAlign(TextAlignH.Center, TextAlignV.Top);
        float focus = PVector.Dist(view.Eye, view.Target);
        for (int i = 0; i < services.Length; i++)
        {
            var s = services[i];
            var p = view.Project(new PVector(s.Pos.X, s.Pos.Y - (s.Name == ""db"" ? 5.4f : 4.0f), s.Pos.Z), Width, Height);
            if (p.Depth <= 1 || p.Y < Bar || p.Y > Height - Bar - 20) continue;
            // Labels fade with the depth of field, like everything else.
            float d = PVector.Dist(view.Eye, s.Pos);
            float a = Math.Clamp(1.4f - Math.Abs(d - focus) / Math.Max(10, focus) * 1.4f, 0.15f, 1);
            TextFont(font, 15);
            Fill(i == selected ? Color(255, 255, 255, 255 * a) : Color(s.Color.R, s.Color.G, s.Color.B, 230 * a));
            Text((i >= 1 ? $""{i}  "" : """") + s.Name, p.X, p.Y);
            if (s.Name == ""internet"") continue;
            TextFont(mono, 12);
            Fill(170, 185, 215, 190 * a);
            string err = s.Errors > 0.01f ? $"" · {s.Errors:P0} err"" : """";
            Text($""{s.Rate:0}/s · {s.Latency:0} ms{err}"", p.X, p.Y + 19);
            string c = ChaosText(s.Name);
            if (c != """") { Fill(255, 140, 80, 255 * a); Text(c, p.X, p.Y + 35); }
        }
        var g = view.Project(new PVector(services[1].Pos.X, services[1].Pos.Y + 8.5f, services[1].Pos.Z), Width, Height);
        if (g.Depth > 1 && threads > 0 && g.Y > Bar && g.Y < Height - Bar)
        {
            TextFont(mono, 11);
            Fill(255, 200, 110, 200);
            Text($""thread pool  {busy}/{threads} busy"" + (queued > 0 ? $"" · {queued} waiting"" : """"), g.X, g.Y);
        }
    }

    string ChaosText(string name)
    {
        if (chaos == null) return """";
        var parts = new List<string>();
        var lat = chaos.GetJSONObject(""latency""); var fail = chaos.GetJSONObject(""failure"");
        if (lat != null && lat.HasKey(name)) parts.Add($""+{lat.GetInt(name)} ms"");
        if (fail != null && fail.HasKey(name)) parts.Add($""fails {fail.GetFloat(name):P0}"");
        if (name == ""inventory"" && chaos.GetBoolean(""starve"")) parts.Add(""Task.Wait() !"");
        if (name == ""db"" && chaos.GetBoolean(""gcStorm"")) parts.Add(""allocating like crazy"");
        return string.Join("" · "", parts);
    }

    // =====================================================================================
    // The frame: letterbox, monitor, vitals, legend, captions
    // =====================================================================================
    void Letterbox()
    {
        NoStroke();
        Fill(0);
        Rect(0, 0, Width, Bar);
        Rect(0, Height - Bar, Width, Bar);
        // A soft vignette into the bars.
        for (int i = 0; i < 16; i++)
        {
            Fill(0, 0, 0, 14 * (16 - i) / 16f * 6);
            Rect(0, Bar + i * 2, Width, 2);
            Rect(0, Height - Bar - (i + 1) * 2, Width, 2);
        }
    }

    void Monitor()
    {
        float x0 = 24, y0 = Height - Bar + 10, w = 470, h = Bar - 22;
        float speed = 120;
        int before = (int)ecgCursor;
        ecgCursor = (ecgCursor + speed / 60f) % ecg.Length;
        for (int i = before; i != (int)ecgCursor; i = (i + 1) % ecg.Length) { ecgRed[i] = false; ecg[i] = Wave(i); }
        NoFill();
        StrokeWeight(1.6f);
        int gap = 14;
        bool open = false, openRed = false;
        for (int i = 0; i < ecg.Length; i++)
        {
            int dist = (i - (int)ecgCursor + ecg.Length) % ecg.Length;
            bool red = ecgRed[i] || flatline;
            if (dist < gap || (open && red != openRed)) { if (open) { EndShape(); open = false; } if (dist < gap) continue; }
            if (!open)
            {
                Stroke(red ? Color(255, 70, 60, 230) : Color(60, 255, 150, 230));
                BeginShape();
                if (i > 0) Vertex(x0 + (i - 1) / (float)ecg.Length * w, y0 + h / 2 - ecg[i - 1] * h * 0.45f);
                open = true; openRed = red;
            }
            Vertex(x0 + i / (float)ecg.Length * w, y0 + h / 2 - ecg[i] * h * 0.45f);
        }
        if (open) EndShape();
        int c = ((int)ecgCursor - 1 + ecg.Length) % ecg.Length;
        NoStroke();
        Fill(flatline ? Color(255, 90, 80) : Color(120, 255, 180));
        Circle(x0 + c / (float)ecg.Length * w, y0 + h / 2 - ecg[c] * h * 0.45f, 5);
        TextFont(mono, 10);
        TextAlign(TextAlignH.Left, TextAlignV.Top);
        Fill(flatline ? Color(255, 100, 90) : Color(80, 230, 140, 170));
        Text(flatline ? ""NO REQUESTS SERVED"" : $""1 beat = {ServedPerBeep} requests served"", x0, Height - Bar + 2);
    }

    int beatStart = -1000; bool beatRed;
    float Wave(int i)
    {
        int k = (i - beatStart + ecg.Length) % ecg.Length;
        if (k > 34) return (Noise(i * 0.2f, Now) - 0.5f) * 0.04f;
        ecgRed[i] = beatRed;
        if (k < 5) return Sin(k / 5f * PI) * 0.12f;
        if (k < 8) return 0;
        if (k == 8) return -0.15f;
        if (k == 9) return 1f;
        if (k == 10) return -0.35f;
        if (k < 17) return 0;
        if (k < 29) return Sin((k - 17) / 12f * PI) * 0.22f;
        return 0;
    }

    void Beat(bool red) { beatStart = (int)ecgCursor; beatRed = red; }

    void Vitals()
    {
        if (metrics == null) return;
        float x = 520, y = Height - Bar + 6;
        float rps = metrics.GetFloat(""rps""), p99 = metrics.GetFloat(""p99""), err = metrics.GetFloat(""errorRate""), heap = metrics.GetFloat(""heapMB"");
        void Vital(float vx, string label, string value, Color c)
        {
            TextAlign(TextAlignH.Left, TextAlignV.Top);
            TextFont(mono, 10); Fill(c.R, c.G, c.B, 170); Text(label, vx, y);
            TextFont(big, 26); Fill(c); Text(value, vx, y + 12);
        }
        Vital(x, ""REQ/S"", $""{rps:0}"", flatline ? Color(255, 80, 70) : Color(60, 255, 150));
        Vital(x + 80, ""P99 MS"", $""{p99:0}"", p99 > 500 ? Color(255, 160, 50) : Color(110, 200, 255));
        Vital(x + 175, ""ERRORS"", $""{err * 100:0.#}%"", err > 0.02f ? Color(255, 80, 70) : Color(200, 210, 235));
        Vital(x + 265, ""HEAP MB"", $""{heap:0}"", Color(190, 150, 255));
        Vital(x + 350, ""GC2"", $""{lastGen2}"", Now - lastBoom < 1 ? Color(255, 255, 255) : Color(190, 150, 255));
    }

    void Legend()
    {
        float x = Width - 330, y = Height - Bar + 6;
        (string Sound, string Means, float Last)[] rows =
        {
            (""beep"", $""{ServedPerBeep} requests served"", lastBeep), (""buzz"", ""a request failed"", lastBuzz), (""boom"", ""full GC (gen 2)"", lastBoom),
            (""hum"", ""work waiting for a thread"", lastHum), (""flatline"", ""nothing served"", lastFlat),
        };
        TextFont(mono, 10);
        TextAlign(TextAlignH.Left, TextAlignV.Top);
        for (int i = 0; i < rows.Length; i++)
        {
            float lit = Math.Clamp(1 - (Now - rows[i].Last) / 0.35f, 0, 1);
            float rx = x + (i < 3 ? 0 : 168), ry = y + (i % 3) * 17;
            NoStroke();
            Fill(255, 220, 120, 40 + 215 * lit);
            Circle(rx + 4, ry + 6, 4 + 4 * lit);
            Fill(255, 255, 255, 100 + 155 * lit);
            Text($""{rows[i].Sound,-9}{rows[i].Means}"", rx + 13, ry);
        }
        if (muted) { Fill(255, 255, 255, 90); Text(""muted (M)"", x + 168, y + 34); }
    }

    /// <summary>Subtitles, like a film: the title of what's happening, and a line or two of why.</summary>
    void Captions()
    {
        float age = Now - capAt;
        if (age > 7) return;
        float a = Math.Clamp(Math.Min(age * 3, (7 - age) * 1.5f), 0, 1);
        var lines = new List<string>();
        var line = """";
        foreach (var word in capBody.Split(' '))
        {
            if (line.Length + word.Length > 100) { lines.Add(line); line = """"; }
            line = line == """" ? word : line + "" "" + word;
        }
        if (line != """") lines.Add(line);
        float y = Height - Bar - 28 - lines.Count * 21;
        TextAlign(TextAlignH.Center, TextAlignV.Bottom);
        TextFont(big, 30);
        Fill(0, 0, 0, 160 * a); Text(capTitle, Width / 2 + 2, y + 2);
        Fill(255, 225, 170, 255 * a); Text(capTitle, Width / 2, y);
        TextAlign(TextAlignH.Center, TextAlignV.Top);
        TextFont(font, 16);
        for (int i = 0; i < lines.Count; i++)
        {
            Fill(0, 0, 0, 170 * a); Text(lines[i], Width / 2 + 1, y + 7 + i * 21);
            Fill(235, 238, 250, 240 * a); Text(lines[i], Width / 2, y + 6 + i * 21);
        }
    }

    void Alarm()
    {
        float a = (Sin(Now * 6) * 0.5f + 0.5f) * 70;
        NoStroke();
        for (int i = 0; i < 14; i++)
        {
            Fill(255, 30, 30, a * (1 - i / 14f) * 0.35f);
            Rect(0, Bar + i * 4, Width, 4); Rect(0, Height - Bar - (i + 1) * 4, Width, 4);
            Rect(i * 4, Bar, 4, Height - 2 * Bar); Rect(Width - (i + 1) * 4, Bar, 4, Height - 2 * Bar);
        }
    }

    void Title()
    {
        TextAlign(TextAlignH.Left, TextAlignV.Center);
        TextFont(big, 24);
        Fill(235, 240, 255);
        Text(""Inside Kestrel"", 24, Bar / 2 - 6);
        TextFont(font, 12);
        Fill(150, 165, 200);
        string source = hub.Connected ? ""the vital signs of a live ASP.NET Core app · SignalR · .NET 10""
                      : simulate ? ""SIMULATED (PulseShop isn't running)""
                      : $""{HubUrl}: {hub.Status} -- start it: cd showcase/PulseShop && dotnet run (or D)"";
        Text(source, 24, Bar / 2 + 16);
        TextAlign(TextAlignH.Right, TextAlignV.Center);
        TextFont(font, 13);
        Fill(255, 255, 255, 130);
        Text(""made with Dana · creative coding in C#"", Width - 24, Bar / 2 - 8);
        TextFont(font, 11);
        Fill(255, 255, 255, 70);
        string cam = !director ? ""camera: yours (C: director)"" : Now < manualUntil ? ""camera: yours for a moment"" : $""shot: {ShotName()}"";
        Text($""{cam} · 1-6 pick · L latency · F fail · S starve · G GC · Space spike · R reset · M mute"", Width - 24, Bar / 2 + 12);
        if (chaos != null && chaos.GetBoolean(""spike""))
        {
            TextAlign(TextAlignH.Center, TextAlignV.Center);
            TextFont(big, 18);
            Fill(255, 210, 90, 150 + 100 * Sin(Now * 8));
            Text(""TRAFFIC SPIKE ×8"", Width / 2, Bar / 2);
        }
    }

    string ShotName() => shot switch
    {
        Shot.Wide => ""wide"", Shot.FlyThrough => ""fly-through"", Shot.Ride => ""riding a request"", Shot.Crane => ""crane"",
        _ => ""close-up: "" + services[shotSubject].Name,
    };

    static float Hash(int n)
    {
        unchecked
        {
            uint x = (uint)n * 747796405u + 2891336453u;
            x = ((x >> (int)((x >> 28) + 4)) ^ x) * 277803737u;
            return ((x >> 22) ^ x) / 4294967296f;
        }
    }

    // =====================================================================================
    // Breaking it
    // =====================================================================================
    public override void KeyPressed()
    {
        string name = services[selected].Name;
        if (Key >= '1' && Key <= '6') { selected = Key - '0'; Cut(Shot.CloseUp, selected, 8); }
        else if (Key == 'l' || Key == 'L')
        {
            var lat = chaos?.GetJSONObject(""latency"");
            int now = lat != null && lat.HasKey(name) ? lat.GetInt(name) : 0;
            hub.Invoke(""Latency"", name, now >= 800 ? 0 : now + 200);
        }
        else if (Key == 'f' || Key == 'F')
        {
            var fail = chaos?.GetJSONObject(""failure"");
            hub.Invoke(""Fail"", name, fail != null && fail.HasKey(name) ? 0.0 : 0.3);
        }
        else if (Key == 's' || Key == 'S') hub.Invoke(""Starve"", !(chaos?.GetBoolean(""starve"") ?? false));
        else if (Key == 'g' || Key == 'G') hub.Invoke(""GcStorm"", !(chaos?.GetBoolean(""gcStorm"") ?? false));
        else if (Key == ' ') hub.Invoke(""Spike"", 8);
        else if (Key == '+' || Key == '=') hub.Invoke(""SetLoad"", Math.Min((chaos?.GetInt(""load"") ?? 40) * 2, 1600));
        else if (Key == '-' || Key == '_') hub.Invoke(""SetLoad"", Math.Max(5, (chaos?.GetInt(""load"") ?? 40) / 2));
        else if (Key == 'r' || Key == 'R') hub.Invoke(""Reset"");
        else if (Key == 'm' || Key == 'M') muted = !muted;
        else if (Key == 'c' || Key == 'C') { director = !director; manualUntil = -10; }
        else if (Key == 'd' || Key == 'D') simulate = !simulate;
    }

    public override void MouseDragged()
    {
        // Dragging takes the camera for a while; the director gets it back after 8 seconds.
        if (Now > manualUntil && director)
        {
            var d = PVector.Sub(camEye, new PVector(0, 0, -12));
            manualYaw = Atan2(d.X, -d.Z);
            manualPitch = Asin(Constrain(d.Y / Math.Max(1, d.Mag()), -1, 1));
        }
        manualUntil = Now + 8;
        manualYaw -= (MouseX - PMouseX) * 0.006f;
        manualPitch = Constrain(manualPitch - (MouseY - PMouseY) * 0.006f, -1.2f, 1.3f);
    }

    public override void MouseWheel(float delta) => zoom = Constrain(zoom * (delta > 0 ? 1 / 1.1f : 1.1f), 0.5f, 3);

    // =====================================================================================
    // The simulation (D, only while PulseShop isn't running)
    // =====================================================================================
    float simBudget, simMetricsAt;
    int simGen0, simGen2;

    void Simulate()
    {
        simBudget += 40 / 60f;
        while (simBudget >= 1)
        {
            simBudget--;
            float r = Random(1);
            Hop(""internet"", ""gateway"", 30 + Random(40), 200, true, Random(0.1f));
            if (r < 0.6f) { Hop(""gateway"", ""catalog"", 6, 200, false, Random(0.1f)); Hop(""catalog"", ""db"", 4, 200, false, Random(0.1f)); Hop(""gateway"", ""inventory"", 4, 200, false, Random(0.1f)); }
            else if (r < 0.85f) { Hop(""gateway"", ""inventory"", 8, 200, false, Random(0.1f)); Hop(""inventory"", ""db"", 6, 200, false, Random(0.1f)); Hop(""gateway"", ""cart"", 5, 200, false, Random(0.1f)); }
            else { Hop(""gateway"", ""cart"", 4, 200, false, Random(0.1f)); Hop(""gateway"", ""payments"", 30, Random(1) < 0.05f ? 503 : 200, false, Random(0.1f)); Hop(""payments"", ""db"", 5, 200, false, Random(0.1f)); }
        }
        if (Now - simMetricsAt > 0.25f)
        {
            simMetricsAt = Now;
            simGen0 += Random(1) < 0.3f ? 1 : 0;
            simGen2 += Random(1) < 0.01f ? 1 : 0;
            var m = new JSONObject();
            m.SetFloat(""rps"", 40 + Random(-3, 3)); m.SetFloat(""p50"", 22); m.SetFloat(""p99"", 70 + Random(30)); m.SetFloat(""errorRate"", 0.01f);
            m.SetInt(""gen0"", simGen0); m.SetInt(""gen1"", simGen0 / 8); m.SetInt(""gen2"", simGen2);
            m.SetFloat(""heapMB"", 18 + Sin(Now) * 3); m.SetFloat(""allocMBps"", 2); m.SetInt(""threadPoolQueue"", 0);
            m.SetInt(""threads"", 8); m.SetInt(""busyThreads"", 2 + (int)Random(3)); m.SetFloat(""cpu"", 6); m.SetInt(""load"", 40);
            Metrics(m);
        }
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
