using Avalonia.Animation;
using Avalonia.Markup.Xaml.MarkupExtensions;
using DanaProcessing.Ide.Localization;
using Microsoft.CodeAnalysis;
using Silk.NET.Maths;

namespace DanaProcessing.Ide.Editor
{
    /// <summary>One entry in the Samples window: a name, a one-line description, and the source to load into a new tab.</summary>
    public sealed record SketchSample(string Name, string Description, string Source);

    /// <summary>
    /// Fixed catalog of sample sketches shown in SamplesWindow. Plain
    /// in-memory list rather than files on disk — these ship with the IDE
    /// itself, so there's nothing to load/parse and no risk of a missing
    /// file at first run. Add new samples here as the API grows (PShape,
    /// PImage, custom shapes, etc.) rather than scattering them elsewhere.
    /// </summary>
    public static class SketchSamples
    {
        public static readonly SketchSample[] All =
        {
            new SketchSample(
                Loc.Tr("Sketch mínimo", "Minimal sketch"),
                Loc.Tr("Setup()/Draw() chico -- un círculo que sigue al mouse, crece mientras mantenés apretado el botón, y cambia de color con cada click.",
                       "A small Setup()/Draw() -- a circle that follows the mouse, grows while you hold the button down, and changes color on each click."),
                MinimalSketch),
             
            new SketchSample(
                Loc.Tr("Sketch mínimo 3D", "Minimal 3D sketch"),
                Loc.Tr("Setup()/Draw() ", "Setup()/Draw() "),
                MinimalSketch3D),

            new SketchSample(
                Loc.Tr("Árbol fractal", "Fractal tree"),
                Loc.Tr("Árbol recursivo con ángulo controlado por el mouse, coloreado por profundidad.",
                       "A recursive tree with its angle controlled by the mouse, colored by depth."),
                FractalTree),

            new SketchSample(
                Loc.Tr("Cubo 3D (Silk.NET)", "3D cube (Silk.NET)"),
                Loc.Tr("Box() con inercia real: arrastrá para rotarlo, soltá y sigue girando por su propia velocidad. La rueda escala el cubo, el click cambia de color y la tecla L compara con/sin Lights().",
                       "Box() with real inertia: drag to rotate it, release and it keeps spinning under its own velocity. The wheel scales the cube, click changes its color, and the L key compares with/without Lights()."),
                Box3D),

            new SketchSample(
                Loc.Tr("Esfera 3D (Silk.NET)", "3D sphere (Silk.NET)"),
                Loc.Tr("Sphere()/SphereDetail() en vivo -- el mouse en X cambia la resolución de la malla, el mouse en Y cambia el color (FillHSB), arrastrá para rotar con inercia, la rueda escala el radio y el click prende/apaga las luces.",
                       "Sphere()/SphereDetail() live -- mouse X changes the mesh resolution, mouse Y changes the color (FillHSB), drag to rotate with inertia, the wheel scales the radius, and click toggles the lights on/off."),
                Sphere3D),

            new SketchSample(
                Loc.Tr("Cámara 3D (Silk.NET)", "3D camera (Silk.NET)"),
                Loc.Tr("Camera()/Perspective()/Ortho() en vivo -- el mouse orbita la cámara, la rueda hace zoom, la tecla P alterna perspectiva/ortográfica y el click recorre una paleta de colores sobre la grilla de cubos.",
                       "Camera()/Perspective()/Ortho() live -- the mouse orbits the camera, the wheel zooms, the P key toggles perspective/orthographic, and click cycles through a color palette on the grid of cubes."),
                Camera3D),

            new SketchSample(
                Loc.Tr("Luces 3D (Silk.NET)", "3D lights (Silk.NET)"),
                Loc.Tr("PointLight()/SpotLight()/LightFalloff() sobre una grilla de esferas -- el mouse mueve la luz, la rueda ajusta el falloff en vivo, la tecla L alterna point/spot light y el click prende/apaga la luz ambiente.",
                       "PointLight()/SpotLight()/LightFalloff() over a grid of spheres -- the mouse moves the light, the wheel adjusts the falloff live, the L key toggles between point/spot light, and click turns the ambient light on/off."),
                Lights3D),

            new SketchSample(
                Loc.Tr("Material 3D (Silk.NET)", "3D material (Silk.NET)"),
                Loc.Tr("Ambient()/Specular()/Emissive()/Shininess() sobre una fila de esferas -- el mouse en X barre la Shininess(), el mouse en Y cambia el color del Specular(), la rueda controla el brillo de la luz, el click cambia el Fill() y la tecla E alterna un Emissive() fijo.",
                       "Ambient()/Specular()/Emissive()/Shininess() over a row of spheres -- mouse X sweeps through Shininess(), mouse Y changes the Specular() color, the wheel controls the light's brightness, click changes the Fill(), and the E key toggles a fixed Emissive()."),
                Material3D),

            new SketchSample(
                Loc.Tr("Cámara avanzada: beginCamera/endCamera (Silk.NET)", "Advanced camera: beginCamera/endCamera (Silk.NET)"),
                Loc.Tr("BeginCamera()/EndCamera() en vivo -- arma un rig de cámara con Translate()/RotateY()/RotateX() (las mismas llamadas que usarías para mover un objeto, pero apuntando a la cámara) en vez de calcular eye/center a mano como en el sample de Camera3D. El mouse orbita, la rueda hace zoom acercando la cámara sobre su propio eje.",
                       "BeginCamera()/EndCamera() live -- builds a camera rig with Translate()/RotateY()/RotateX() (the same calls you'd use to move an object, but aimed at the camera) instead of computing eye/center by hand like in the Camera3D sample. The mouse orbits, the wheel zooms by moving the camera along its own axis."),
                PerspectiveDemo3D),

            new SketchSample(
                Loc.Tr("Coordenadas 3D→2D: modelX/Y/Z + screenX/Y/Z (Silk.NET)", "3D→2D coordinates: modelX/Y/Z + screenX/Y/Z (Silk.NET)"),
                Loc.Tr("ModelX/Y/Z() para \"anclar\" un punto en espacio 3D después de una serie de transformaciones (igual que el ejemplo oficial de Processing), y ScreenX/Y/Z() para proyectar un punto 3D a coordenadas de pantalla y dibujar una etiqueta 2D justo encima de un cubo que gira.",
                       "ModelX/Y/Z() to \"anchor\" a point in 3D space after a series of transformations (just like Processing's official example), and ScreenX/Y/Z() to project a 3D point to screen coordinates and draw a 2D label right above a spinning cube."),
                Coordinates3D),

            new SketchSample(
                Loc.Tr("Shader custom: PShader (Silk.NET)", "Custom shader: PShader (Silk.NET)"),
                Loc.Tr("LoadShader()/Shader()/ResetShader() en vivo -- compila un fragment shader GLSL que colorea por normal (una esfera con cada cara pintada según hacia dónde mira, ignorando luces y Fill()) y lo compara contra el shading normal con solo un click.",
                       "LoadShader()/Shader()/ResetShader() live -- compiles a GLSL fragment shader that colors by normal (a sphere with each face painted according to the direction it faces, ignoring lights and Fill()) and compares it against normal shading with a single click."),
                Shader3D),

            new SketchSample(
                Loc.Tr("normal() (Silk.NET)", "normal() (Silk.NET)"),
                Loc.Tr("Muestra la firma de normal(nx, ny, nz) -- por ahora solo guarda el valor (no tiene efecto visible todavía: hace falta una API de formas 3D por vértice, tipo beginShape()/vertex(), que este motor no tiene aún). Este sample lo deja documentado en código en vez de dejarlo sin ejemplo.",
                       "Shows the signature of normal(nx, ny, nz) -- for now it only stores the value (it has no visible effect yet: that needs a per-vertex 3D shape API, like beginShape()/vertex(), which this engine doesn't have yet). This sample documents it in code instead of leaving it without an example."),
                Normal3D),

            new SketchSample(
                Loc.Tr("Lluvia de círculos: circle()", "Circle rain: circle()"),
                Loc.Tr("Circle(x, y, d) en vivo -- lluvia de círculos que caen y rebotan, el mouse en X controla cuántos caen por segundo, la rueda cambia el tamaño, y cada click cambia de paleta.",
                       "Circle(x, y, d) live -- a rain of circles that fall and bounce, mouse X controls how many fall per second, the wheel changes their size, and each click switches the palette."),
                CircleRain),

            new SketchSample(
                Loc.Tr("Flota reutilizable: createShape()", "Reusable fleet: createShape()"),
                Loc.Tr("CreateShape(GROUP, ...) arma una navecita una sola vez en Setup() a partir de Rect()+Triangle()+Ellipse(), y Shape() la estampa muchas veces por frame -- cada click agrega una nave nueva en el mouse, todas giran a su propia velocidad sin volver a construir la geometría.",
                       "CreateShape(GROUP, ...) builds a little ship just once in Setup() out of Rect()+Triangle()+Ellipse(), and Shape() stamps it many times per frame -- each click adds a new ship at the mouse, and all of them spin at their own speed without ever rebuilding the geometry."),
                ShapeFleet),

            new SketchSample(
                Loc.Tr("Ecualizador reordenable: FloatList", "Reorderable equalizer: FloatList"),
                Loc.Tr("Un FloatList de alturas al estilo ecualizador -- click lo reordena con Shuffle(), la tecla S lo ordena con Sort(), la tecla R genera valores nuevos, y las líneas punteadas marcan Min()/Max()/Average() en vivo mientras cambian.",
                       "A FloatList of equalizer-style bar heights -- click shuffles it with Shuffle(), the S key sorts it with Sort(), the R key generates new values, and dotted lines mark Min()/Max()/Average() live as they change."),
                ReorderableEqualizer),

            new SketchSample(
                Loc.Tr("Reloj de bajo consumo: delay()", "Low-power clock: delay()"),
                Loc.Tr("Un reloj analógico real (Hour()/Minute()/Second()) que llama Delay(1000) al final de cada Draw() -- en vez de redibujar cientos de veces por segundo sin necesidad, se redibuja una sola vez por segundo, como recomienda la referencia de Processing para sketches que no necesitan animación fluida.",
                       "A real analog clock (Hour()/Minute()/Second()) that calls Delay(1000) at the end of every Draw() -- instead of redrawing hundreds of times per second for no reason, it redraws just once per second, as Processing's reference recommends for sketches that don't need smooth animation."),
                LowPowerClock),
            new SketchSample(
                Loc.Tr("Modo presentación: FullScreen()", "Presentation mode: FullScreen()"),
                Loc.Tr("Un caleidoscopio en HSB que gira solo -- la tecla F llama FullScreen() y el HUD de abajo muestra Width/Height (lógicos) junto a PixelWidth/PixelHeight (reales) y DisplayDensity(), para ver los cuatro juntos en un caso con contenido de verdad.",
                       "A self-spinning HSB kaleidoscope -- the F key calls FullScreen() and the HUD at the bottom shows Width/Height (logical) next to PixelWidth/PixelHeight (real) and DisplayDensity(), so you can see all four together in a case with real content."),
                PresentationMode),

            new SketchSample(
                Loc.Tr("Panel de ventana: windowMove/Resizable/Title/Ratio", "Window control panel: windowMove/Resizable/Title/Ratio"),
                Loc.Tr("Un panel con log en pantalla para las funciones de ventana -- F: FullScreen(), M: WindowMove() a una posición al azar, R: WindowResizable(), T: WindowTitle() al azar, A: WindowRatio(16,9). Cada tecla imprime en el log qué se pidió y qué devolvió el estado (IsFullScreen, IsWindowResizable, WindowTitleText); si el host de la IDE todavía no escucha estos eventos, el log documenta igual el llamado -- queda listo para cuando se conecte.",
                       "An on-screen log panel for the window functions -- F: FullScreen(), M: WindowMove() to a random position, R: WindowResizable(), T: WindowTitle() to a random title, A: WindowRatio(16,9). Each key prints to the log what was requested and what the state returned (IsFullScreen, IsWindowResizable, WindowTitleText); if the IDE host doesn't listen for these events yet, the log still documents the call -- ready for whenever it gets wired up."),
                WindowControlPanel),

            new SketchSample(
                Loc.Tr("Tamaño dinámico: Settings()", "Dynamic size: Settings()"),
                Loc.Tr("Settings() corre ANTES que Setup() -- acá decide una orientación (retrato o paisaje) al azar y llama Size() con esa decisión, así Setup() ya arranca con Width/Height correctos sin tener que adivinarlos de antemano. Click reelige la orientación en cualquier momento llamando Size() directo, para contrastar con la garantía de orden que da Settings().",
                       "Settings() runs BEFORE Setup() -- here it randomly picks an orientation (portrait or landscape) and calls Size() with that choice, so Setup() already starts with the correct Width/Height without having to guess them beforehand. Click re-picks the orientation at any time by calling Size() directly, to contrast with the ordering guarantee that Settings() gives."),
                DynamicSizeSettings),
                // --- inside SketchSamples.All, add: ---
            new SketchSample(
                Loc.Tr("Contador binario: Binary()/Unbinary()", "Binary counter: Binary()/Unbinary()"),
                Loc.Tr("Un contador de 0 a 255 mostrado como 8 bits que se prenden y apagan -- Binary(byte) arma la fila, Unbinary() la vuelve a convertir en número para probar que van y vuelven. La rueda cambia la velocidad, y se puede forzar un bit a mano con click.",
                       "A counter from 0 to 255 shown as 8 bits turning on and off -- Binary(byte) builds the row, Unbinary() converts it back into a number to prove they round-trip. The wheel changes the speed, and you can force a bit by hand with a click."),
                BinaryCounter),

            new SketchSample(
                Loc.Tr("Búsqueda en paralelo: thread()", "Parallel search: thread()"),
                Loc.Tr("Thread(\"SearchPrimes\") lanza la búsqueda de primos en un hilo aparte apenas arranca el sketch -- el spinner de la izquierda sigue girando fluido en Draw() mientras tanto, sin trabarse, porque el trabajo pesado vive en su propio hilo. Click reinicia la búsqueda.",
                       "Thread(\"SearchPrimes\") launches the prime search on a separate thread as soon as the sketch starts -- the spinner on the left keeps spinning smoothly in Draw() the whole time, without stalling, because the heavy work lives on its own thread. Click restarts the search."),
                PrimeSearchThread),

            new SketchSample(
                Loc.Tr("Exportar a PDF: beginRaw()/endRaw()", "Export to PDF: beginRaw()/endRaw()"),
                Loc.Tr("Un póster generativo (círculos en espiral con color HSB) que se dibuja normal cada frame -- la tecla V llama al MISMO método de dibujo una vez más, esta vez encerrado entre BeginRaw()/EndRaw(), y ese segundo llamado no aparece en pantalla: se va directo a poster.pdf como vector real, no como imagen.",
                       "A generative poster (circles in a spiral with HSB color) that draws normally every frame -- the V key calls the SAME drawing method one more time, this time wrapped in BeginRaw()/EndRaw(), and that second call never appears on screen: it goes straight to poster.pdf as real vector output, not an image."),
                PdfExport),

            new SketchSample(
                Loc.Tr("Respaldo de trazos: saveStream()", "Stroke backup: saveStream()"),
                Loc.Tr("Dibujá con el mouse -- la tecla S guarda el trazo en trazo.txt con SaveStrings() y después usa CreateInput() + SaveStream() para copiar ese archivo entero a un backup con nombre único, sin leerlo a mano línea por línea.",
                       "Draw with the mouse -- the S key saves the stroke to trazo.txt with SaveStrings() and then uses CreateInput() + SaveStream() to copy that whole file to a backup with a unique name, without reading it by hand line by line."),
                StrokeBackup),

            new SketchSample(
                Loc.Tr("Tarjeta de datos: parseJSONObject()/parseXML() + launch()", "Data card: parseJSONObject()/parseXML() + launch()"),
                Loc.Tr("Arma un JSONObject y un fragmento de XML con la API normal, los serializa a String, y los vuelve a leer con ParseJSONObject()/ParseXML() -- exactamente como llegarían datos desde una red o un campo de texto, no desde un archivo. La tecla L abre el sitio guardado en el JSON con Launch(), en el navegador del sistema.",
                       "Builds a JSONObject and an XML fragment with the normal API, serializes them to String, and reads them back with ParseJSONObject()/ParseXML() -- exactly as data would arrive from a network call or a text field, not from a file. The L key opens the site saved in the JSON with Launch(), in the system browser."),
                DataCardParseLaunch),
            new SketchSample(
                Loc.Tr("Gema facetada: BeginShape/Vertex(x,y,z)/Normal() en 3D", "Faceted gem: BeginShape/Vertex(x,y,z)/Normal() in 3D"),
                Loc.Tr("Un octaedro armado a mano, cara por cara, con BeginShape(Triangles)+Vertex(x,y,z)+Normal() -- la tecla N alterna entre sombreado plano (una normal por cara, via PVector.Cross()) y suave (normales promediadas por vértice), para ver en vivo qué cambia normal() en la iluminación. Arrastrá para rotar.",
                       "An octahedron built by hand, face by face, with BeginShape(Triangles)+Vertex(x,y,z)+Normal() -- the N key toggles between flat shading (one normal per face, via PVector.Cross()) and smooth shading (normals averaged per vertex), to see live what normal() changes about the lighting. Drag to rotate."),
                FacetedGem),

            new SketchSample(
                Loc.Tr("Partículas 3D: PVector con Z", "3D particles: PVector with Z"),
                Loc.Tr("PVector ahora tiene X, Y, y Z -- este sistema de partículas usa Add()/Sub() de PVector para gravedad y rebote en las TRES dimensiones dentro de un cubo invisible, en vez de simular la profundidad a mano con floats sueltos. Click agrega más partículas.",
                       "PVector now has X, Y, and Z -- this particle system uses PVector's Add()/Sub() for gravity and bouncing in all THREE dimensions inside an invisible cube, instead of simulating depth by hand with loose floats. Click adds more particles."),
                Particles3D),

             new SketchSample(
                Loc.Tr("Enjambre reutilizable: CreateShape3D()", "Reusable swarm: CreateShape3D()"),
                Loc.Tr("La misma gema facetada del sample anterior, pero armada UNA sola vez con CreateShape3D() en vez de BeginShape()/EndShape() cada frame -- la malla se sube a la GPU una vez, y después 150 copias se dibujan solo con Shape(), cada una con su propia posición y rotación, sin volver a triangular ni volver a subir nada.",
                       "The same faceted gem from the previous sample, but built just ONCE with CreateShape3D() instead of BeginShape()/EndShape() every frame -- the mesh is uploaded to the GPU once, and then 150 copies are drawn just with Shape(), each with its own position and rotation, without ever re-triangulating or re-uploading anything."),
                ReusableSwarm),

            new SketchSample(
                Loc.Tr("Cartel texturizado: Vertex(x,y,z,u,v)", "Textured billboard: Vertex(x,y,z,u,v)"),
                Loc.Tr("Un panel 3D con una textura generada en código (un PGraphics 2D convertido a PImage con Get()) mapeada por Vertex(x,y,z,u,v) -- Texture(img) antes de BeginShape() le dice al shape qué imagen indexan esas coordenadas. Arrastrá para rotar y ver el mapeo desde otros ángulos.",
                       "A 3D panel with a texture generated in code (a 2D PGraphics converted to a PImage with Get()) mapped via Vertex(x,y,z,u,v) -- Texture(img) before BeginShape() tells the shape which image those coordinates index into. Drag to rotate and see the mapping from other angles."),
                TexturedBillboard),

            new SketchSample(
                Loc.Tr("Audio reactivo: FFT en vivo (NAudio)", "Audio-reactive: live FFT (NAudio)"),
                Loc.Tr("Un `// nuget: NAudio` capturando el audio del sistema (sin micrófono) y corriéndolo por una FFT para mover un anillo de 40 barras -- cualquier cosa que esté sonando en la máquina mueve el dibujo en vivo. La tecla S detiene/reinicia la captura.",
                       "A `// nuget: NAudio` sketch capturing system audio (no microphone) and running it through an FFT to drive a ring of 40 bars -- whatever's playing on the machine moves the drawing live. The S key stops/restarts the capture."),
                AudioReactive),

            new SketchSample(
                Loc.Tr("Instalación reactiva: MIDI + OSC (NAudio.Midi, Rug.Osc)", "Reactive installation: MIDI + OSC (NAudio.Midi, Rug.Osc)"),
                Loc.Tr("Dos paquetes NuGet a la vez alimentando la misma grilla de \"ripples\" estilo instalación/VJ: notas y CC de un controlador MIDI conectado, y cualquier mensaje OSC a /dana/pulse <float> por UDP -- ninguno de los dos es obligatorio (sin ambos igual podés hacer click para ver cómo reacciona).",
                       "Two NuGet packages at once feeding the same installation/VJ-style \"ripple\" grid: notes and CC from a connected MIDI controller, and any OSC message to /dana/pulse <float> over UDP -- neither is required (click anywhere without either to see it react)."),
                ControllerInstallation),

            new SketchSample(
                Loc.Tr("Probador de Hot Reload: juego de vuelo", "Hot Reload testbed: flying game"),
                Loc.Tr("Un juego chico estilo \"flappy bird\" hecho a propósito para el botón ⚡ Hot Reload -- toda la sensación del juego (gravedad, fuerza del aleteo, velocidad/separación de los tubos) está en constantes al principio del archivo. Jugá, cambiá un número, y apretá Hot Reload en vez de Run: la física nueva se siente al toque sin perder el puntaje ni reiniciar la partida.",
                       "A small \"flappy bird\"-style game built on purpose for the ⚡ Hot Reload button -- everything about how it feels (gravity, flap strength, pipe speed/spacing) sits in constants at the top of the file. Play it, change a number, and press Hot Reload instead of Run: the new physics apply instantly without losing your score or restarting the run."),
                HotReloadFlapper),

            new SketchSample(
                Loc.Tr("ML.NET paso a paso: regresión lineal en vivo", "ML.NET step by step: live linear regression"),
                Loc.Tr("El ejemplo de ML.NET más simple posible, pensado para VER el aprendizaje, no solo el resultado: una línea recta se reentrena un poquito más cada frame (un paso de descenso de gradiente a la vez) sobre los puntos que clickeás, y el cartel de arriba muestra la iteración y el error (MSE) bajando en vivo. R reinicia con puntos al azar.",
                       "The simplest possible ML.NET example, built to SHOW the learning instead of just the result: a straight line retrains a little more every frame (one gradient-descent step at a time) against the points you click, with the iteration count and error (MSE) visibly dropping frame by frame. R restarts with random points."),
                SimpleLinearRegression),

            new SketchSample(
                Loc.Tr("Campo de color con ML.NET: aprendizaje en vivo", "ML.NET color field: live learning"),
                Loc.Tr("Un `// nuget: Microsoft.ML` de verdad -- cada click deja una semilla de color, y un modelo de regresión (entrenado ahí mismo, sin archivo ni descarga) aprende a pintar un campo generativo que se reacomoda alrededor de tus clicks. El entrenamiento corre en un hilo aparte con un aviso de \"Training...\" visible, nunca traba el dibujo.",
                       "A real `// nuget: Microsoft.ML` sketch -- every click drops a color seed, and a regression model (trained right there, no file or download involved) learns to paint a generative field that reshapes around your clicks. Training runs on a background thread with a visible \"Training...\" notice, never blocking the drawing."),
                MLColorField),

            new SketchSample(
                Loc.Tr("Flappy autoaprendiz: ML.NET juega solo", "Self-learning flappy: ML.NET plays itself"),
                Loc.Tr("ML.NET no trae reinforcement learning de fábrica, así que este ejemplo lo arma con lo que SÍ tiene: cada tanto, 60 intentos simulados (con algo de ruido) corren en un hilo aparte; los mejores le enseñan a un clasificador cuándo aletear, y ese modelo se vuelve el nuevo campeón SOLO si de verdad juega mejor que el anterior. El pájaro que ves jugando en pantalla siempre usa al campeón vigente -- mirá cómo el puntaje mejora generación tras generación, sin que vos toques nada.",
                       "ML.NET has no reinforcement learning built in, so this example builds one out of what it DOES have: every so often, 60 simulated attempts (with a bit of noise) run on a background thread; the best ones teach a classifier when to flap, and that model becomes the new champion ONLY if it genuinely plays better than the last one. The bird playing on screen always uses the current champion -- watch the score climb generation after generation, without you touching anything."),
                SelfPlayingFlapper),

            new SketchSample(
                Loc.Tr("200.000 partículas en la GPU: ruido curl", "200,000 GPU particles: curl noise"),
                Loc.Tr("CreateGpuParticles() de verdad -- 200.000 partículas simuladas enteramente en la GPU, cada una moviéndose por un campo de ruido curl calculado en vivo. En esta máquina corre como un shader de cómputo real (GLSL 430, glDispatchCompute); en una Mac corre exactamente el MISMO código de sketch, pero sobre una simulación de textura ping-pong -- macOS topa OpenGL en 4.1, por debajo de lo que un compute shader necesita. La etiqueta abajo dice cuál de los dos está corriendo.",
                       "Real CreateGpuParticles() -- 200,000 particles simulated entirely on the GPU, each one moving through a live curl-noise field. On this machine it runs as a real compute shader (GLSL 430, glDispatchCompute); on a Mac the exact SAME sketch code runs instead on a ping-pong texture simulation -- macOS caps OpenGL at 4.1, below what a compute shader needs. The label at the bottom says which of the two is actually running."),
                GpuParticles),
        };

        private const string CircleRain =
@"// Circle rain -- demo of Circle(x, y, d), the new shortcut for
// Ellipse(x, y, d, d). Each particle is a circle that falls with
// simple gravity and bounces off the floor losing energy, until it
// fades out and gets recycled at the top with a new size.
public class MySketch : Sketch
{
    private class Drop
    {
        public float X, Y, VY, Size;
    }
 
    private readonly List<Drop> _drops = new List<Drop>();
    private readonly Color[] _palette =
    {
        new Color(120, 180, 255),
        new Color(255, 140, 180),
        new Color(160, 255, 180),
        new Color(255, 210, 120),
    };
    private int _paletteIndex;
    private float _sizeScale = 1f;
 
    public override void Setup()
    {
        Size(700, 450);
        for (int i = 0; i < 40; i++)
            _drops.Add(NewDrop(Random(Height)));
    }
 
    public override void Draw()
    {
        Background(18, 20, 28);
 
        // Mouse X controls how many new drops spawn per frame --
        // from 0 (none) to ~1 per frame near the right edge.
        float spawnChance = Map(MouseX, 0, Width, 0f, 1f);
        if (Random(1f) < spawnChance)
            _drops.Add(NewDrop(-20));
 
        NoStroke();
        Fill(_palette[_paletteIndex]);
 
        foreach (var drop in _drops)
        {
            drop.VY += 0.4f; // gravity
            drop.Y += drop.VY;
 
            float floor = Height - drop.Size / 2f;
            if (drop.Y > floor)
            {
                drop.Y = floor;
                drop.VY *= -0.55f; // bounce with energy loss
            }
 
            Circle(drop.X, drop.Y, drop.Size * _sizeScale);
        }
 
        // Recycle the ones that barely bounce anymore, so they don't pile up forever.
        _drops.RemoveAll(d => Abs(d.VY) < 0.6f && d.Y >= Height - d.Size / 2f - 1);
 
        Fill(255);
        TextSize(13);
        Text($""Circle() x{_drops.Count} -- mouse X: spawn -- rueda: tamaño ({_sizeScale:F1}x) -- click: paleta"", 12, Height - 16);
    }
 
    public override void MouseWheel(float delta)
    {
        _sizeScale = Constrain(_sizeScale - delta * 0.05f, 0.4f, 2.5f);
    }
 
    public override void MouseClicked()
    {
        _paletteIndex = (_paletteIndex + 1) % _palette.Length;
    }
 
    private Drop NewDrop(float y) => new Drop
    {
        X = Random(Width),
        Y = y,
        VY = Random(1f, 3f),
        Size = Random(10, 26),
    };
}
";

        private const string ShapeFleet =
@"// Reusable fleet -- demo of CreateShape(). The little ship (a Triangle
// for the nose, a Rect for the fuselage, and an Ellipse for the engine)
// is built just ONCE in Setup() with CreateShape(GROUP, ...), instead of
// calling Triangle()/Rect()/Ellipse() by hand every frame for each ship.
// Each click adds a new ship at the mouse position -- they all share
// the SAME geometry (the same PShape), only where and with what
// rotation it gets stamped via Shape() changes.
public class MySketch : Sketch
{
    private class Ship
    {
        public float X, Y, Angle, Spin;
    }
 
    private PShape _shipShape;
    private readonly List<Ship> _fleet = new List<Ship>();
 
    public override void Setup()
    {
        Size(700, 450);
 
        // Geometry centered at (0,0): nose pointing toward -Y, rectangular
        // fuselage, engine as an ellipse at the tail. Fill()/Stroke() at the
        // moment of each CreateShape() call get BAKED into that piece --
        // that's why the color is set before each call.
        Fill(230, 230, 235);
        NoStroke();
        var nose = CreateShape(PShapeType.Triangle, -10, -28, 10, -28, 0, -46);
 
        Fill(160, 170, 185);
        var body = CreateShape(PShapeType.Rect, -9, -28, 18, 34);
 
        Fill(255, 140, 60);
        var engine = CreateShape(PShapeType.Ellipse, -12, 4, 24, 16);
 
        _shipShape = CreateShape(nose, body, engine);
 
        _fleet.Add(new Ship { X = Width / 2f, Y = Height / 2f, Spin = 0.5f });
    }
 
    public override void Draw()
    {
        Background(12, 14, 22);
 
        foreach (var ship in _fleet)
        {
            ship.Angle += ship.Spin;
 
            PushMatrix();
            Translate(ship.X, ship.Y);
            Rotate(Radians(ship.Angle));
            Shape(_shipShape, 0, 0);
            PopMatrix();
        }
 
        Fill(255);
        TextSize(13);
        Text($""CreateShape(GROUP) x1, Shape() x{_fleet.Count} -- click agrega una nave"", 12, Height - 16);
    }
 
    public override void MouseClicked()
    {
        _fleet.Add(new Ship
        {
            X = MouseX,
            Y = MouseY,
            Spin = Random(-2f, 2f),
        });
    }
}
";

        private const string ReorderableEqualizer =
@"// Reorderable equalizer -- demo of FloatList. The 24 bar heights live
// in a single FloatList instead of a fixed hand-written array --
// click calls Shuffle() to scramble them with an animation, the S key
// calls Sort(), the R key generates new values with Random(), and
// Min()/Max()/Average() (all FloatList methods) draw the reference
// lines that move on their own when the data changes.
public class MySketch : Sketch
{
    private FloatList _heights;
    private FloatList _targetHeights;
    private const int BarCount = 24;
 
    public override void Setup()
    {
        Size(720, 420);
        _heights = new FloatList();
        _targetHeights = new FloatList();
        RegenerateValues();
    }
 
    public override void Draw()
    {
        Background(22, 24, 30);
 
        float barWidth = Width / (float)BarCount;
 
        for (int i = 0; i < _heights.Size; i++)
        {
            // Smoothly interpolate toward the target value, so Shuffle()/
            // Sort() look like an animation instead of an abrupt jump.
            _heights[i] = Lerp(_heights[i], _targetHeights[i], 0.15f);
 
            float h = _heights[i];
            float x = i * barWidth;
            float hue = Map(i, 0, BarCount, 200, 320);
 
            NoStroke();
            FillHSB(hue, 70, 90);
            Rect(x + 2, Height - h, barWidth - 4, h);
        }
 
        float avg = _targetHeights.Average();
        float min = _targetHeights.Min();
        float max = _targetHeights.Max();
 
        DrawReferenceLine(avg, new Color(255, 255, 255), $""Average() = {avg:F0}"");
        DrawReferenceLine(min, new Color(120, 200, 255), $""Min() = {min:F0}"");
        DrawReferenceLine(max, new Color(255, 140, 140), $""Max() = {max:F0}"");
 
        Fill(255);
        TextSize(13);
        Text(""click: Shuffle() -- tecla S: Sort() -- tecla R: valores nuevos"", 12, 22);
    }
 
    public override void MouseClicked()
    {
        _targetHeights.Shuffle();
    }
 
    public override void KeyPressed()
    {
        if (Key == 's')
            _targetHeights.Sort();
        else if (Key == 'r')
            RegenerateValues();
    }
 
    private void RegenerateValues()
    {
        _targetHeights.Clear();
        for (int i = 0; i < BarCount; i++)
            _targetHeights.Append(Random(40, Height - 40));
 
        if (_heights.Size == 0)
        {
            for (int i = 0; i < BarCount; i++)
                _heights.Append(_targetHeights[i]);
        }
    }
 
    private void DrawReferenceLine(float h, Color c, string label)
    {
        Stroke(c);
        StrokeWeight(1);
        Line(0, Height - h, Width, Height - h);
        NoStroke();
        Fill(c);
        TextSize(12);
        Text(label, Width - 150, Height - h - 6);
    }
}
";

        private const string LowPowerClock =
@"// Low-power clock -- demo of Delay(). An ordinary analog clock
// (Hour()/Minute()/Second() drive the hands) that, unlike
// every other sample in this list, does NOT need 60 frames per
// second: nothing on screen changes more than once per second. That's why
// Draw() ends with Delay(1000) -- the animation thread stops for that
// whole second instead of recalculating and redrawing an identical clock
// hundreds of times for no reason. This is exactly the use case that
// Processing's reference recommends for delay(): not for smooth
// animation (that's what FrameRate() is for), but for low-power sketches
// that only need to redraw once in a while.
public class MySketch : Sketch
{
    public override void Setup()
    {
        Size(400, 400);
    }
 
    public override void Draw()
    {
        Background(15, 18, 26);
 
        float cx = Width / 2f;
        float cy = Height / 2f;
        float radius = Min(Width, Height) * 0.4f;
 
        NoFill();
        Stroke(200, 200, 210);
        StrokeWeight(3);
        Ellipse(cx, cy, radius * 2, radius * 2);
 
        // Hour marks.
        for (int i = 0; i < 12; i++)
        {
            float a = Radians(i * 30 - 90);
            float x1 = cx + Cos(a) * radius * 0.9f;
            float y1 = cy + Sin(a) * radius * 0.9f;
            float x2 = cx + Cos(a) * radius;
            float y2 = cy + Sin(a) * radius;
            Line(x1, y1, x2, y2);
        }
 
        int h = Hour() % 12;
        int m = Minute();
        int s = Second();
 
        DrawHand(cx, cy, (h + m / 60f) / 12f * 360 - 90, radius * 0.5f, 6, new Color(230, 230, 235));
        DrawHand(cx, cy, (m + s / 60f) / 60f * 360 - 90, radius * 0.72f, 4, new Color(200, 210, 255));
        DrawHand(cx, cy, s / 60f * 360 - 90, radius * 0.85f, 2, new Color(255, 110, 110));
 
        Fill(255);
        TextSize(13);
        Text($""{h:D2}:{m:D2}:{s:D2} -- Delay(1000): se redibuja 1 vez/seg, no {TargetFrameRate}/seg"", 20, Height - 20);
 
        // The line that makes this sample's whole point: it blocks the
        // animation thread for a full second before returning to Draw(), instead
        // of recalculating/redrawing an identical clock dozens of times for no
        // reason while the second hand hasn't changed.
        Delay(1000);
    }
 
    private void DrawHand(float cx, float cy, float angleDeg, float length, float weight, Color c)
    {
        float a = Radians(angleDeg);
        Stroke(c);
        StrokeWeight(weight);
        Line(cx, cy, cx + Cos(a) * length, cy + Sin(a) * length);
    }
}
";

        private const string MinimalSketch =
@"public class MySketch : Sketch
{
    private readonly Color[] _palette =
    {
        new Color(100, 200, 255),
        new Color(255, 120, 140),
        new Color(255, 210, 90),
        new Color(140, 230, 150),
    };
    private int _colorIndex;
    private float _size = 60;

    public override void Setup()
    {
        Size(600, 400);
    }

    public override void Draw()
    {
        Background(20, 20, 30);

        // While the mouse button is held down, the circle grows; when you
        // release it, it returns to its normal size -- IsMousePressed is read every
        // frame, Lerp() smooths the change instead of jumping abruptly.
        _size = Lerp(_size, IsMousePressed ? 120f : 60f, 0.1f);

        NoStroke();
        Fill(_palette[_colorIndex]);
        Ellipse(MouseX, MouseY, _size, _size);
    }

    public override void MouseClicked()
    {
        // Each click advances to the next color in the palette.
        _colorIndex = (_colorIndex + 1) % _palette.Length;
    }
}
";
        private const string MinimalSketch3D =
@"public class MySketch : Sketch
{ private float _angle;

    public override void Setup() => Size(800, 600, RendererKind.Renderer3D);

    public override void Draw()
    {
        Background(20, 20, 30);
        Lights();
        Fill(255, 150, 90);
        PushMatrix();
        Translate(Width / 2f, Height / 2f, 0);
        RotateY(_angle += 0.02f);
        Box(200);
        PopMatrix();
    }
}
";

        private const string FractalTree =
@"// Recursive fractal tree, adapted from p5.js's ""Recursive Tree"" example.
// Palette: trunk in dark red, branches interpolating from orange to green
// by depth, leaves/background in warm tones.
public class MySketch : Sketch
{
    private float _angle;

    private Color _paletteRed;
    private Color _paletteOrange;
    private Color _paletteCream;
    private Color _paletteGreen;

    private const int MaxDepth = 10;

    public override void Setup()
    {
        Size(800, 600);
        ColorMode(ColorSpaceMode.RGB);

        _paletteRed = new Color(0x8B, 0x26, 0x26);
        _paletteOrange = new Color(0xEF, 0x69, 0x05);
        _paletteCream = new Color(0xF1, 0xE5, 0xA1);
        _paletteGreen = new Color(0x48, 0x6C, 0x2F);
    }

    public override void Draw()
    {
        Background(Red(_paletteCream), Green(_paletteCream), Blue(_paletteCream));

        _angle = (MouseX / Width) * 90f;
        _angle = Min(_angle, 90f);

        Translate(Width / 2f, Height);

        StrokeWeight(6);
        Stroke(Red(_paletteRed), Green(_paletteRed), Blue(_paletteRed));
        Line(0, 0, 0, -180);

        Translate(0, -180);
        Branch(180, 0);
    }

    public override void KeyPressed()
    {
        if (Key == 's')
            SaveFrame();
    }

    private void Branch(float length, int level)
    {
        float depthRatio = Constrain((float)level / MaxDepth, 0f, 1f);
        var branchColor = LerpColor(_paletteOrange, _paletteGreen, depthRatio);
        Stroke(Red(branchColor), Green(branchColor), Blue(branchColor));
        StrokeWeight(Map(depthRatio, 0f, 1f, 5f, 1f));

        length *= 0.66f;

        if (length > 2)
        {
            PushMatrix();
            Rotate(_angle);
            Line(0, 0, 0, -length);
            Translate(0, -length);
            Branch(length, level + 1);
            PopMatrix();

            PushMatrix();
            Rotate(-_angle);
            Line(0, 0, 0, -length);
            Translate(0, -length);
            Branch(length, level + 1);
            PopMatrix();
        }
        else
        {
            NoStroke();
            Fill(Red(_paletteGreen), Green(_paletteGreen), Blue(_paletteGreen));
            Ellipse(0, 0, 6, 6);
            NoFill();
            Stroke(Red(branchColor), Green(branchColor), Blue(branchColor));
        }
    }
}
";

        private const string Box3D =
@"// Demo of the 3D backend (Silk.NET/OpenGL) -- Size(w, h, RendererKind.Renderer3D)
// turns on the direct GPU backend for the sketch (Renderer3DBackend.Create()
// spins up a hidden Silk.NET window + OpenGL context + shader + cube mesh,
// just ONCE in Setup()); Sketch.RenderFrame() already composes 3D +
// 2D overlay (the Text() further below) automatically every frame, so
// there's no need for an offscreen PGraphics or a manual Image().
//
// ROTATION: a real trackball, using an accumulated quaternion (_qw/_qx/_qy/_qz)
// instead of two Euler angles added up separately (RotateY(rotY) +
// RotateX(rotX)). The old version had a real bug: since RotateX()/
// RotateY() always rotate around the cube's ORIGINAL axes (not the
// axes as they currently appear on screen), dragging up/down
// felt right only while the front face kept facing forward. As soon as
// a previous rotation left a face turned sideways, that same vertical
// drag ended up spinning the cube like a wheel instead
// of tilting it -- and with the back face facing the camera, the axis got
// mirrored, so dragging right spun it left.
//
// The fix (standard arcball): every drag frame computes a rotation axis
// in SCREEN space (perpendicular to the mouse's movement)
// and composes it by PRE-multiplying onto the accumulated quaternion --
// that is, the new increment is applied in world/camera space, not in
// the cube's (already-rotated) local space. This way dragging always
// rotates around the axis as it appears on screen at THAT moment, no matter
// how much it has rotated before or which face is facing the camera. On
// release, the last increment keeps being applied with friction (*0.99 per
// frame) -- same inertia as before, but without the fixed-axis bug.
// To draw, the quaternion is decomposed into angle+axis just ONCE per
// frame and sent with the new Rotate(angle, x, y, z) (rotation around an
// arbitrary axis) -- a single combined rotation, instead of two separate
// RotateX/RotateY calls stepping on each other.
public class MySketch : Sketch
{
    // Accumulated orientation as a quaternion (w, x, y, z), identity = not rotated.
    private float _qw = 1f, _qx, _qy, _qz;
    // Last drag increment (normalized axis + angle) -- keeps being
    // applied with friction while the mouse is released, for the inertia.
    private float _spinAxisX = 1f, _spinAxisY, _spinAxisZ, _spinAngle;

    private float _boxSize = 220;
    private bool _lit = true;

    private readonly Color[] _palette =
    {
        new Color(255, 176, 140),
        new Color(140, 200, 255),
        new Color(180, 255, 160),
        new Color(255, 210, 90),
    };
    private int _colorIndex;

    public override void Setup() => Size(800, 600, RendererKind.Renderer3D);

    public override void Draw()
    {
        if (IsMousePressed)
        {
            float dx = MouseX - PMouseX;
            float dy = MouseY - PMouseY;
            float dragMag = Mag(dx, dy);
            if (dragMag > 0.001f)
            {
                // Axis perpendicular to the drag, IN SCREEN SPACE -- not
                // in object space, which is exactly what avoids the bug
                // from the previous version. And without inverting: the 3D world is
                // Y-down (see COORDINATE CONVENTION in Renderer3DBackend.cs)
                // and that flip is already included below (-dy).
                _spinAxisX = -dy / dragMag;
                _spinAxisY = dx / dragMag;
                _spinAxisZ = 0f;
                _spinAngle = dragMag * 0.01f;
            }
        }

        ApplySpin(_spinAxisX, _spinAxisY, _spinAxisZ, _spinAngle);
        if (!IsMousePressed)
            _spinAngle *= 0.99f; // friction only while spinning free (released)

        // Lights() every frame -- the backend resets the light list on
        // every BeginFrame(), just like Processing: without this the cube looks
        // flat (unshaded Fill() is Processing's actual default).
        // The L key turns this off on purpose so the difference stands out.
        if (_lit)
            Lights();
        Fill(_palette[_colorIndex]);
        PushMatrix();
        Translate(Width / 2f, Height / 2f, 0);

        // Total angle + axis, derived from the accumulated quaternion -- ONE
        // single combined rotation per frame.
        float angle = 2f * Acos(Constrain(_qw, -1f, 1f));
        float s = Sqrt(Max(1f - _qw * _qw, 0f));
        if (s < 0.0001f)
            Rotate(angle, 1, 0, 0); // angle ~0: the axis doesn't matter
        else
            Rotate(angle, _qx / s, _qy / s, _qz / s);

        Box(_boxSize);
        PopMatrix();

        Fill(255);
        TextSize(13);
        string lightsStatus = _lit ? ""ON"" : ""OFF"";
        Text($""Arrastra para rotar (trackball real, sin importar la cara que mires) -- rueda escala ({(int)_boxSize}px) -- click cambia color -- tecla L: luces {lightsStatus}"", 12, Height - 16);
    }

    // Composes a rotation increment (normalized axis + angle, in
    // radians) onto the accumulated quaternion, by PRE-multiplying -- the
    // new increment is applied in world space, on top of the
    // existing orientation, which is what makes dragging always
    // correspond to the SCREEN axes instead of the object's (already
    // rotated) axes.
    private void ApplySpin(float ax, float ay, float az, float angle)
    {
        if (angle == 0f)
            return;

        float half = angle * 0.5f;
        float dw = Cos(half), dx = ax * Sin(half), dy = ay * Sin(half), dz = az * Sin(half);

        float nw = dw * _qw - dx * _qx - dy * _qy - dz * _qz;
        float nx = dw * _qx + dx * _qw + dy * _qz - dz * _qy;
        float ny = dw * _qy - dx * _qz + dy * _qw + dz * _qx;
        float nz = dw * _qz + dx * _qy - dy * _qx + dz * _qw;

        // Renormalize -- without this, the floating-point error accumulated
        // frame after frame ends up deforming the cube instead of just rotating it.
        float norm = Sqrt(nw * nw + nx * nx + ny * ny + nz * nz);
        _qw = nw / norm;
        _qx = nx / norm;
        _qy = ny / norm;
        _qz = nz / norm;
    }

    public override void MouseWheel(float delta)
    {
        _boxSize = Constrain(_boxSize - delta * 4, 60, 380);
    }

    public override void MouseClicked()
    {
        _colorIndex = (_colorIndex + 1) % _palette.Length;
    }

    public override void KeyPressed()
    {
        if (Key == 'l')
            _lit = !_lit;
    }
}
";

        private const string Sphere3D =
@"// Demo of Sphere()/SphereDetail() on the same 3D backend as Box3D --
// Size(w, h, RendererKind.Renderer3D) directly in Setup(), no offscreen
// PGraphics (see Box3D's comment for why). Mouse X
// controls SphereDetail() live (range 3-60)
// so the effect of mesh resolution stands out: on the left,
// a clearly low-poly (faceted) sphere; on the right, a finer
// mesh -- EnsureSphereMesh() in Renderer3DBackend only re-uploads the mesh
// when the detail level actually changes between frames.
//
// On top of that: mouse Y sweeps through the hue wheel via FillHSB()
// (0-360 degrees), the wheel changes the radius live, and click toggles
// the lights to compare the shading against Processing's flat color.
//
// ROTATION: the same quaternion trackball as Box3D (see its comments
// for why) -- dragging always rotates around the axis as it appears on
// screen at that moment, instead of the object's fixed axes, so it never
// inverts or gets weird depending on which side of the sphere is facing
// the camera.
public class MySketch : Sketch
{
    private float _qw = 1f, _qx, _qy, _qz;
    private float _spinAxisX = 1f, _spinAxisY, _spinAxisZ, _spinAngle;

    private float _radius = 160;
    private bool _lit = true;

    public override void Setup() => Size(800, 600, RendererKind.Renderer3D);

    public override void Draw()
    {
        int detail = (int)Map(MouseX, 0, Width, 3, 60);
        float hue = Map(MouseY, 0, Height, 0, 360);

        if (IsMousePressed)
        {
            float dx = MouseX - PMouseX;
            float dy = MouseY - PMouseY;
            float dragMag = Mag(dx, dy);
            if (dragMag > 0.001f)
            {
                _spinAxisX = -dy / dragMag;
                _spinAxisY = dx / dragMag;
                _spinAxisZ = 0f;
                _spinAngle = dragMag * 0.01f;
            }
        }

        ApplySpin(_spinAxisX, _spinAxisY, _spinAxisZ, _spinAngle);
        if (!IsMousePressed)
            _spinAngle *= 0.98f;

        if (_lit)
            Lights();
        SphereDetail(detail);
        FillHSB(hue, 65, 95);
        PushMatrix();
        Translate(Width / 2f, Height / 2f, 0);

        float angle = 2f * Acos(Constrain(_qw, -1f, 1f));
        float s = Sqrt(Max(1f - _qw * _qw, 0f));
        if (s < 0.0001f)
            Rotate(angle, 1, 0, 0);
        else
            Rotate(angle, _qx / s, _qy / s, _qz / s);

        Sphere(_radius);
        PopMatrix();

        Fill(255);
        string lightsStatus = _lit ? ""ON"" : ""OFF"";
        Text($""SphereDetail({detail}) por mouse X -- color por mouse Y -- arrastra para rotar (trackball) -- rueda escala ({(int)_radius}) -- click: luces {lightsStatus}"", 12, Height - 16);
    }

    private void ApplySpin(float ax, float ay, float az, float angle)
    {
        if (angle == 0f)
            return;

        float half = angle * 0.5f;
        float dw = Cos(half), dx = ax * Sin(half), dy = ay * Sin(half), dz = az * Sin(half);

        float nw = dw * _qw - dx * _qx - dy * _qy - dz * _qz;
        float nx = dw * _qx + dx * _qw + dy * _qz - dz * _qy;
        float ny = dw * _qy - dx * _qz + dy * _qw + dz * _qx;
        float nz = dw * _qz + dx * _qy - dy * _qx + dz * _qw;

        float norm = Sqrt(nw * nw + nx * nx + ny * ny + nz * nz);
        _qw = nw / norm;
        _qx = nx / norm;
        _qy = ny / norm;
        _qz = nz / norm;
    }

    public override void MouseWheel(float delta)
    {
        _radius = Constrain(_radius - delta * 3, 40, 260);
    }

    public override void MouseClicked()
    {
        _lit = !_lit;
    }
}
";

        private const string Camera3D =
@"// Demo of Camera()/Perspective()/Ortho() -- Size(w, h, RendererKind.Renderer3D)
// directly in Setup(), no offscreen PGraphics (see Box3D's comment
// for why). The mouse orbits the
// camera around the scene's origin (Camera() with eye computed by
// hand instead of the usual translate/rotate -- the camera is its own
// matrix, separate from the model stack). The P key toggles between
// Perspective() (with foreshortening, distant cubes look smaller)
// and Ortho() (parallel projection, every cube measures the same on
// screen regardless of distance) -- compare them over the same grid
// of cubes to notice the difference.
//
// On top of that: the mouse wheel moves the camera closer/farther (it
// changes the orbit radius, not the FOV -- a real zoom, moving the eye),
// and each click cycles through a color palette computed with FillHSB() by
// position in the grid, so you can see that each cube keeps its
// own color while the camera moves around all of them.
public class MySketch : Sketch
{
    private bool _usePerspective = true;
    private float _orbitRadius = 500;
    private float _hueShift;

    public override void Setup() => Size(800, 600, RendererKind.Renderer3D);

    public override void Draw()
    {
        Background(20, 20, 30);

        float orbitAngle = Map(MouseX, 0, Width, 0, TWO_PI);
        float orbitHeight = Map(MouseY, 0, Height, 400, -400);

        float eyeX = Width / 2f + Cos(orbitAngle) * _orbitRadius;
        float eyeZ = Sin(orbitAngle) * _orbitRadius;
        float eyeY = Height / 2f + orbitHeight;

        Lights();

        Camera(eyeX, eyeY, eyeZ, Width / 2f, Height / 2f, 0, 0, 1, 0);

        if (_usePerspective)
            Perspective();
        else
            Ortho(-Width / 2f, Width / 2f, -Height / 2f, Height / 2f);

        for (int gx = -2; gx <= 2; gx++)
        {
            for (int gz = 0; gz <= 4; gz++)
            {
                float hue = (_hueShift + gx * 40 + gz * 25) % 360f;
                if (hue < 0)
                    hue += 360f;
                FillHSB(hue, 55, 90);

                PushMatrix();
                Translate(Width / 2f + gx * 140, Height / 2f, gz * -140);
                Box(80);
                PopMatrix();
            }
        }

        Fill(255);
        TextSize(13);
        string projectionLabel = _usePerspective ? ""Perspective()"" : ""Ortho()"";
Text($""{projectionLabel} -- mouse orbita, rueda hace zoom ({(int)_orbitRadius}) -- tecla P alterna proyeccion -- click cambia paleta"", 12, Height - 16);

    }

    public override void MouseWheel(float delta)
    {
        _orbitRadius = Constrain(_orbitRadius - delta * 8, 200, 900);
    }

    public override void MouseClicked()
    {
        _hueShift += 45;
    }

    public override void KeyPressed()
    {
        if (Key == 'p')
            _usePerspective = !_usePerspective;
    }
}
";

        private const string Lights3D =
        @"// Demo of PointLight()/SpotLight()/LightFalloff() -- Size(w, h,
// RendererKind.Renderer3D) directly in Setup(), no offscreen PGraphics (see
// Box3D's comment for why). The mouse
// moves a light over a grid of spheres, with a dim AmbientLight as a
// base (so the spheres' shape still reads outside the lit
// spot) plus the point/spot light following the mouse. The L key
// toggles between PointLight() (lights in every direction) and SpotLight()
// (a cone pointing downward, with fixed angle and concentration).
//
// On top of that: the mouse wheel adjusts LightFalloff(1, linear, 0)'s
// LINEAR coefficient live -- raising it makes the light fall off
// faster with distance (a small, sharp halo); lowering it lets it light
// up a wider area. Click toggles AmbientLight() on/off to
// compare against total black outside the reach of the main light.
public class MySketch : Sketch
{
    private bool _useSpot;
    private bool _ambientOn = true;
    private float _falloffLinear = 0.0025f;

    public override void Setup() => Size(800, 600, RendererKind.Renderer3D);

    public override void Draw()
    {
        Background(15, 15, 20);

        float lightX = Map(MouseX, 0, Width, Width / 2f - 300, Width / 2f + 300);
        // Symmetric range around the center of the sphere grid
        // (Height/2 = 300 for a 600px-tall canvas) -- it used to go from
        // 60 to 320, almost entirely ABOVE the center (240px above versus
        // only 20px below), so the light practically never felt
        // like it was below the spheres. 60..(Height-60) leaves the same margin
        // on both sides.
        float lightY = Map(MouseY, 0, Height, 60, Height - 60);
        float lightZ = 150;

        if (_ambientOn)
            AmbientLight(25, 25, 32);
        // LightFalloff(1, linear, 0) uses LINEAR attenuation, not quadratic --
        // at this scene's scale (distances of ~100-400 units) a
        // quadratic falloff saturates very fast; the linear one gives a
        // smoother falloff that's easier to calibrate with the mouse wheel.
        LightFalloff(1, _falloffLinear, 0);
        if (_useSpot)
            SpotLight(255, 220, 180, lightX, lightY, lightZ, 0, 1, 0, Radians(35), 8);
        else
            PointLight(255, 220, 180, lightX, lightY, lightZ);

        Fill(210, 210, 210);
        for (int gx = -2; gx <= 2; gx++)
        {
            for (int gz = -1; gz <= 1; gz++)
            {
                PushMatrix();
                Translate(Width / 2f + gx * 130, Height / 2f, gz * 130);
                Sphere(45);
                PopMatrix();
            }
        }

        Fill(255);
        TextSize(13);
       string lightTypeLabel = _useSpot ? ""SpotLight()"" : ""PointLight()"";
string ambientStatus = _ambientOn ? ""ON"" : ""OFF"";
Text($""{lightTypeLabel} -- mouse mueve la luz -- rueda ajusta LightFalloff ({_falloffLinear:F4}) -- tecla L: tipo de luz -- click: ambiente {ambientStatus}"", 12, Height - 16);

    }

    public override void MouseWheel(float delta)
    {
        _falloffLinear = Constrain(_falloffLinear - delta * 0.0003f, 0.0005f, 0.02f);
    }

    public override void MouseClicked()
    {
        _ambientOn = !_ambientOn;
    }

    public override void KeyPressed()
    {
        if (Key == 'l')
            _useSpot = !_useSpot;
    }
}
";

        private const string Material3D =
        @"// Demo of Material Properties -- Ambient()/Specular()/Emissive()/Shininess() --
// Size(w, h, RendererKind.Renderer3D) directly in Setup(), no offscreen
// PGraphics (see Box3D's comment for why). A row of
// spheres with increasing Shininess() from left to
// right shows how the specular highlight tightens and gets brighter
// -- mouse X scales through that whole progression live
// (on the left there's almost no highlight, on the right a small and
// bright one).
//
// Mouse Y interpolates the Specular() color between white and magenta
// via LerpColor() -- it clearly separates the highlight color (the
// light/specular one) from the sphere body's color (the Fill()/ambient one).
// Note the LightSpecular(255,255,255) in Draw(): without it, Shininess() and
// Specular() are NEVER visible, no matter the value -- the highlight
// depends both on the material and on the light contributing specular color
// (which is black by default, just like in real Processing).
// The mouse wheel controls the PointLight()'s brightness (0-255), so
// you can see how the material reacts to more or less incident light without touching
// any property of the material itself. Click cycles through a palette for
// the base Fill(), and the E key turns on a fixed Emissive() on the middle
// sphere -- notice it looks 'lit from within' regardless of the
// light's brightness or position, because Emissive() doesn't depend on it.
public class MySketch : Sketch
{
    private bool _emissiveOn;
    private float _lightBrightness = 255;

    private readonly Color[] _palette =
    {
        new Color(120, 190, 255),
        new Color(255, 150, 150),
        new Color(170, 255, 170),
        new Color(255, 220, 130),
    };
    private int _colorIndex;

    public override void Setup() => Size(800, 600, RendererKind.Renderer3D);

    public override void Draw()
    {
        Background(15, 15, 20);

        float shininessScale = Map(MouseX, 0, Width, 1, 40);
        float specAmt = Map(MouseY, 0, Height, 0, 1);
        var specColor = LerpColor(new Color(255, 255, 255), new Color(255, 80, 200), specAmt);
        int count = 5;

        AmbientLight(40, 40, 48);
        // Without this, Shininess()/Specular() have NO visible effect
        // no matter what value you give them: the specular highlight
        // needs both the material (Specular()/Shininess()) AND the
        // light itself contributing specular color, and LightSpecular() is
        // black (0,0,0) by default -- just like in real Processing, you
        // need to call it explicitly for any light to shine.
        LightSpecular(255, 255, 255);
PointLight(_lightBrightness, _lightBrightness, _lightBrightness, Width / 2f, 120, 220);

for (int i = 0; i < count; i++)
{
    PushMatrix();
    Translate(Width / 2f + (i - (count - 1) / 2f) * 130, Height / 2f, 0);

    Fill(_palette[_colorIndex]);
    Specular(Red(specColor), Green(specColor), Blue(specColor));
   Shininess((i + 1) * shininessScale);

    if (_emissiveOn && i == count / 2)
        Emissive(80, 20, 90);
    else
        Emissive(0, 0, 0);

    Sphere(50);
    PopMatrix();
}

Fill(255);
TextSize(13);
string emissiveStatus = _emissiveOn ? ""ON"" : ""OFF"";
Text($""Shininess() por mouse X -- color de Specular() por mouse Y -- rueda: brillo de luz ({(int)_lightBrightness}) -- click cambia Fill() -- tecla E: Emissive() {emissiveStatus}"", 12, Height - 16);
    }

    public override void MouseWheel(float delta)
{
    _lightBrightness = Constrain(_lightBrightness - delta * 4, 60, 255);
}

public override void MouseClicked()
{
    _colorIndex = (_colorIndex + 1) % _palette.Length;
}

public override void KeyPressed()
{
    if (Key == 'e')
        _emissiveOn = !_emissiveOn;
}
}
";

        private const string PerspectiveDemo3D = @"
// Perspective -- translated from Processing's official sample.
// Move the mouse on X to change the field of view (fov).
// Click to toggle the aspect ratio.
//
// perspective(fov, aspect, near, far) defines a view volume shaped
// like a truncated pyramid: objects near the near plane appear
// at their real size, objects farther away appear smaller
// (foreshortening). cameraZ is recomputed every frame from the fov
// so the cube up front always keeps the same size on
// screen no matter how wide you open the angle -- it's the same formula
// PGraphicsOpenGL uses internally for its default fov/cameraZ.
//
// NOTE: unlike real Processing, Matrix4x4.CreatePerspectiveFieldOfView
// (.NET) requires fieldOfView to be strictly > 0 and < PI -- that's why the fov
// gets clamped with Constrain() before it's used. Without this, the moment the
// sketch starts (MouseX still at 0, before the first mouse movement)
// fov comes out to exactly 0 and it blows up with ArgumentOutOfRangeException.
public class MySketch : Sketch
{
    public override void Setup() => Size(640, 360, RendererKind.Renderer3D);

    public override void Draw()
    {
        Lights();
        Background(0);

        float cameraY = Height / 2f;
        float fov = Constrain(MouseX / (float)Width * (PI / 2f), 0.01f, PI / 2f - 0.01f);
        float cameraZ = cameraY / Tan(fov / 2f);
        float aspect = (float)Width / Height;
        if (IsMousePressed)
            aspect = aspect / 2f;

        Perspective(fov, aspect, cameraZ / 10f, cameraZ * 10f);

        Translate(Width / 2f + 30, Height / 2f, 0);
        RotateX(-PI / 6f);
        RotateY(PI / 3f + MouseY / (float)Height * PI);
        Box(45);
        Translate(0, 0, -50);
        Box(30);
    }
}
";

        private const string Coordinates3D =
@"// Demo of ModelX/Y/Z() + ScreenX/Y/Z() -- Size(w, h, RendererKind.Renderer3D)
// directly in Setup(), no offscreen PGraphics (see Box3D's comment
// for why). The first half is
// Processing's official example for modelX/Y/Z: a cube is placed with
// a series of Translate()/RotateY()/RotateZ()/RotateX(), and BEFORE
// undoing those transformations with PopMatrix() it reads where the
// local origin (0, 0, 0) ended up in world space -- that's exactly what
// ModelX/Y/Z() do. With the transformations already undone, a
// direct Translate(x, y, z) to that position places a second cube in the
// exact same spot, without repeating the chain of rotations.
//
// The second half uses ScreenX/Y() on that same point to project it
// to SCREEN coordinates and draw a 2D label (circle + text)
// stuck to the cube -- a real use case: anchoring 2D UI to a moving
// 3D object, without having to reimplement the camera + perspective
// projection by hand.
public class MySketch : Sketch
{
    public override void Setup() => Size(800, 600, RendererKind.Renderer3D);

    public override void Draw()
    {
        Background(15, 15, 20);

        Lights();

        PushMatrix();
        Translate(Width / 2f, Height / 2f, -200);
        RotateY(1.0f);
        RotateZ(2.0f);
        RotateX(FrameCount / 100f);
        Translate(0, 150, 0);

        Fill(230, 230, 230);
        Box(50);

        // BEFORE PopMatrix(): where the local origin (0,0,0) ended up in
        // world space (ModelX/Y/Z) and in screen space (ScreenX/Y).
        float ax = ModelX(0, 0, 0);
        float ay = ModelY(0, 0, 0);
        float az = ModelZ(0, 0, 0);
        float sx = ScreenX(0, 0, 0);
        float sy = ScreenY(0, 0, 0);

        PopMatrix();

        // Transformations already undone -- Translate(ax, ay, az) places the
        // small cube in exactly the same spot as the big one, proof
        // that ModelX/Y/Z() returned the correct position.
        PushMatrix();
        Translate(ax, ay, az);
        Fill(255, 70, 100);
        Box(18);
        PopMatrix();

        // 2D label anchored to the cube using the position ScreenX/Y()
        // projected -- it moves on its own frame by frame, without touching any
        // 3D matrix from here.
        NoFill();
        Stroke(255, 220, 90);
        StrokeWeight(2);
        Ellipse(sx, sy, 10, 10);
        NoStroke();
        Fill(255, 220, 90);
        TextSize(13);
        Text(""ModelX/Y/Z(0,0,0) -> ScreenX/Y(0,0,0)"", sx + 12, sy + 4);

        Fill(255);
        Text(""Cubo chico = misma posicion que el origen local del grande, leida con ModelX/Y/Z() antes de PopMatrix()"", 12, Height - 16);
    }
}
";

        private const string Shader3D =
@"// Demo of PShader -- Shader()/ResetShader()/LoadShader() -- Size(w, h,
// RendererKind.Renderer3D) directly in Setup(), no offscreen PGraphics (see
// Box3D's comment for why). Since
// LoadShader() reads a GLSL file from disk (just like real Processing),
// and this sample has to be a single self-contained file, the fragment
// shader gets written to a temp file in Setup() and loaded from there
// -- in a normal sketch you'd simply have the .glsl as your own asset
// and call LoadShader(""myShader.frag"") directly.
//
// The example shader is a classic ""normal debug"": instead of using
// the lights or Fill(), it paints each pixel based on which way its normal
// points (vNormal * 0.5 + 0.5, to map the [-1,1] range to a [0,1] color) --
// that's why each face of the sphere shows up a different, clearly
// distinct color from normal lighting, so you can tell at a glance
// that the custom shader is really active. Since it reuses the engine's
// own vertex shader (LoadShader(fragFilename), a single argument), there's
// no need to declare the attribute layout by hand -- see PShader's
// comments for why.
public class MySketch : Sketch
{
    private PShader _normalShader;
    private bool _useCustomShader = true;

    public override void Setup()
    {
        Size(800, 600, RendererKind.Renderer3D);

        string fragSource =
@""#version 330 core
in vec3 vNormal;
in vec3 vWorldPos;
out vec4 FragColor;
void main()
{
    vec3 n = normalize(vNormal);
    FragColor = vec4(n * 0.5 + 0.5, 1.0);
}"";

        string tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), ""dana_normal_debug.frag"");
        System.IO.File.WriteAllText(tempPath, fragSource);
        _normalShader = LoadShader(tempPath);
    }

    public override void Draw()
    {
        Background(15, 15, 20);

        Lights();

        if (_useCustomShader)
            Shader(_normalShader);
        else
            ResetShader();

        Fill(200, 200, 200);
        PushMatrix();
        Translate(Width / 2f, Height / 2f, 0);
        RotateY(FrameCount / 60f);
        RotateX(FrameCount / 90f);
        Sphere(180);
        PopMatrix();

        Fill(255);
        TextSize(13);
        string shaderStatus = _useCustomShader ? ""shader custom (color = normal)"" : ""shading normal (luces + Fill())"";
        Text($""Click para alternar -- ahora: {shaderStatus}"", 12, Height - 16);
    }

    public override void MouseClicked()
    {
        _useCustomShader = !_useCustomShader;
    }
}
";

        private const string Normal3D =
@"// Demo of normal(nx, ny, nz) -- unlike the other samples in this
// list, this one has NO visual effect to show yet: Processing's
// real normal() only affects vertices defined AFTER it, inside
// beginShape()/vertex(), and that per-vertex 3D shape API doesn't exist
// yet in this engine (Box()/Sphere() are the only 3D primitives, and
// they compute their own normals from the mesh -- there are no loose
// vertices for normal() to apply anything to). For now normal()
// just STORES the value you pass it, for the day that API exists.
//
// This sample calls normal() anyway, to document in code how
// the signature is used -- but the sphere below looks identical whether
// you call it or not, and that's exactly what's expected today.
public class MySketch : Sketch
{
    public override void Setup() => Size(800, 600, RendererKind.Renderer3D);

    public override void Draw()
    {
        Background(15, 15, 20);

        Lights();

        // Real call to the API -- it doesn't throw, but today it doesn't change anything
        // visible (see the comment above and the one on Normal() in
        // GraphicsContext.3D.cs).
        Normal(0, 0, 1);

        Fill(140, 200, 255);
        PushMatrix();
        Translate(Width / 2f, Height / 2f, 0);
        RotateY(FrameCount / 80f);
        Sphere(160);
        PopMatrix();

        Fill(255);
        TextSize(13);
        Text(""normal(0, 0, 1) se llama arriba, pero todavia no tiene efecto visible -- hace falta beginShape()/vertex() en 3D para que tenga donde aplicarse."", 12, Height - 16);
    }
}
";
        private const string PresentationMode =
@"// Presentation mode -- demo of FullScreen() + PixelWidth/PixelHeight. The
// drawing itself (a spinning kaleidoscope, pure HSB) doesn't need anything
// special: what's interesting is the HUD at the bottom, which shows all 4
// environment numbers together -- logical Width/Height, real PixelWidth/PixelHeight
// (always equal in this engine, see the comment on PixelWidth in
// Sketch.cs), and DisplayDensity().
public class MySketch : Sketch
{
    private float _t;
 
    public override void Setup()
    {
        Size(700, 500);
        ColorMode(ColorSpaceMode.HSB);
    }
 
    public override void Draw()
    {
        Background(230, 20, 12);
 
        PushMatrix();
        Translate(Width / 2f, Height / 2f);
 
        int arms = 10;
        _t += 0.01f;
 
        for (int i = 0; i < arms; i++)
        {
            PushMatrix();
            Rotate(TWO_PI / arms * i + _t);
            NoStroke();
            for (int r = 0; r < 6; r++)
            {
                float hue = (_t * 60 + r * 40) % 360;
                FillHSB(hue, 80, 90);
                Circle(80 + r * 30, 0, 18);
            }
            PopMatrix();
        }
 
        PopMatrix();
 
        Fill(0, 0, 100);
        TextSize(13);
        Text($""tecla F: FullScreen() -- ahora: {IsFullScreen} -- {Width}x{Height} lógicos, {PixelWidth}x{PixelHeight} reales (density {DisplayDensity()}x)"", 12, Height - 16);
    }
 
    public override void KeyPressed()
    {
        if (Key == 'f')
            FullScreen(!IsFullScreen);
    }
}
";

        private const string WindowControlPanel =
@"
using System.Collections.Generic;
// Window control panel -- demo of WindowMove()/WindowResizable()/WindowTitle()/
// WindowRatio(), all exposed as events that a real host (the IDE's
// own window, for example) can listen to and actually move/resize/retitle
// the window. This sample doesn't depend on that wiring already
// existing: each key calls the function regardless, and the on-screen log
// shows the call and the resulting state (IsFullScreen,
// IsWindowResizable, WindowTitleText) read directly from the Sketch --
// that always works, whether or not a host that reacts is connected.
public class MySketch : Sketch
{
    private readonly List<string> _log = new List<string>();
    private readonly string[] _titles = { ""Boceto A"", ""Boceto B"", ""DanaProcessing Live"", ""Sin titulo"" };
 
    public override void Setup()
    {
        Size(640, 420);
        WindowTitle(""DanaProcessing -- Panel de ventana"");
        _log.Add($""WindowTitle('{WindowTitleText}')"");
    }
 
    public override void Draw()
    {
        Background(24, 26, 34);
 
        Fill(255);
        TextSize(14);
        Text(""F: FullScreen()   M: WindowMove()   R: WindowResizable()   T: WindowTitle()   A: WindowRatio(16,9)"", 16, 28);
 
        TextSize(12);
        Fill(180, 200, 255);
        for (int i = 0; i < _log.Count; i++)
            Text(_log[_log.Count - 1 - i], 16, 60 + i * 20);
 
        while (_log.Count > 12)
            _log.RemoveAt(0);
    }
 
    public override void KeyPressed()
    {
        switch (Key)
        {
            case 'f':
                FullScreen(!IsFullScreen);
                _log.Add($""FullScreen({!IsFullScreen}) -> IsFullScreen={IsFullScreen}"");
                break;
 
            case 'm':
                int x = (int)Random(0, 400);
                int y = (int)Random(0, 300);
                WindowMove(x, y);
                _log.Add($""WindowMove({x}, {y})"");
                break;
 
            case 'r':
                WindowResizable(!IsWindowResizable);
                _log.Add($""WindowResizable({IsWindowResizable}) -> IsWindowResizable={IsWindowResizable}"");
                break;
 
            case 't':
                string title = _titles[(int)Random(_titles.Length)];
                WindowTitle(title);
                _log.Add($""WindowTitle('{title}') -> WindowTitleText='{WindowTitleText}'"");
                break;
 
            case 'a':
                WindowRatio(16, 9);
                _log.Add(""WindowRatio(16, 9)"");
                break;
        }
    }
}
";

        private const string DynamicSizeSettings =
@"// Dynamic size -- demo of Settings(). Unlike every other
// sample in this list (which call Size() inside Setup()), this one
// calls it inside Settings() -- the hook that runs BEFORE Setup(), just
// like in real Processing. The practical difference: by the time Setup()
// starts, Width/Height ALREADY reflect what Settings() decided, without
// depending on the order in which the host calls the lifecycle methods.
// In plain C# this isn't strictly necessary (Size() works just as
// well called directly in Setup()), but it keeps the same guaranteed
// order that sketches ported from Processing expect.
public class MySketch : Sketch
{
    private bool _portrait;
 
    public override void Settings()
    {
        _portrait = Random(1f) < 0.5f;
        Size(_portrait ? 400 : 700, _portrait ? 700 : 400);
    }
 
    public override void Setup()
    {
        // Width/Height are already set by Settings() -- no need to
        // call Size() again here.
        ColorMode(ColorSpaceMode.HSB);
    }
 
    public override void Draw()
    {
        Background(210, 15, 15);
 
        NoStroke();
        for (int i = 0; i < 40; i++)
        {
            float y = Map(i, 0, 40, 0, Height);
            float hue = Map(i, 0, 40, 190, 260);
            FillHSB(hue, 70, 90);
            Rect(0, y, Width, Height / 40f + 1);
        }
 
        Fill(0, 0, 100);
        TextSize(16);
        string orientation = _portrait ? ""retrato"" : ""paisaje"";
        Text($""Settings() eligió {orientation} antes de Setup() -- {Width}x{Height}"", 20, 34);
        TextSize(13);
        Text(""click para volver a elegir con Size() directo (ya en Draw, no en Settings)"", 20, Height - 20);
    }
 
    public override void MouseClicked()
    {
        _portrait = !_portrait;
        Size(_portrait ? 400 : 700, _portrait ? 700 : 400);
    }
}
";
        private const string BinaryCounter =
@"// Visual binary counter -- demo of Binary()/Unbinary(). A value from 0 to
// 255 is shown as 8 cells (bits): Binary((byte)value) builds the 8-character
// string, and Unbinary() converts it back into a number to
// prove that the round trip gives back the same value.
public class MySketch : Sketch
{
    private int _value;
    private float _accum;
    private float _speed = 2f;
 
    public override void Setup()
    {
        Size(500, 260);
    }
 
    public override void Draw()
    {
        Background(18, 20, 26);
 
        _accum += 1f / Max(_speed, 0.1f);
        if (_accum >= 1f)
        {
            _accum = 0;
            _value = (_value + 1) % 256;
        }
 
        string bits = Binary((byte)_value);
        float cellSize = 50;
        float startX = (Width - cellSize * 8) / 2f;
 
        for (int i = 0; i < 8; i++)
        {
            bool on = bits[i] == '1';
            float x = startX + i * cellSize;
 
            Fill(on ? new Color(255, 200, 60) : new Color(45, 48, 58));
            Stroke(70, 70, 80);
            StrokeWeight(2);
            Rect(x, 90, cellSize - 6, cellSize - 6);
 
            Fill(on ? new Color(30, 30, 20) : new Color(120, 120, 130));
            TextSize(20);
            Text(bits[i].ToString(), x + cellSize / 2f - 6, 90 + cellSize / 2f + 8);
        }
 
        Fill(255);
        TextSize(15);
        Text($""Binary({_value}) = {bits}   ->   Unbinary(bits) = {Unbinary(bits)}"", startX, 60);
        TextSize(12);
        Text(""rueda: velocidad del contador -- click en un bit para forzarlo a mano"", startX, 200);
    }
 
    public override void MouseWheel(float delta)
    {
        _speed = Constrain(_speed - delta * 0.3f, 0.2f, 30f);
    }
 
    public override void MouseClicked()
    {
        float cellSize = 50;
        float startX = (Width - cellSize * 8) / 2f;
        int index = (int)((MouseX - startX) / cellSize);
        if (index >= 0 && index < 8 && MouseY > 90 && MouseY < 90 + cellSize)
        {
            int bitFromLeft = 7 - index;
            _value ^= 1 << bitFromLeft;
        }
    }
}
";

        private const string PrimeSearchThread =
@"// Parallel search -- demo of Thread(). SearchPrimes() is a plain,
// ordinary parameterless method; Thread(nameof(SearchPrimes)) finds it
// via reflection and runs it on a separate thread. Meanwhile, Draw() keeps
// running at its own pace (the spinner never stalls), because the heavy
// work of the prime loop lives entirely on another thread. The data
// handoff between the two threads is deliberately simple -- just a couple
// of volatile fields that one thread writes and the other reads, no locks.
public class MySketch : Sketch
{
    private volatile int _primesFound;
    private volatile long _lastPrime;
    private volatile bool _searching;
    private float _spin;
 
    public override void Setup()
    {
        Size(600, 300);
        StartSearch();
    }
 
    public override void Draw()
    {
        Background(20, 22, 30);
 
        if (_searching)
            _spin += 6f;
 
        PushMatrix();
        Translate(90, Height / 2f);
        Rotate(Radians(_spin));
        NoFill();
        Stroke(_searching ? new Color(255, 200, 60) : new Color(80, 200, 120));
        StrokeWeight(6);
        Arc(0, 0, 70, 70, 0, Radians(270));
        PopMatrix();
 
        Fill(255);
        TextSize(18);
        Text(_searching ? ""Buscando primos en un hilo aparte..."" : ""Búsqueda terminada"", 150, Height / 2f - 30);
        TextSize(14);
        Text($""Encontrados hasta ahora: {_primesFound}"", 150, Height / 2f);
        Text($""Último primo: {_lastPrime}"", 150, Height / 2f + 24);
        TextSize(12);
        Text(""el spinner de la izquierda nunca se traba mientras tanto -- click reinicia la búsqueda"", 20, Height - 20);
    }
 
    public override void MouseClicked()
    {
        if (!_searching)
            StartSearch();
    }
 
    private void StartSearch()
    {
        _primesFound = 0;
        _lastPrime = 0;
        _searching = true;
        Thread(nameof(SearchPrimes));
    }
 
    // No parameters -- exactly what Thread() looks for via reflection.
    private void SearchPrimes()
    {
        for (long n = 2; n < 2_000_000; n++)
        {
            if (IsPrime(n))
            {
                _primesFound++;
                _lastPrime = n;
            }
        }
        _searching = false;
    }
 
    private static bool IsPrime(long n)
    {
        if (n < 2)
            return false;
        for (long d = 2; d * d <= n; d++)
        {
            if (n % d == 0)
                return false;
        }
        return true;
    }
}
";

        private const string PdfExport =
@"// Export to PDF -- demo of BeginRaw()/EndRaw(). DrawPoster() is an ordinary
// method that draws the scene -- Draw() calls it every frame to show it
// on screen, just like any sketch. The V key calls that SAME
// method one more time, but wrapped in BeginRaw('poster.pdf') and
// EndRaw() -- during that extra call, everything DrawPoster() draws
// gets redirected to the PDF page instead of the screen (which is why the screen
// doesn't 'flicker' or change at that instant), and once EndRaw() is called the file
// ends up written with the same scene, but as real vectors -- you can
// open it and zoom in infinitely without it pixelating, unlike a
// screenshot.
public class MySketch : Sketch
{
    private float _t;
 
    public override void Setup()
    {
        Size(600, 600);
        ColorMode(ColorSpaceMode.HSB);
    }
 
    public override void Draw()
    {
        _t += 0.01f;
        DrawPoster();
 
        Fill(0, 0, 100);
        TextSize(13);
        Text(""tecla V: exporta este frame a poster.pdf (vectorial, via BeginRaw/EndRaw)"", 16, Height - 16);
    }
 
    public override void KeyPressed()
    {
        if (Key == 'v')
        {
            BeginRaw(""poster.pdf"");
            DrawPoster();
            EndRaw();
        }
    }
 
    private void DrawPoster()
    {
        Background(0, 0, 8);
        PushMatrix();
        Translate(Width / 2f, Height / 2f);
        NoStroke();
        for (int i = 0; i < 200; i++)
        {
            float angle = i * 0.3f + _t;
            float radius = i * 1.3f;
            float hue = (i * 2 + _t * 40) % 360;
            FillHSB(hue, 70, 90);
            Circle(Cos(angle) * radius, Sin(angle) * radius, 14);
        }
        PopMatrix();
    }
}
";

        private const string StrokeBackup =
@"// Stroke backup -- demo of SaveStream(). The stroke drawn with the
// mouse is first saved as text with SaveStrings() (a function that already
// existed), and then the S key opens THAT file with CreateInput() and
// copies it whole to a backup with a unique name using SaveStream(path, input)
// -- a file-to-file copy without reading it by hand line by line.
public class MySketch : Sketch
{
    private readonly List<float> _xs = new List<float>();
    private readonly List<float> _ys = new List<float>();
    private string _status = ""dibuja con el mouse -- tecla S: guarda + backup"";
 
    public override void Setup()
    {
        Size(640, 420);
    }
 
    public override void Draw()
    {
        Background(24, 26, 32);
 
        Stroke(255, 210, 90);
        StrokeWeight(3);
        NoFill();
        for (int i = 1; i < _xs.Count; i++)
            Line(_xs[i - 1], _ys[i - 1], _xs[i], _ys[i]);
 
        if (IsMousePressed)
        {
            _xs.Add(MouseX);
            _ys.Add(MouseY);
        }
 
        Fill(255);
        TextSize(13);
        Text(_status, 14, 24);
    }
 
    public override void KeyPressed()
    {
        if (Key != 's')
            return;
 
        var lines = new string[_xs.Count];
        for (int i = 0; i < _xs.Count; i++)
            lines[i] = $""{_xs[i]},{_ys[i]}"";
        SaveStrings(""trazo.txt"", lines);
 
        string backupPath = $""trazo_backup_{Millis()}.txt"";
        using (var input = CreateInput(""trazo.txt""))
            SaveStream(backupPath, input);
 
        _status = $""guardado trazo.txt ({_xs.Count} puntos) y copiado a {backupPath} con SaveStream()"";
    }
}
";

        private const string DataCardParseLaunch =
@"
using System.Collections.Generic;
// Data card -- demo of ParseJSONObject()/ParseXML() + Launch(). The
// JSON and the XML are first built with the normal API (JSONObject/XML) and
// serialized to text with ToString() -- simulating data that arrived as a
// STRING (a network response, a pasted text field), not as a
// file on disk. Then THAT text gets read back with
// ParseJSONObject()/ParseXML(), which is the new function. The L key opens
// the site saved in the JSON with Launch(), in the operating system's
// browser.
public class MySketch : Sketch
{
    private JSONObject _data;
    private string _bio;
 
    public override void Setup()
    {
        Size(520, 280);
 
        var built = new JSONObject();
        built.SetString(""name"", ""Processing Foundation"");
        built.SetInt(""founded"", 2012);
        built.SetString(""site"", ""https://processing.org"");
        string json = built.ToString();
        _data = ParseJSONObject(json);
 
        string xmlText = ""<bio>Fundación sin fines de lucro detrás de Processing y p5.js.</bio>"";
        var xml = ParseXML(xmlText);
        _bio = xml.GetContent(""(sin descripción)"");
    }
 
    public override void Draw()
    {
        Background(26, 28, 36);
 
        Fill(255);
        TextSize(22);
        Text(_data.GetString(""name""), 30, 60);
 
        TextSize(14);
        Fill(200, 210, 230);
        Text($""Fundada en {_data.GetInt(""founded"")}"", 30, 92);
        Text(_bio, 30, 118);
 
        TextSize(13);
        Fill(160, 200, 255);
        Text(""tecla L: abre "" + _data.GetString(""site"") + "" con Launch()"", 30, Height - 20);
    }
 
    public override void KeyPressed()
    {
        if (Key == 'l')
            Launch(_data.GetString(""site""));
    }
}
";
        private const string FacetedGem =
    @"
using System.Collections.Generic;
// Faceted gem -- demo of BeginShape(Triangles) + Vertex(x,y,z) + Normal()
// under Renderer3D, the piece that was missing until now: normal() already
// had somewhere to store its value but no vertex that used it. An octahedron (8
// triangular faces) is built by hand, face by face -- each face calls Normal()
// BEFORE its 3 Vertex() calls, and that normal is what stays attached to those 3
// vertices when EndShape() uploads the mesh.
//
// FLAT shading: each face's normal comes from PVector.Cross() between
// two of its edges (the PVector method that adds the cross product in
// 3D) -- that's why each face looks like a solid facet, with a sharp
// edge against its neighbor, just like a real cut gem.
// SMOOTH shading: on a regular octahedron centered at the origin, each
// vertex's 'averaged' normal is simply its own direction from the
// center (normalized) -- so there's not even a need to average faces by
// hand, and the facets blend into a surface that looks rounded.
public class MySketch : Sketch
{
    private float _qw = 1f, _qx, _qy, _qz;
    private float _spinAxisX = 1f, _spinAxisY, _spinAxisZ, _spinAngle;
    private bool _smooth;
    private float _radius = 150;

    public override void Setup() => Size(700, 500, RendererKind.Renderer3D);

    public override void Draw()
    {
        Background(15, 15, 20);

        if (IsMousePressed)
        {
            float dx = MouseX - PMouseX;
            float dy = MouseY - PMouseY;
            float dragMag = Mag(dx, dy);
            if (dragMag > 0.001f)
            {
                _spinAxisX = -dy / dragMag;
                _spinAxisY = dx / dragMag;
                _spinAxisZ = 0f;
                _spinAngle = dragMag * 0.01f;
            }
        }
        ApplySpin(_spinAxisX, _spinAxisY, _spinAxisZ, _spinAngle);
        if (!IsMousePressed)
            _spinAngle *= 0.98f;

        Lights();
        Fill(160, 220, 255);
        PushMatrix();
        Translate(Width / 2f, Height / 2f, 0);

        float angle = 2f * Acos(Constrain(_qw, -1f, 1f));
        float s = Sqrt(Max(1f - _qw * _qw, 0f));
        if (s < 0.0001f)
            Rotate(angle, 1, 0, 0);
        else
            Rotate(angle, _qx / s, _qy / s, _qz / s);

        DrawGem(_radius, _smooth);

        PopMatrix();

        Fill(255);
        TextSize(13);
        string shadingLabel = _smooth ? ""suave (normales promediadas)"" : ""plana (una normal por cara)"";
        Text($""BeginShape+Vertex(x,y,z)+Normal() -- sombreado {shadingLabel} -- tecla N alterna -- rueda escala -- arrastrá para rotar"", 12, Height - 16);
    }
 
    public override void KeyPressed()
    {
        if (Key == 'n')
            _smooth = !_smooth;
    }
 
    public override void MouseWheel(float delta)
    {
        _radius = Constrain(_radius - delta * 3, 60, 260);
    }
 
    private void DrawGem(float r, bool smooth)
    {
        var points = new PVector[]
        {
            new PVector(0, -r, 0),   // 0: top
            new PVector(0,  r, 0),   // 1: bottom
            new PVector( r, 0,  0),  // 2: equator +X
            new PVector( 0, 0,  r),  // 3: equator +Z
            new PVector(-r, 0,  0),  // 4: equator -X
            new PVector( 0, 0, -r),  // 5: equator -Z
        };

        var smoothNormals = new PVector[points.Length];
        for (int i = 0; i < points.Length; i++)
            smoothNormals[i] = points[i].Copy().Normalize();

        int[,] faces =
        {
            { 0, 2, 3 }, { 0, 3, 4 }, { 0, 4, 5 }, { 0, 5, 2 },
            { 1, 3, 2 }, { 1, 4, 3 }, { 1, 5, 4 }, { 1, 2, 5 },
        };

        BeginShape(ShapeKind.Triangles);
        for (int f = 0; f < faces.GetLength(0); f++)
        {
            int ia = faces[f, 0], ib = faces[f, 1], ic = faces[f, 2];
            var a = points[ia];
            var b = points[ib];
            var c = points[ic];

            if (smooth)
            {
                Normal(smoothNormals[ia].X, smoothNormals[ia].Y, smoothNormals[ia].Z);
                Vertex(a.X, a.Y, a.Z);
                Normal(smoothNormals[ib].X, smoothNormals[ib].Y, smoothNormals[ib].Z);
                Vertex(b.X, b.Y, b.Z);
                Normal(smoothNormals[ic].X, smoothNormals[ic].Y, smoothNormals[ic].Z);
                Vertex(c.X, c.Y, c.Z);
            }
            else
            {
                var faceNormal = PVector.Cross(PVector.Sub(b, a), PVector.Sub(c, a)).Normalize();
                Normal(faceNormal.X, faceNormal.Y, faceNormal.Z);
                Vertex(a.X, a.Y, a.Z);
                Vertex(b.X, b.Y, b.Z);
                Vertex(c.X, c.Y, c.Z);
            }
        }
        EndShape();
    }
 
    private void ApplySpin(float ax, float ay, float az, float angle)
    {
        if (angle == 0f)
            return;
 
        float half = angle * 0.5f;
        float dw = Cos(half), dx = ax * Sin(half), dy = ay * Sin(half), dz = az * Sin(half);
 
        float nw = dw * _qw - dx * _qx - dy * _qy - dz * _qz;
        float nx = dw * _qx + dx * _qw + dy * _qz - dz * _qy;
        float ny = dw * _qy - dx * _qz + dy * _qw + dz * _qx;
        float nz = dw * _qz + dx * _qy - dy * _qx + dz * _qw;
 
        float norm = Sqrt(nw * nw + nx * nx + ny * ny + nz * nz);
        _qw = nw / norm;
        _qx = nx / norm;
        _qy = ny / norm;
        _qz = nz / norm;
    }
}
";

        private const string Particles3D =
@"
using System.Collections.Generic;
// 3D particles -- demo of PVector with Z. PVector used to only have X and Y;
// now Add()/Sub()/Mult()/Normalize()/etc. operate on all three dimensions,
// so a particle system with real position/velocity/gravity
// in 3D is written exactly like a 2D one -- without having to manage
// a separate loose 'z' float by hand.
public class MySketch : Sketch
{
    private readonly List<PVector> _pos = new List<PVector>();
    private readonly List<PVector> _vel = new List<PVector>();
    private const float BoxHalf = 200;

    public override void Setup()
    {
        Size(700, 500, RendererKind.Renderer3D);
        for (int i = 0; i < 40; i++)
            SpawnParticle();
    }

    public override void Draw()
    {
        Background(12, 14, 20);

        Lights();
        PushMatrix();
        Translate(Width / 2f, Height / 2f, 0);
        RotateY(FrameCount / 200f); // rotates the whole scene so the depth stands out

        var gravity = new PVector(0, 0.15f, 0);

        for (int i = 0; i < _pos.Count; i++)
        {
            var p = _pos[i];
            var v = _vel[i];

            v.Add(gravity);
            p.Add(v);

            // Elastic bounce against the 6 walls of an invisible cube --
            // each axis is checked separately, but it's the SAME PVector in
            // all three cases thanks to the new Z support.
            if (Abs(p.X) > BoxHalf) { p.X = Constrain(p.X, -BoxHalf, BoxHalf); v.X *= -0.8f; }
            if (Abs(p.Y) > BoxHalf) { p.Y = Constrain(p.Y, -BoxHalf, BoxHalf); v.Y *= -0.8f; }
            if (Abs(p.Z) > BoxHalf) { p.Z = Constrain(p.Z, -BoxHalf, BoxHalf); v.Z *= -0.8f; }

            _pos[i] = p;
            _vel[i] = v;

            PushMatrix();
            Translate(p.X, p.Y, p.Z);
            FillHSB(Map(p.Y, -BoxHalf, BoxHalf, 0, 300), 70, 90);
            Sphere(10);
            PopMatrix();
        }

        PopMatrix();

        Fill(255);
        TextSize(13);
        Text($""PVector en 3D (X,Y,Z) -- {_pos.Count} partículas rebotando en un cubo -- click agrega más"", 12, Height - 16);
    }
 
    public override void MouseClicked()
    {
        for (int i = 0; i < 10; i++)
            SpawnParticle();
    }
 
    private void SpawnParticle()
    {
        _pos.Add(new PVector(Random(-50, 50), -BoxHalf + 10, Random(-50, 50)));
        _vel.Add(new PVector(Random(-2f, 2f), 0, Random(-2f, 2f)));
    }
}
";
        private const string ReusableSwarm =
 @"
using System.Collections.Generic;
// Reusable swarm -- demo of CreateShape3D(). The gem's geometry
// (the same faceted octahedron from the previous sample) is built just ONCE
// in Setup() -- CreateShape3D() records the same Vertex()/Normal() calls
// we'd use with BeginShape()/EndShape(), but instead of drawing
// immediately, it uploads the mesh to a persistent VAO/VBO and returns a
// PShape. From then on, each gem in the swarm is drawn with a simple
// Shape() -- without re-triangulating on the CPU or re-uploading data to
// the GPU every frame, which is exactly what the 'Faceted gem' sample DID
// do (on purpose, to show the contrast).
public class MySketch : Sketch
{
    private PShape _gemShape;
    private readonly List<(float x, float y, float z, float spin, float angle)> _gems =
        new List<(float, float, float, float, float)>();

    public override void Setup()
    {
        Size(800, 600, RendererKind.Renderer3D);

        // CreateShape3D() claims and releases the GL context on its own
        // -- it can be called standalone, just once, without BeginDraw()/EndDraw().
        _gemShape = CreateShape3D(ShapeKind.Triangles, () => BuildGemVertices(22));

        for (int i = 0; i < 150; i++)
        {
            _gems.Add((
                Random(-320, 320),
                Random(-220, 220),
                Random(-320, 320),
                Random(0.3f, 1.5f),
                Random(TWO_PI)));
        }
    }

    public override void Draw()
    {
        Background(12, 14, 20);

        Lights();
        Fill(180, 220, 255);
        PushMatrix();
        Translate(Width / 2f, Height / 2f, 0);
        RotateY(FrameCount / 300f);

        for (int i = 0; i < _gems.Count; i++)
        {
            var g = _gems[i];
            float angle = g.angle + FrameCount * 0.01f * g.spin;

            PushMatrix();
            Translate(g.x, g.y, g.z);
            RotateY(angle);
            RotateX(angle * 0.6f);
            Shape(_gemShape, 0, 0);
            PopMatrix();
        }

        PopMatrix();

        Fill(255);
        TextSize(13);
        Text($""CreateShape3D() x1 (subida una vez) -- Shape() x{_gems.Count} por frame, sin re-triangular ni re-subir nada"", 12, Height - 16);
    }

    private void BuildGemVertices(float r)
    {
        var points = new PVector[]
        {
            new PVector(0, -r, 0), new PVector(0, r, 0),
            new PVector(r, 0, 0), new PVector(0, 0, r), new PVector(-r, 0, 0), new PVector(0, 0, -r),
        };
        int[,] faces =
        {
            { 0, 2, 3 }, { 0, 3, 4 }, { 0, 4, 5 }, { 0, 5, 2 },
            { 1, 3, 2 }, { 1, 4, 3 }, { 1, 5, 4 }, { 1, 2, 5 },
        };

        for (int f = 0; f < faces.GetLength(0); f++)
        {
            int ia = faces[f, 0], ib = faces[f, 1], ic = faces[f, 2];
            var a = points[ia];
            var b = points[ib];
            var c = points[ic];

            var normal = PVector.Cross(PVector.Sub(b, a), PVector.Sub(c, a)).Normalize();
            Normal(normal.X, normal.Y, normal.Z);
            Vertex(a.X, a.Y, a.Z);
            Vertex(b.X, b.Y, b.Z);
            Vertex(c.X, c.Y, c.Z);
        }
    }
}
";
        private const string TexturedBillboard =
@"// Textured billboard -- demo of Vertex(x,y,z,u,v) + Texture() under
// Renderer3D. The texture doesn't come from a file: it's generated by drawing
// a pattern with the usual 2D API onto a small PGraphics, and Get() turns
// it into an ordinary PImage -- Texture()/Vertex(...,u,v) don't distinguish
// between an image generated in code and one loaded with LoadImage(), so
// both work the same way.
//
// The panel is a single Quad with the texture's 4 corners (u,v) going from
// (0,0) to (1,1) -- CreateShape3D() uploads it to the GPU just once in
// Setup(), and after that each frame it's just drawn with Shape().
public class MySketch : Sketch
{
    private PImage _texture;
    private PShape _panel;

    private float _qw = 1f, _qx, _qy, _qz;
    private float _spinAxisX, _spinAxisY = 1f, _spinAxisZ, _spinAngle;

    public override void Setup()
    {
        Size(700, 500, RendererKind.Renderer3D);

        _texture = BuildCheckerTexture();

        // NOTE: BeginShape() (called internally by CreateShape3D())
        // resets _shapeTexture to null -- that's why Texture() goes INSIDE the
        // callback, as the first line, instead of before CreateShape3D().
        _panel = CreateShape3D(ShapeKind.Quads, () =>
        {
            Texture(_texture);

            float s = 180;
            Normal(0, 0, 1);
            Vertex(-s, -s, 0, 0, 0);
            Vertex(s, -s, 0, 1, 0);
            Vertex(s, s, 0, 1, 1);
            Vertex(-s, s, 0, 0, 1);
        });
    }

    public override void Draw()
    {
        Background(15, 15, 20);

        if (IsMousePressed)
        {
            float dx = MouseX - PMouseX;
            float dy = MouseY - PMouseY;
            float dragMag = Mag(dx, dy);
            if (dragMag > 0.001f)
            {
                _spinAxisX = -dy / dragMag;
                _spinAxisY = dx / dragMag;
                _spinAxisZ = 0f;
                _spinAngle = dragMag * 0.01f;
            }
        }
        ApplySpin(_spinAxisX, _spinAxisY, _spinAxisZ, _spinAngle);
        if (!IsMousePressed)
            _spinAngle *= 0.98f;

        Lights();
        Fill(255);
        PushMatrix();
        Translate(Width / 2f, Height / 2f, 0);

        float angle = 2f * Acos(Constrain(_qw, -1f, 1f));
        float s = Sqrt(Max(1f - _qw * _qw, 0f));
        if (s < 0.0001f)
            Rotate(angle, 1, 0, 0);
        else
            Rotate(angle, _qx / s, _qy / s, _qz / s);

        Shape(_panel, 0, 0);

        PopMatrix();

        Fill(255);
        TextSize(13);
        Text(""CreateShape3D(Quads) + Vertex(x,y,z,u,v) + Texture() -- arrastrá para rotar"", 12, Height - 16);
    }
 
    // Generates an 8x8 checkerboard on a small 2D PGraphics and returns it as a
    // PImage -- exactly the same API you'd already use to draw on
    // screen (Rect/Fill), except here the result gets saved as a
    // texture instead of being shown directly.
    private PImage BuildCheckerTexture()
    {
        var tex = CreateGraphics(256, 256, RendererKind.Renderer2D);
        tex.BeginDraw();
        int cells = 8;
        float cellSize = 256f / cells;
        for (int gy = 0; gy < cells; gy++)
        {
            for (int gx = 0; gx < cells; gx++)
            {
                bool dark = (gx + gy) % 2 == 0;
                float hue = Map(gx, 0, cells, 0, 300);
                if (dark)
                    tex.FillHSB(hue, 70, 40);
                else
                    tex.FillHSB(hue, 40, 95);
                tex.NoStroke();
                tex.Rect(gx * cellSize, gy * cellSize, cellSize, cellSize);
            }
        }
        tex.EndDraw();
        var img = tex.Get();
        tex.Dispose();
        return img;
    }
 
    private void ApplySpin(float ax, float ay, float az, float angle)
    {
        if (angle == 0f)
            return;
 
        float half = angle * 0.5f;
        float dw = Cos(half), dx = ax * Sin(half), dy = ay * Sin(half), dz = az * Sin(half);
 
        float nw = dw * _qw - dx * _qx - dy * _qy - dz * _qz;
        float nx = dw * _qx + dx * _qw + dy * _qz - dz * _qy;
        float ny = dw * _qy - dx * _qz + dy * _qw + dz * _qx;
        float nz = dw * _qz + dx * _qy - dy * _qx + dz * _qw;
 
        float norm = Sqrt(nw * nw + nx * nx + ny * ny + nz * nz);
        _qw = nw / norm;
        _qx = nx / norm;
        _qy = ny / norm;
        _qz = nz / norm;
    }
}
";

        private const string AudioReactive =
@"// nuget: NAudio, 2.2.1
#nullable enable

using System;
using NAudio.Wave;

// Audio-reactive spectrum -- captures whatever the system is currently
// playing (WasapiLoopbackCapture: system audio output, no microphone
// needed), runs it through an FFT (NAudio.Dsp.FastFourierTransform) to drive
// 40 frequency ""blobs"" arranged around a ring, each a bright glowing head
// on a tapered tail anchored to the ring -- and when the audio goes quiet
// (or you stop capture with S), every blob tears loose from wherever it
// currently is and drifts outward like a small explosion, floating with
// its own gentle inertia (Perlin noise, not a one-shot jump) until sound
// picks up again, at which point every blob eases back into formation.
// The glow itself is faked the classic 2D way (no shaders here): the same
// circle drawn several times, each pass bigger and more transparent than
// the last.
//
// Frequency bins are grouped onto the 40 bars LOGARITHMICALLY, not in
// equal-sized chunks -- real audio energy is concentrated at low
// frequencies (bin-for-bin, not just in total), so a naive equal-width
// split made the very first bar (the lowest frequencies) massively louder
// than every other bar and look like a broken outlier. Splitting bins the
// way a real spectrum analyzer does (proportionally wider bands as
// frequency rises, like octaves) plus a mild gain ramp toward the higher
// bars is what makes all 40 read as one coherent, evenly-alive spectrum
// instead of ""one giant bar plus 39 quiet ones.""
//
// Known caveat: nothing in the engine calls a cleanup hook on the old
// Sketch instance when you press Run again (there's no IDisposable/Stop()
// contract yet), so the WASAPI capture started in Setup() keeps running
// in the background even after a re-Run. It's harmless -- Windows allows
// many simultaneous loopback captures, and the orphaned one just feeds a
// Sketch instance nobody's drawing anymore -- but press S first if you'd
// rather stop it by hand before closing the IDE.
public class MySketch : Sketch
{
    private const int FftSize = 1024;   // must be a power of two
    private const int FftLog2 = 10;     // log2(FftSize)
    private const int Bars = 40;
    private const int MinBin = 1;       // skip bin 0 (DC offset)
    private const int MaxBin = 400;     // first 400 of 512 usable bins (~18.7kHz at 48kHz)
    private const float Gain = 26f;     // raw FFT magnitudes are small; scale up to fill the bars
    private const float Decay = 0.85f;  // per-update falloff, so bars settle instead of flickering

    private const float BaseRadius = 90f;
    private const float MinLen = 18f;   // even a silent bar shows a small dot, not nothing
    private const float MaxLen = 220f;

    // --- Silence detection, with hysteresis so mid-dynamic passages don't
    // flicker between exploded/reassembled. Two different thresholds:
    // you have to fall below SilenceEnter to explode, and rise above
    // SilenceExit to reassemble. The gap between them is the dead zone.
    private const float SilenceEnter = 0.085f;
    private const float SilenceExit  = 0.095f;
    private const int SilenceHoldMs = 40;

    // --- Explosion timing. Base durations are modulated by how loud it
    // was just before the drop (a quiet fade-out tears loose slowly and
    // dreamily; a hard cut snaps apart fast and violently).
    private const float ExplodeMsMin = 0f;
    private const float ExplodeMsMax = 0.00001f;
    private const float ReassembleMsMin = 450f;
    private const float ReassembleMsMax = 900f;

    // --- Per-blob flight sim tuning.
    private const float WanderSpeed = 0.0006f;  // how fast the steering noise field is sampled over time (per ms)
    private const float WanderAccel = 340f;     // px/s^2 -- how hard the noise-driven steering pushes
    private const float FlightHomeK = 2.6f;     // spring constant back toward home (scaled by reattachment)
    private const float FlightDrag = 1.6f;      // velocity damping, always-on
    private const float MaxFlightSpeed = 90f;   // px/s cap, always-on

    // One-off outward velocity kick at the instant the thread lets go.
    // Scaled up by how much energy was present just before the drop.
    private const float ReleaseKickMin = 50f;
    private const float ReleaseKickMax = 110f;

    // Log-spaced bin boundaries -- BinBoundaries[b]..BinBoundaries[b+1] (exclusive)
    // are the FFT bins bar b averages, computed once.
    private static readonly int[] BinBoundaries = BuildBinBoundaries();

    private static int[] BuildBinBoundaries()
    {
        var boundaries = new int[Bars + 1];
        for (int b = 0; b <= Bars; b++)
        {
            float frac = (float)b / Bars;
            float bin = MinBin * MathF.Pow((float)MaxBin / MinBin, frac);
            boundaries[b] = (int)MathF.Round(bin);
        }
        for (int b = 1; b <= Bars; b++)
            if (boundaries[b] <= boundaries[b - 1])
                boundaries[b] = boundaries[b - 1] + 1;
        return boundaries;
    }

    private WasapiLoopbackCapture? _capture;
    private readonly NAudio.Dsp.Complex[] _fft = new NAudio.Dsp.Complex[FftSize];
    private int _fftWritePos;
    private readonly float[] _bars = new float[Bars];
    private readonly object _lock = new object();
    private volatile bool _capturing;

    // --- ""Firefly on a leash"" flight state -- Draw()/render thread only.
    // _flightPos/_flightVel is a real, ALWAYS-RUNNING position+velocity
    // simulation per blob -- an OFFSET from that blob's frozen anchor
    // (see _anchorPos below). The thread only controls how much of that
    // already-happening flight actually shows up in the drawn position:
    // 0 means draws exactly on the live ring (taut), 1 means draws at
    // anchor + real flight position (free).
    //
    // Anchor freezing: when the explosion begins, each blob's CURRENT
    // drawn position (live ring position + visible flight offset) is
    // captured into _anchorPos, and its flight offset is reset to zero.
    // The kick then pushes from that frozen position, not from the ring.
    // This is what makes the explosion start from wherever the blob
    // actually was at the moment audio cut, instead of first collapsing
    // inward to the ring and then exploding outward from there. While
    // attached (explode ~ 0), the drawn position uses the LIVE ring, so
    // the normal audio-reactive motion is unaffected; the frozen anchor
    // only takes over as explode rises toward 1.
    //
    // Steering: each blob has a base heading spread evenly around the
    // circle (i * TWO_PI / Bars), perturbed by Perlin noise. This is the
    // key fix for the ""everything drifts left"" bug -- the old code used
    // Noise(...) * TWO_PI directly, which only ever produced headings in
    // [0, TWO_PI] with Perlin's natural bias toward mid values (~PI, which
    // in screen coords is ""left""), so every blob shared the same drift.
    private readonly PVector[] _flightPos = new PVector[Bars];
    private readonly PVector[] _flightVel = new PVector[Bars];
    private readonly PVector[] _anchorPos = new PVector[Bars];      // head anchor, frozen on explode
    private readonly PVector[] _anchorBase = new PVector[Bars];     // tail-base anchor, frozen on explode
    private readonly float[] _blobExplode = new float[Bars];
    private readonly float[] _blobDelay = new float[Bars];
    private long _lastFrameMs = -1;
    private bool _isExploded;
    private long _quietSinceMs = -1;
    private long _transitionStartMs;
    private float _transitionStartAmount;
    private float _currentDuration = 850f;
    private float _lastOverallLevel; // smoothed, used to scale explode speed + kick

    public override void Setup()
    {
        Size(700, 700);

        for (int i = 0; i < Bars; i++)
        {
            _flightPos[i] = new PVector(0f, 0f);
            _flightVel[i] = new PVector(0f, 0f);
            _anchorPos[i] = new PVector(0f, 0f);
            _anchorBase[i] = new PVector(0f, 0f);
            _blobExplode[i] = 0f;
            _blobDelay[i] = 0f;
        }

        StartCapture();
    }

    public override void Draw()
    {
        Background(6, 8, 16);

        float[] snapshot;
        lock (_lock)
            snapshot = (float[])_bars.Clone();

        float overall = 0f;
        for (int i = 0; i < Bars; i++)
            overall += snapshot[i];
        overall /= Bars;

        // Smoothed energy -- used to scale explode duration + release kick.
        // Fast attack, slow release, so a sudden drop still reads the
        // energy that was present just before it.
        _lastOverallLevel = overall > _lastOverallLevel
            ? overall
            : Lerp(_lastOverallLevel, overall, 0.08f);

        UpdateExplodeState(overall, snapshot);

        // Real elapsed time since the last frame, clamped so a debugger
        // pause can't fling everything across the canvas in one jump.
        float dt = _lastFrameMs < 0 ? 0f : Constrain((Millis() - _lastFrameMs) / 1000f, 0f, 0.05f);
        _lastFrameMs = Millis();
        float noiseT = Millis() * WanderSpeed;

        PushMatrix();
        Translate(Width / 2f, Height / 2f);

        for (int i = 0; i < Bars; i++)
        {
            float angle = Map(i, 0, Bars, 0, TWO_PI);
            float liveLen = Map(Constrain(snapshot[i], 0f, 1f), 0, 1, MinLen, MaxLen);
            float hue = Map(i, 0, Bars, 170, 285);

            // Live ring geometry (moves with the audio).
            float liveRingX = Cos(angle) * (BaseRadius + liveLen);
            float liveRingY = Sin(angle) * (BaseRadius + liveLen);
            float liveBaseX = Cos(angle) * BaseRadius;
            float liveBaseY = Sin(angle) * BaseRadius;

            float explode = _blobExplode[i];

            // Blend between live ring and frozen anchor based on explode.
            // At 0 the blob draws on the live ring (normal audio-reactive
            // motion, unaffected). At 1 it draws from the frozen anchor
            // (wherever it was when the explosion began). In between it
            // eases between the two, so there's no snap at either end.
            float attachedHeadX = Lerp(liveRingX, _anchorPos[i].X, explode);
            float attachedHeadY = Lerp(liveRingY, _anchorPos[i].Y, explode);

            // Steering heading: base direction spread evenly around the
            // circle per blob, plus Perlin jitter. The even base spread is
            // what guarantees no global directional bias -- without it,
            // Perlin's natural mid-range bias makes every blob drift the
            // same way (to the ""left"" in screen coords).
            float baseHeading = i * (TWO_PI / Bars);
            float jitter = (Noise(i * 7.3f, noiseT) - 0.5f) * 0.9f;
            float heading = baseHeading + jitter;

            // Home spring is scaled by (1 - explode): fully free while
            // exploded (so the kick actually carries), fully springy once
            // reattached (so it settles cleanly onto its anchor).
            float homeK = FlightHomeK * (1f - explode);

            // Wander fades while exploded -- the initial kick should
            // dominate the trajectory, and the noise should only curve it
            // gently.
            float wanderScale = 1f - explode * 0.6f;

            _flightVel[i].X += (Cos(heading) * WanderAccel * wanderScale - _flightPos[i].X * homeK) * dt;
            _flightVel[i].Y += (Sin(heading) * WanderAccel * wanderScale - _flightPos[i].Y * homeK) * dt;

            float damping = MathF.Exp(-FlightDrag * dt);
            _flightVel[i].X *= damping;
            _flightVel[i].Y *= damping;
            _flightVel[i].Limit(MaxFlightSpeed);

            // Extra velocity drain + position pull while reattaching, so
            // the blob ""lands"" on its home instead of pinging against it.
            float reattach = 1f - explode;
            if (reattach > 0f)
            {
                float vDrain = MathF.Exp(-6f * reattach * dt);
                float pPull  = MathF.Exp(-3f * reattach * dt);
                _flightVel[i].X *= vDrain;
                _flightVel[i].Y *= vDrain;
                _flightPos[i].X *= pPull;
                _flightPos[i].Y *= pPull;
            }

            _flightPos[i].X += _flightVel[i].X * dt;
            _flightPos[i].Y += _flightVel[i].Y * dt;

            float headX = attachedHeadX + _flightPos[i].X * explode;
            float headY = attachedHeadY + _flightPos[i].Y * explode;

            // Tail base blends from live ring base to frozen base anchor
            // the same way the head does, then retracts toward the head as
            // the thread lets go. The tail factor eases all the way to 1.0
            // so the tail fully collapses onto the head in the exploded
            // state (reads as a spark, not a stretched droplet with a stump).
            float tailBaseX = Lerp(liveBaseX, _anchorBase[i].X, explode);
            float tailBaseY = Lerp(liveBaseY, _anchorBase[i].Y, explode);
            float tailT = explode;
            tailT = tailT * tailT * (3f - 2f * tailT); // smoothstep
            tailBaseX = Lerp(tailBaseX, headX, tailT);
            tailBaseY = Lerp(tailBaseY, headY, tailT);

            float headR = Map(Constrain(snapshot[i], 0f, 1f), 0, 1, 5f, 15f);
            DrawBlob(tailBaseX, tailBaseY, headX, headY, headR, hue, explode);
        }

        PopMatrix();

        Fill(255);
        NoStroke();
        TextSize(13);
        Text(_capturing ? ""Reproduciendo audio del sistema -- proba poner musica"" : ""Captura detenida"", 16, Height - 40);
        Text(""S: iniciar/detener captura"", 16, Height - 20);
    }

    // A soft glowing head (several stacked, increasingly transparent
    // circles) at the tip of a tapered tail (one triangle from the ring
    // anchor widening to the head). As the blob explodes, the tail
    // triangle is skipped entirely once it's fully collapsed.
    private void DrawBlob(float tailBaseX, float tailBaseY, float headX, float headY, float headR, float hue, float explode)
    {
        // --- Cuerda con glow: varias líneas apiladas, de más gruesa y
        // transparente a más fina y opaca, igual que el bloom del head.
        // A medida que explode sube, la cuerda se acorta hacia el head
        // y se desvanece, hasta desaparecer por completo.
        if (explode < 0.999f)
        {
            float tailAlpha = 1f - explode;
            float tailX = Lerp(tailBaseX, headX, explode);
            float tailY = Lerp(tailBaseY, headY, explode);

            // Grosor base de la cuerda: proporcional al radio del head, pero
            // mucho más fino -- una cuerda, no un cono.
            float baseWidth = headR * 0.02f;

            // Capas de glow: de más ancha y sutil a más fina y brillante.
            // 5 capas dan un bloom suave sin costar demasiado.
            float[] widthScale = { 4.5f, 3.2f, 2.2f, 1.4f, 1f };
            float[] alphaScale = { 12f, 22f, 40f, 80f, 180f };

            for (int layer = 0; layer < widthScale.Length; layer++)
            {
                float alpha = alphaScale[layer] * tailAlpha;
                if (alpha < 1f) continue;

                float w = baseWidth * widthScale[layer];
                StrokeHSB(hue, layer == widthScale.Length - 1 ? 70 : 85, 100, (byte)alpha);
                StrokeWeight(w);
                Line(tailX, tailY, headX, headY);
            }

            NoStroke();
        }

        // Slight glow boost while exploded -- a free firefly reads a bit
        // brighter/hotter than one tethered to the ring.
        float glowBoost = 1f + explode * 0.35f;

        float[] layerScale = { 3.2f, 2.1f, 1.4f, 1f };
        float[] layerAlpha = { 18f, 35f, 70f, 255f };
        for (int layer = 0; layer < layerScale.Length; layer++)
        {
            float alpha = layerAlpha[layer] * glowBoost;
            if (alpha > 255f) alpha = 255f;
            FillHSB(hue, layer == layerScale.Length - 1 ? 70 : 85, 100, (byte)alpha);
            Circle(headX, headY, headR * 2 * layerScale[layer]);
        }
    }

    // Silence -> explode, sound -> reassemble. Each blob has its own
    // random delay, so the ring comes apart and reassembles as a ragged
    // cascade instead of one synchronized motion. The live snapshot is
    // passed through so BeginTransition can freeze each blob's current
    // drawn position as its explosion anchor.
    private void UpdateExplodeState(float overallLevel, float[] snapshot)
    {
        // Hysteresis: different thresholds for entering and exiting silence,
        // so mid-dynamic passages don't flicker between states.
        bool quiet = !_capturing || (_isExploded
            ? overallLevel < SilenceExit
            : overallLevel < SilenceEnter);

        if (quiet)
        {
            if (_quietSinceMs < 0)
                _quietSinceMs = Millis();
            if (!_isExploded && Millis() - _quietSinceMs > SilenceHoldMs)
                BeginTransition(exploding: true, snapshot: snapshot);
        }
        else
        {
            _quietSinceMs = -1;
            if (_isExploded)
                BeginTransition(exploding: false, snapshot: snapshot);
        }

        float elapsed = Millis() - _transitionStartMs;
        float progress = Constrain(elapsed / _currentDuration, 0f, 1f);

        // Per-blob stagger: each blob has its own delay in 0..MaxDelay and
        // remaps progress over the remaining window.
        for (int i = 0; i < Bars; i++)
        {
            float d = _blobDelay[i];
            float local = d >= 1f ? 1f : Constrain((progress - d) / (1f - d), 0f, 1f);
            float eased = local * local * (3f - 2f * local);
            float target = _isExploded ? 1f : 0f;
            _blobExplode[i] = Lerp(_transitionStartAmount, target, eased);
        }
    }

    private void BeginTransition(bool exploding, float[] snapshot)
    {
        _isExploded = exploding;
        _transitionStartMs = Millis();
        _transitionStartAmount = _blobExplode[0];

        // Random per-blob delay, reshuffled each transition.
        for (int i = 0; i < Bars; i++)
            _blobDelay[i] = Random(0f, 0.35f);

        // Duration depends on the smoothed energy just before the drop.
        float energy = Constrain(_lastOverallLevel, 0f, 1f);
        _currentDuration = exploding
            ? Lerp(ExplodeMsMax, ExplodeMsMin, energy)
            : Lerp(ReassembleMsMax, ReassembleMsMin, energy);

        if (exploding)
        {
            // Freeze each blob's CURRENT drawn position as its explosion
            // anchor, and reset its flight offset to zero. That way the
            // kick below pushes from wherever the blob actually was at
            // this instant -- not from the ring. Without this, the drop in
            // audio level would shrink the live ring inward first, and
            // blobs would visibly ""return home"" before exploding.
            for (int i = 0; i < Bars; i++)
            {
                float angle = Map(i, 0, Bars, 0, TWO_PI);
                float len = Map(Constrain(snapshot[i], 0f, 1f), 0, 1, MinLen, MaxLen);

                float liveRingX = Cos(angle) * (BaseRadius + len);
                float liveRingY = Sin(angle) * (BaseRadius + len);
                float liveBaseX = Cos(angle) * BaseRadius;
                float liveBaseY = Sin(angle) * BaseRadius;

                // Current drawn head = live ring + visible flight offset.
                // Current drawn tail base = live base + visible flight offset
                // (approximated the same way the tail is drawn -- it uses
                // head-relative collapse, so anchoring the base to live base
                // plus the same visible flight offset keeps the tail shape
                // consistent through the freeze).
                _anchorPos[i].X = liveRingX + _flightPos[i].X * _blobExplode[i];
                _anchorPos[i].Y = liveRingY + _flightPos[i].Y * _blobExplode[i];
                _anchorBase[i].X = liveBaseX + _flightPos[i].X * _blobExplode[i];
                _anchorBase[i].Y = liveBaseY + _flightPos[i].Y * _blobExplode[i];

                // Flight is now measured relative to the frozen anchor.
                _flightPos[i].X = 0f;
                _flightPos[i].Y = 0f;
            }

            // Release kick: radial (outward along the ring angle) plus a
            // per-blob random tangential component. The tangential part
            // is what breaks radial symmetry -- without it every blob
            // would just slide straight out from its anchor, and the ring
            // would expand like a perfect circle instead of bursting
            // raggedly. Angle jitter is Perlin-driven (per-blob, per-moment)
            // rather than uniform-random, so even within a single burst the
            // angular spread has organic clumping instead of even noise.
            for (int i = 0; i < Bars; i++)
            {
                float ringAngle = Map(i, 0, Bars, 0, TWO_PI);
                float angleJitter = (Noise(i * 3.1f, Millis() * 0.001f) - 0.5f) * 1.2f;
                float angle = ringAngle + angleJitter;

                float radial = Random(ReleaseKickMin, ReleaseKickMax) * (0.6f + energy * 0.8f);
                float tangent = Random(-1f, 1f) * radial * 0.7f;

                float cosA = Cos(angle);
                float sinA = Sin(angle);
                _flightVel[i].X += cosA * radial - sinA * tangent;
                _flightVel[i].Y += sinA * radial + cosA * tangent;
            }
        }
    }

    public override void KeyPressed()
    {
        if (Key != 's')
            return;

        if (_capturing)
            StopCapture();
        else
            StartCapture();
    }

    private void StartCapture()
    {
        var capture = new WasapiLoopbackCapture();
        _fftWritePos = 0;
        Array.Clear(_bars, 0, _bars.Length);

        capture.DataAvailable += (s, e) =>
        {
            int bytesPerSample = capture.WaveFormat.BitsPerSample / 8;
            int channels = capture.WaveFormat.Channels;
            int frameSize = bytesPerSample * channels;
            int frames = e.BytesRecorded / frameSize;

            for (int i = 0; i < frames; i++)
            {
                float sum = 0f;
                for (int ch = 0; ch < channels; ch++)
                    sum += BitConverter.ToSingle(e.Buffer, i * frameSize + ch * bytesPerSample);
                float mono = sum / channels;

                _fft[_fftWritePos].X = (float)(mono * NAudio.Dsp.FastFourierTransform.HammingWindow(_fftWritePos, FftSize));
                _fft[_fftWritePos].Y = 0f;
                _fftWritePos++;

                if (_fftWritePos >= FftSize)
                {
                    _fftWritePos = 0;
                    NAudio.Dsp.FastFourierTransform.FFT(true, FftLog2, _fft);
                    UpdateBars();
                }
            }
        };
        capture.RecordingStopped += (s, e) => _capturing = false;

        _capture = capture;
        capture.StartRecording();
        _capturing = true;
    }

    private void StopCapture()
    {
        _capture?.StopRecording();
        _capture?.Dispose();
        _capture = null;
        _capturing = false;
    }

    private void UpdateBars()
    {
        lock (_lock)
        {
            for (int b = 0; b < Bars; b++)
            {
                int start = BinBoundaries[b];
                int end = BinBoundaries[b + 1]; // exclusive
                float sum = 0f;
                for (int k = start; k < end; k++)
                    sum += Mag(_fft[k].X, _fft[k].Y);

                float barGain = Map(b, 0, Bars, 0.75f, 2.1f);
                float value = Sqrt(sum / (end - start)) * Gain * barGain;
                _bars[b] = Max(value, _bars[b] * Decay);
            }
        }
    }
}


";

        private const string ControllerInstallation =
@"// nuget: NAudio, 2.2.1
// nuget: Rug.Osc, 1.2.5

using System;
using System.Collections.Generic;
using NAudio.Midi;
using Rug.Osc;

// Installation-style visual reacting to two different ""controller"" inputs
// at once: MIDI notes/CC from a connected device (NAudio.Midi -- a thin
// wrapper over the OS's own winmm.dll, so no extra native asset to manage),
// and OSC messages over UDP (Rug.Osc -- e.g. from TouchOSC, Max/MSP, or any
// OSC sender on the same machine/network). Both just feed the same ripple
// field below, so it doesn't matter which one -- or both -- you actually
// have available.
//
// A grid of cells is lit by expanding ""ripples"": a MIDI Note On spawns one
// at an X position mapped from the note number (so playing a scale sends
// ripples marching left to right), colored by note and sized by velocity.
// A MIDI CC controls how fast ripples expand. Any OSC message sent to
// /dana/pulse <float 0..1> spawns one from the center with that float as
// its strength.
//
// Same caveat as the NAudio sample: the background MIDI listener and the
// OSC UDP socket started here aren't torn down on a second Run (nothing in
// the engine calls a cleanup hook on the old Sketch instance yet) --
// harmless, just an orphaned listener until the IDE closes.
public class MySketch : Sketch
{
    private const int Cols = 36;
    private const int Rows = 24;
    private const float CellSize = 20f;
    private const float RingThickness = 40f;
    private const int MaxRipples = 24;

    private struct Ripple
    {
        public float OriginCol, OriginRow;
        public long StartMillis;
        public float Hue;
        public float Strength;
    }

    private readonly List<Ripple> _ripples = new List<Ripple>();
    private readonly object _ripplesLock = new object();
    private volatile float _rippleSpeed = 140f; // px/sec, CC-controlled

    private MidiIn? _midiDevice;
    private string? _midiDeviceName;
    private OscReceiver? _oscReceiver;

    public override void Setup()
    {
        Size((int)(Cols * CellSize), (int)(Rows * CellSize));
        StartMidi();
        StartOsc();
    }

    public override void Draw()
    {
        Background(8, 9, 14);

        List<Ripple> snapshot;
        lock (_ripplesLock)
            snapshot = new List<Ripple>(_ripples);

        long now = Millis();

        for (int cy = 0; cy < Rows; cy++)
        {
            for (int cx = 0; cx < Cols; cx++)
            {
                float brightness = 6f; // dim base -- reads as ""off but present"", like an LED wall
                float hue = 0f;

                foreach (var r in snapshot)
                {
                    float dx = (cx - r.OriginCol) * CellSize;
                    float dy = (cy - r.OriginRow) * CellSize;
                    float dist = Mag(dx, dy);
                    float radius = (now - r.StartMillis) / 1000f * _rippleSpeed;
                    float ringDist = Abs(dist - radius);

                    if (ringDist < RingThickness)
                    {
                        float b = (1f - ringDist / RingThickness) * r.Strength * 90f;
                        if (b > brightness)
                        {
                            brightness = b;
                            hue = r.Hue;
                        }
                    }
                }

                NoStroke();
                FillHSB(hue, 75, Constrain(brightness, 0, 100));
                Rect(cx * CellSize, cy * CellSize, CellSize - 2, CellSize - 2);
            }
        }

        // Drop ripples once they've grown past the far corner -- keeps the
        // list from growing forever under a flood of MIDI/OSC events.
        float maxRadius = Mag(Cols * CellSize, Rows * CellSize);
        lock (_ripplesLock)
        {
            _ripples.RemoveAll(r => (now - r.StartMillis) / 1000f * _rippleSpeed - RingThickness > maxRadius);
            while (_ripples.Count > MaxRipples)
                _ripples.RemoveAt(0);
        }

        Fill(255);
        TextSize(12);
        string midiLabel = _midiDeviceName ?? ""no controller connected (that's fine)"";
        Text($""MIDI: {midiLabel}   OSC: UDP 9000, /dana/pulse <float>   click anywhere to try it"", 10, Height - 12);
    }

    // Neither MIDI hardware nor an OSC sender is required to see this sketch
    // do something -- click spawns a ripple right where you clicked, same as
    // a MIDI/OSC event would, just so there's always an immediate way to see
    // it work.
    public override void MouseClicked()
    {
        float col = Constrain(MouseX / CellSize, 0, Cols);
        float row = Constrain(MouseY / CellSize, 0, Rows);
        AddRipple(col, row, (Millis() / 4f) % 360f, 0.9f);
    }

    private void StartMidi()
    {
        if (MidiIn.NumberOfDevices == 0)
            return;

        _midiDeviceName = MidiIn.DeviceInfo(0).ProductName;
        var midiIn = new MidiIn(0);
        midiIn.MessageReceived += (s, e) =>
        {
            if (e.MidiEvent is NoteOnEvent noteOn && noteOn.Velocity > 0)
            {
                AddRipple(
                    Map(noteOn.NoteNumber, 0, 127, 0, Cols),
                    Rows / 2f,
                    Map(noteOn.NoteNumber, 0, 127, 0, 360),
                    Map(noteOn.Velocity, 0, 127, 0.3f, 1f));
            }
            else if (e.MidiEvent is ControlChangeEvent cc)
            {
                _rippleSpeed = Map(cc.ControllerValue, 0, 127, 40, 500);
            }
        };
        midiIn.Start();
        _midiDevice = midiIn; // keep it referenced -- otherwise nothing stops the GC from collecting it out from under its own event pump
    }

    private void StartOsc()
    {
        _oscReceiver = new OscReceiver(9000);
        Thread(nameof(ListenOsc));
    }

    // No parameters -- exactly what Thread() looks for via reflection.
    private void ListenOsc()
    {
        var receiver = _oscReceiver!;
        try
        {
            receiver.Connect();
            while (receiver.State == OscSocketState.Connected)
            {
                var packet = receiver.Receive();
                if (packet is OscMessage message && message.Address == ""/dana/pulse"" && message.Count > 0)
                {
                    float strength = Constrain(Convert.ToSingle(message[0]), 0f, 1f);
                    AddRipple(Cols / 2f, Rows / 2f, (Millis() / 5f) % 360f, Max(strength, 0.2f));
                }
            }
        }
        catch (Exception)
        {
            // Socket closed (or a malformed packet) -- nothing to recover,
            // just stop listening instead of crashing the sketch.
        }
    }

    private void AddRipple(float col, float row, float hue, float strength)
    {
        lock (_ripplesLock)
        {
            _ripples.Add(new Ripple { OriginCol = col, OriginRow = row, StartMillis = Millis(), Hue = hue, Strength = strength });
        }
    }
}
";

        private const string HotReloadFlapper =
@"using System.Collections.Generic;

// A small flappy-bird-style game, built specifically as a Hot Reload
// testbed: every constant that shapes how it FEELS lives at the top, and
// everything that makes a run worth continuing (score, best score, player
// position/velocity, the pipes already on screen) lives in instance fields.
// Play it, then while it's still running, tweak one of the constants below
// and press HOT RELOAD (the lightning-bolt button, not Run) -- the new
// physics apply on the very next frame without losing your run or your
// score, because those are exactly the instance fields SketchHotReload
// transplants. Press Run instead and you'll see the normal full restart:
// same new physics, but score/position back to zero.
//
// One deliberate design choice worth calling out: pipes are three PARALLEL
// List<float>/List<bool> fields, not one List<Pipe> of a small struct that
// would otherwise be the more natural shape. A struct/class declared inside
// the sketch itself gets a brand-new type identity every single recompile
// (it lives in that compile's own throwaway assembly, unlike Sketch/PVector/
// etc., which live in the one stable DanaProcessing.dll) -- so even a
// same-name, same-shape Pipe from an unrelated one-constant tweak reads as
// ""this field's type changed"" to SketchHotReload, and Hot Reload falls back
// to a full restart every single time, on every edit, no matter how small.
// Parallel List<float>/List<bool> fields (or List<PVector>, or anything else
// built entirely out of stable framework/DanaProcessing types) sidestep that
// completely.
public class MySketch : Sketch
{
    // ---- Tweak these while playing, then Hot Reload to feel the change ----
    private const float Gravity = 1400f;      // px/s^2 -- how hard the player falls
    private const float FlapStrength = 480f;  // px/s -- upward kick on flap
    private const float PipeSpeed = 220f;     // px/s -- how fast pipes scroll left
    private const float PipeGap = 170f;       // px -- vertical opening between top/bottom pipe
    private const float PipeSpacing = 260f;   // px -- horizontal distance between pipes
    private const float PlayerRadius = 16f;
    private const float PipeWidth = 60f;
    // -------------------------------------------------------------------

    private float _playerY;
    private float _playerVelocity;
    private readonly List<float> _pipeX = new List<float>();
    private readonly List<float> _pipeGapCenterY = new List<float>();
    private readonly List<bool> _pipeScored = new List<bool>();
    private int _score;
    private int _bestScore;
    private bool _gameOver;
    private float _lastFrameMillis;

    public override void Setup()
    {
        Size(480, 640);
        ResetGame();
    }

    public override void Draw()
    {
        float now = Millis();
        // Clamped so a paused debugger, a slow frame, or the brief gap a Hot
        // Reload itself takes never shows up as one giant physics jump.
        float dt = Constrain((now - _lastFrameMillis) / 1000f, 0f, 0.05f);
        _lastFrameMillis = now;

        Background(20, 24, 34);

        if (!_gameOver)
        {
            _playerVelocity += Gravity * dt;
            _playerY += _playerVelocity * dt;

            float playerX = Width * 0.3f;

            for (int i = 0; i < _pipeX.Count; i++)
            {
                _pipeX[i] -= PipeSpeed * dt;

                bool withinX = playerX + PlayerRadius > _pipeX[i] && playerX - PlayerRadius < _pipeX[i] + PipeWidth;
                bool withinGap = _playerY - PlayerRadius > _pipeGapCenterY[i] - PipeGap / 2f
                               && _playerY + PlayerRadius < _pipeGapCenterY[i] + PipeGap / 2f;
                if (withinX && !withinGap)
                    _gameOver = true;

                if (!_pipeScored[i] && _pipeX[i] + PipeWidth < playerX)
                {
                    _pipeScored[i] = true;
                    _score++;
                }
            }

            if (_pipeX.Count == 0 || _pipeX[_pipeX.Count - 1] < Width - PipeSpacing)
            {
                _pipeX.Add(Width);
                _pipeGapCenterY.Add(Random(PipeGap, Height - PipeGap));
                _pipeScored.Add(false);
            }

            // Drop pipes that scrolled off the left edge -- keep all three
            // lists in lockstep since they're really one array-of-structs
            // split into parallel columns.
            while (_pipeX.Count > 0 && _pipeX[0] < -PipeWidth)
            {
                _pipeX.RemoveAt(0);
                _pipeGapCenterY.RemoveAt(0);
                _pipeScored.RemoveAt(0);
            }

            if (_playerY - PlayerRadius < 0 || _playerY + PlayerRadius > Height)
                _gameOver = true;

            if (_gameOver)
                _bestScore = Max(_bestScore, _score);
        }

        NoStroke();
        Fill(90, 200, 140);
        for (int i = 0; i < _pipeX.Count; i++)
        {
            Rect(_pipeX[i], 0, PipeWidth, _pipeGapCenterY[i] - PipeGap / 2f);
            Rect(_pipeX[i], _pipeGapCenterY[i] + PipeGap / 2f, PipeWidth, Height - (_pipeGapCenterY[i] + PipeGap / 2f));
        }

        Fill(_gameOver ? new Color(220, 90, 90) : new Color(255, 200, 90));
        Circle(Width * 0.3f, _playerY, PlayerRadius * 2);

        Fill(255);
        TextSize(28);
        Text(_score.ToString(), Width / 2f - 8, 40);
        TextSize(13);
        Text($""Best: {_bestScore}"", 12, Height - 40);
        Text(""Click or SPACE to flap"", 12, Height - 20);

        if (_gameOver)
        {
            TextSize(20);
            Text(""Game over -- click to try again"", Width / 2f - 150, Height / 2f);
        }
    }

    public override void MouseClicked() => Flap();

    public override void KeyPressed()
    {
        if (Key == ' ')
            Flap();
    }

    private void Flap()
    {
        if (_gameOver)
        {
            ResetGame();
            return;
        }
        _playerVelocity = -FlapStrength;
    }

    private void ResetGame()
    {
        _playerY = Height / 2f;
        _playerVelocity = 0f;
        _pipeX.Clear();
        _pipeGapCenterY.Clear();
        _pipeScored.Clear();
        _score = 0;
        _gameOver = false;
    }
}
";

        private const string SimpleLinearRegression =
@"// nuget: Microsoft.ML, 4.0.2

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.ML;
using Microsoft.ML.Trainers;

// The simplest possible ML.NET example, built to make the LEARNING itself
// visible instead of just showing a finished result: a straight line fit
// to the points you click, redrawn a little closer to the data every
// single frame while an on-screen counter and error number climb/drop in
// real time.
//
// The trick that makes step-by-step progress visible without writing a
// custom training loop: OnlineGradientDescentTrainer's NumberOfIterations
// is literally ""how many passes over the data to run"" -- so refitting
// from scratch every frame with NumberOfIterations = the current frame's
// iteration count reproduces the exact same weight/bias trajectory real
// online gradient descent would take if it ran one step per frame live.
// It repeats earlier work every frame (not the fastest way to train), but
// for a handful of points and a couple hundred iterations that's still
// well under a millisecond, and it keeps this sketch's own code as simple
// as ""ask for N iterations, read out the line, draw it.""
public class MySketch : Sketch
{
    private const int MaxIterations = 150;
    private const float PlotLeft = 44, PlotRight = 20, PlotTop = 20, PlotBottom = 40;

    // Draw() runs on the render thread while MouseClicked()/KeyPressed()
    // run on the UI thread (see AvaloniaSketchCanvas -- OnKeyDown/
    // OnPointerPressed call straight into the sketch synchronously, from a
    // different thread than PaintSketch's RenderFrame()). _xs/_ys get
    // mutated from the UI thread (a click, or R clearing+reseeding) while
    // Draw() reads them on the render thread to train -- without a lock,
    // List<T>'s enumerator throws InvalidOperationException the instant a
    // Clear()/Add() lands mid-enumeration, which is exactly what
    // LoadFromEnumerable's internal parallel cursoring surfaced as a deep
    // ML.NET ""Splitter/consolidator worker"" exception. Every read AND
    // write of _xs/_ys goes through _dataLock so that can't happen; Draw()
    // takes one snapshot copy per frame and works from that everywhere
    // else in the method, rather than touching the live lists directly.
    private readonly object _dataLock = new object();
    private readonly List<float> _xs = new List<float>();
    private readonly List<float> _ys = new List<float>();
    private readonly MLContext _ml = new MLContext(seed: 1);

    private int _iteration;
    private float _weight;
    private float _bias;
    private float _mse;
    private bool _trained;

    public override void Setup()
    {
        Size(700, 480);
        SeedRandomPoints();
    }

    public override void Draw()
    {
        Background(18, 20, 26);

        Stroke(70);
        StrokeWeight(1);
        Line(PlotLeft, Height - PlotBottom, Width - PlotRight, Height - PlotBottom);
        Line(PlotLeft, PlotTop, PlotLeft, Height - PlotBottom);

        float[] xs, ys;
        lock (_dataLock)
        {
            xs = _xs.ToArray();
            ys = _ys.ToArray();
        }

        // One more gradient-descent step per frame, up to MaxIterations --
        // this line is the entire ""watch it learn"" mechanic.
        if (_iteration < MaxIterations && xs.Length >= 2)
        {
            _iteration++;
            Train(xs, ys, _iteration);
        }

        NoStroke();
        Fill(120, 200, 255);
        for (int i = 0; i < xs.Length; i++)
            Circle(ToScreenX(xs[i]), ToScreenY(ys[i]), 8);

        if (_trained)
        {
            Stroke(255, 160, 90);
            StrokeWeight(3);
            Line(ToScreenX(0f), ToScreenY(_bias), ToScreenX(1f), ToScreenY(_weight + _bias));
        }

        Fill(255);
        NoStroke();
        TextSize(14);
        string status = xs.Length < 2
            ? ""Click para agregar al menos 2 puntos...""
            : $""Iteración {_iteration}/{MaxIterations} -- y = {_weight:F2}x + {_bias:F2}"";
        Text(status, PlotLeft, 16);
        if (_trained)
            Text($""Error (MSE): {_mse:F4}"", PlotLeft, 36);

        TextSize(12);
        Text(""Click agrega un punto y reentrena desde cero -- R reinicia con puntos al azar."", PlotLeft, Height - 12);
    }

    public override void MouseClicked()
    {
        if (MouseY > Height - PlotBottom || MouseX < PlotLeft) return;
        lock (_dataLock)
        {
            _xs.Add(ToDataX(MouseX));
            _ys.Add(ToDataY(MouseY));
        }
        _iteration = 0; // watch it learn again from scratch, now with this point included
    }

    public override void KeyPressed()
    {
        if (Key == 'r' || Key == 'R')
            SeedRandomPoints();
    }

    private void SeedRandomPoints()
    {
        float trueSlope = Random(-1.2f, 1.2f);
        float trueIntercept = Random(0.2f, 0.8f);
        lock (_dataLock)
        {
            _xs.Clear();
            _ys.Clear();
            for (int i = 0; i < 12; i++)
            {
                float x = Random(0f, 1f);
                float y = Constrain(trueSlope * x + trueIntercept + Random(-0.1f, 0.1f), 0f, 1f);
                _xs.Add(x);
                _ys.Add(y);
            }
        }
        _iteration = 0;
        _trained = false;
    }

    private void Train(float[] xs, float[] ys, int iterations)
    {
        var rows = Enumerable.Range(0, xs.Length).Select(i => new PointSample { X = xs[i], Label = ys[i] });
        var data = _ml.Data.LoadFromEnumerable(rows);

        var pipeline = _ml.Transforms.Concatenate(""Features"", nameof(PointSample.X))
            .Append(_ml.Regression.Trainers.OnlineGradientDescent(new OnlineGradientDescentTrainer.Options
            {
                LabelColumnName = ""Label"",
                FeatureColumnName = ""Features"",
                NumberOfIterations = iterations,
                LearningRate = 0.5f,
                Shuffle = false,
            }));

        var model = pipeline.Fit(data);
        var linear = model.LastTransformer.Model;
        _weight = linear.Weights[0];
        _bias = linear.Bias;

        // Mean squared error against the same points -- a real number that
        // visibly trends toward zero as _iteration climbs, exactly like a
        // real training-loss curve.
        float sumSq = 0f;
        for (int i = 0; i < xs.Length; i++)
        {
            float pred = _weight * xs[i] + _bias;
            float err = pred - ys[i];
            sumSq += err * err;
        }
        _mse = sumSq / xs.Length;
        _trained = true;
    }

    private float ToScreenX(float x) => PlotLeft + x * (Width - PlotLeft - PlotRight);
    private float ToScreenY(float y) => (Height - PlotBottom) - y * (Height - PlotBottom - PlotTop);
    private float ToDataX(float screenX) => (screenX - PlotLeft) / (Width - PlotLeft - PlotRight);
    private float ToDataY(float screenY) => ((Height - PlotBottom) - screenY) / (Height - PlotBottom - PlotTop);

    private class PointSample
    {
        public float X;
        public float Label;
    }
}
";

        private const string MLColorField =
@"// nuget: Microsoft.ML, 4.0.2

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Trainers;

// Click to drop a colored ""seed"" point. Microsoft.ML trains a real
// regression model (Sdca, one per color channel) mapping (x, y) -> color
// from every seed you've placed so far, then that model predicts a color
// for a grid covering the whole canvas -- an organic, generative field
// that reshapes itself around wherever you've clicked. This is the
// ""any NuGet package is a sketch library"" pitch made literal: `// nuget:
// Microsoft.ML` and three lines of pipeline code, no separate ML project,
// no .csproj to touch.
//
// Deliberately trains a small model live instead of loading a pretrained
// one from disk/network: no external model file to host or download, so
// the sketch stays exactly as self-contained as every other sample once
// `// nuget:` has resolved.
//
// MaximumNumberOfIterations is set explicitly below -- SDCA's default is
// open-ended convergence-based, and measured directly against this exact
// tiny/sparse dataset shape (a handful of 2D points), that took anywhere
// from under 100ms to 40+ SECONDS with no visible pattern to when. Capping
// it brought every run back to single-digit milliseconds with no loss
// anyone would notice for a generative color field. Still retrains on a
// background Thread() with a visible ""Training..."" indicator regardless
// (rather than inline in Draw()) -- real ML training time isn't something
// a sketch should gamble on staying imperceptible.
public class MySketch : Sketch
{
    private const int GridCols = 40;
    private const int GridRows = 40;
    private const int MaxSeeds = 24;

    private readonly List<float> _seedX = new List<float>();
    private readonly List<float> _seedY = new List<float>();
    private readonly List<float> _seedR = new List<float>();
    private readonly List<float> _seedG = new List<float>();
    private readonly List<float> _seedB = new List<float>();

    private readonly Color[] _palette =
    {
        new Color(255, 90, 90),
        new Color(255, 200, 90),
        new Color(120, 220, 140),
        new Color(110, 180, 255),
        new Color(200, 130, 255),
    };

    private readonly MLContext _ml = new MLContext(seed: 1);
    private volatile bool _isTraining;
    private volatile bool _dirty;
    private Color[]? _gridColors; // GridCols * GridRows, swapped in atomically by RetrainAndPredict

    public override void Setup()
    {
        Size(600, 600);
    }

    public override void Draw()
    {
        Background(16, 18, 24);

        var grid = _gridColors; // local copy -- safe even if the background thread swaps the field mid-frame
        if (grid == null)
        {
            Fill(255);
            TextSize(16);
            Text(""Click anywhere to drop a color seed..."", 24, Height / 2f);
        }
        else
        {
            NoStroke();
            float cellW = Width / (float)GridCols;
            float cellH = Height / (float)GridRows;
            for (int gy = 0; gy < GridRows; gy++)
            {
                for (int gx = 0; gx < GridCols; gx++)
                {
                    Fill(grid[gy * GridCols + gx]);
                    Rect(gx * cellW, gy * cellH, cellW + 1, cellH + 1);
                }
            }
        }

        NoFill();
        Stroke(255);
        StrokeWeight(2);
        for (int i = 0; i < _seedX.Count; i++)
            Circle(_seedX[i] * Width, _seedY[i] * Height, 16);

        Fill(255);
        NoStroke();
        TextSize(13);
        Text(_isTraining
            ? ""Training...""
            : $""{_seedX.Count} seed(s) -- click to add more, Microsoft.ML retrains live"",
            14, Height - 16);
    }

    public override void MouseClicked()
    {
        if (_seedX.Count >= MaxSeeds)
        {
            _seedX.RemoveAt(0);
            _seedY.RemoveAt(0);
            _seedR.RemoveAt(0);
            _seedG.RemoveAt(0);
            _seedB.RemoveAt(0);
        }

        var c = _palette[_seedX.Count % _palette.Length];
        _seedX.Add(MouseX / Width);
        _seedY.Add(MouseY / Height);
        _seedR.Add(c.R / 255f);
        _seedG.Add(c.G / 255f);
        _seedB.Add(c.B / 255f);

        _dirty = true;
        if (!_isTraining)
            Thread(nameof(RetrainAndPredict));
    }

    // No parameters -- exactly what Thread() looks for via reflection.
    private void RetrainAndPredict()
    {
        _isTraining = true;
        try
        {
            do
            {
                _dirty = false;

                // Snapshot the seed lists -- MouseClicked() only ever appends
                // on the UI thread, so worst case this misses a point added a
                // moment ago, which the _dirty/do-while loop just picks up on
                // the next pass instead.
                int count = _seedX.Count;
                if (count == 0)
                    return;

                var xs = new float[count];
                var ys = new float[count];
                var rs = new float[count];
                var gs = new float[count];
                var bs = new float[count];
                for (int i = 0; i < count; i++)
                {
                    xs[i] = _seedX[i]; ys[i] = _seedY[i];
                    rs[i] = _seedR[i]; gs[i] = _seedG[i]; bs[i] = _seedB[i];
                }

                var engineR = TrainChannel(xs, ys, rs);
                var engineG = TrainChannel(xs, ys, gs);
                var engineB = TrainChannel(xs, ys, bs);

                var grid = new Color[GridCols * GridRows];
                for (int gy = 0; gy < GridRows; gy++)
                {
                    float ny = (gy + 0.5f) / GridRows;
                    for (int gx = 0; gx < GridCols; gx++)
                    {
                        float nx = (gx + 0.5f) / GridCols;
                        var input = new ColorSample { Features = new float[] { nx, ny } };
                        float r = Constrain(engineR.Predict(input).Score * 255f, 0, 255);
                        float g = Constrain(engineG.Predict(input).Score * 255f, 0, 255);
                        float b = Constrain(engineB.Predict(input).Score * 255f, 0, 255);
                        grid[gy * GridCols + gx] = new Color((byte)r, (byte)g, (byte)b);
                    }
                }

                _gridColors = grid; // single reference swap -- atomic, Draw() never sees a half-built grid
            } while (_dirty);
        }
        finally
        {
            _isTraining = false;
        }
    }

    private PredictionEngine<ColorSample, ColorPrediction> TrainChannel(float[] xs, float[] ys, float[] labels)
    {
        var rows = Enumerable.Range(0, xs.Length)
            .Select(i => new ColorSample { Features = new float[] { xs[i], ys[i] }, Label = labels[i] });
        var data = _ml.Data.LoadFromEnumerable(rows);
        var pipeline = _ml.Regression.Trainers.Sdca(new SdcaRegressionTrainer.Options
        {
            LabelColumnName = ""Label"",
            FeatureColumnName = ""Features"",
            MaximumNumberOfIterations = 20,
        });
        var model = pipeline.Fit(data);
        return _ml.Model.CreatePredictionEngine<ColorSample, ColorPrediction>(model);
    }

    private class ColorSample
    {
        [VectorType(2)]
        public float[] Features = new float[2];
        public float Label;
    }

    private class ColorPrediction
    {
        [ColumnName(""Score"")]
        public float Score;
    }
}
";

        private const string SelfPlayingFlapper =
@"// nuget: Microsoft.ML, 4.0.2

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Trainers;

// ML.NET has no reinforcement-learning trainer -- no Q-learning, no policy
// gradients, nothing that natively ""plays a game and learns from reward"".
// What it DOES have is ordinary binary classification, which is enough to
// build a real (if simple) self-play loop out of, using a classic technique
// called the cross-entropy method:
//   1. Simulate a POPULATION of attempts (headless, no rendering) using the
//      current CHAMPION policy plus a bit of per-episode noise (see
//      SimulateEpisode()'s own remark for why per-EPISODE, not per-frame).
//   2. Keep the ELITE (the best-performing attempts).
//   3. Train a classifier to imitate ""what the elite did"" -- state in,
//      flap-or-not out.
//   4. Evaluate that freshly-trained CANDIDATE cleanly (no noise). Only
//      promote it to champion if it's actually at least as good as the
//      current one -- see TrainForever()'s own remark on why blindly
//      trusting every fresh fit collapses the whole thing.
// Each round is one ""generation"".
//
// This runs on a background thread (TrainForever(), started once from
// Setup()) so it never blocks Draw() -- the bird you see playing always
// uses whichever champion has been proven best so far, picked up via a
// single reference swap (_championModel), the same safe pattern the
// ""Campo de color"" ML.NET sample already uses for its own background
// training. Training is capped at MaxGenerations: a self-play loop with no
// stopping point would keep a background thread spinning forever even
// after you switch to a different sample (there's no engine hook yet for
// ""this sketch was replaced, stop your threads"") -- capping generations
// is what keeps this sample's CPU cost bounded and predictable instead of
// silently running forever in the background. It also stops EARLY, well
// before MaxGenerations, once ConvergenceStreak generations in a row bring
// no real improvement -- see TrainForever()'s own remark for why.
public class MySketch : Sketch
{
    private const float Gravity = 1400f;
    private const float FlapStrength = 480f;
    private const float PipeSpeed = 220f;
    private const float PipeGap = 190f;
    private const float PipeSpacing = 260f;
    private const float PlayerRadius = 16f;
    private const float PipeWidth = 60f;
    private const float CanvasWidth = 480f;
    private const float CanvasHeight = 640f;
    private const float PlayerX = CanvasWidth * 0.3f;
    private const float SimDt = 1f / 60f; // fixed timestep -- headless training and the displayed bird must experience IDENTICAL physics, so this isn't measured via Millis() like a normal game loop would
    private const float Lookahead = 0.35f; // seconds -- see ComputeFeatures()'s F5 remark

    private const int MaxSimFrames = 1800;   // a 30-second-equivalent cap per simulated attempt, so a great run can't loop forever
    private const int PopulationSize = 60;
    private const int EliteCount = 15;
    private const int MaxGenerations = 35;
    private const int EvalRuns = 8;          // clean (no-noise) episodes used to score a candidate before trusting it
    private const int PauseBetweenGenerationsMs = 1200; // see TrainForever()'s own remark -- pacing, not busywork
    private const int ConvergenceStreak = 10; // see TrainForever()'s own remark -- stop once it's genuinely done improving, not just capped

    // ---- displayed game state -- Draw()/render thread only ----
    private float _playerY;
    private float _playerVelocity;
    private readonly List<float> _pipeX = new List<float>();
    private readonly List<float> _pipeGapCenterY = new List<float>();
    private readonly List<bool> _pipeScored = new List<bool>();
    private int _displayScore;
    private int _displayBest;
    private readonly MLContext _displayMl = new MLContext(seed: 2); // separate MLContext than training's -- see remark on _ml below
    private PredictionEngine<BirdState, FlapPrediction> _displayEngine;
    private ITransformer _displayEngineBuiltFor;

    // ---- training state -- TrainForever()/background thread only, except
    // _championModel and the volatile fields below, which Draw() reads ----
    private readonly MLContext _ml = new MLContext(seed: 1);
    // TWO separate MLContext instances, not one shared between threads --
    // deliberately sidesteps ever needing to know whether concurrently
    // calling one MLContext's LoadFromEnumerable()/Fit() (background
    // thread) and CreatePredictionEngine() (render thread) is actually
    // safe. An ITransformer (a fitted model) is happily usable from any
    // MLContext, so this costs nothing.
    private readonly Random _trainRand = new Random();
    private readonly Random _evalRand = new Random();
    // The background thread's OWN random generators -- never Sketch's own
    // Random()/_rand, which Draw() uses on a different thread. Sharing one
    // System.Random across threads is exactly the bug the ""ML.NET paso a
    // paso"" sample hit with its List<float> fields (see its own remarks) --
    // applying that lesson here before it becomes a repeat bug.
    private ITransformer _championModel; // plain reference swap when a generation promotes a new champion -- both threads only ever read or replace the whole reference, never mutate through it

    private volatile int _generation;
    private double _championScore = -1; // not volatile -- double can't be (a C# language restriction); Draw() only ever reads this for display, a one-frame-stale value is harmless
    private volatile bool _trainingDone;
    private int _noImprovementStreak; // TrainForever()'s own thread only -- never read from Draw()

    public override void Setup()
    {
        Size((int)CanvasWidth, (int)CanvasHeight);
        _playerY = CanvasHeight / 2f;
        Thread(nameof(TrainForever));
    }

    public override void Draw()
    {
        Background(20, 24, 34);

        if (_championModel != null && !ReferenceEquals(_championModel, _displayEngineBuiltFor))
        {
            _displayEngine?.Dispose();
            _displayEngine = _displayMl.Model.CreatePredictionEngine<BirdState, FlapPrediction>(_championModel);
            _displayEngineBuiltFor = _championModel;
        }

        StepDisplayBird();

        NoStroke();
        Fill(90, 200, 140);
        for (int i = 0; i < _pipeX.Count; i++)
        {
            Rect(_pipeX[i], 0, PipeWidth, _pipeGapCenterY[i] - PipeGap / 2f);
            Rect(_pipeX[i], _pipeGapCenterY[i] + PipeGap / 2f, PipeWidth, CanvasHeight - (_pipeGapCenterY[i] + PipeGap / 2f));
        }

        Fill(255, 200, 90);
        Circle(PlayerX, _playerY, PlayerRadius * 2);

        Fill(255);
        TextSize(24);
        Text(_displayScore.ToString(), Width / 2f - 8, 36);
        TextSize(13);
        string genLabel = _trainingDone
            ? $""Entrenamiento terminado ({_generation} generaciones)""
            : $""Generación {_generation}/{MaxGenerations} -- entrenando en 2do plano..."";
        Text(genLabel, 12, Height - 60);
        Text($""Mejor logrado en el entrenamiento: {_championScore:F1} tubos (promedio limpio)"", 12, Height - 40);
        Text($""Esta partida: {_displayScore} (mejor de esta partida: {_displayBest})"", 12, Height - 20);
    }

    private void StepDisplayBird()
    {
        var state = ComputeFeatures(_pipeX, _pipeGapCenterY, _playerY, _playerVelocity);

        // Deliberately dumb (random, ignores the pipe entirely) while
        // waiting for the very first generation to finish -- purely
        // cosmetic, so the very first thing you SEE is obviously
        // unskilled, setting up a real before/after. The background
        // trainer's OWN bootstrap (SimulateEpisode()'s engine==null
        // branch, below) stays the smarter F4-based heuristic -- that one
        // actually needs to correlate with the right answer, or the
        // classifier has nothing true to imitate and never learns
        // anything at all (confirmed the hard way: a from-scratch random
        // bootstrap here never got past a 0.0 average, generation after
        // generation -- there has to be SOME real signal to refine).
        bool flap = _displayEngine != null
            ? _displayEngine.Predict(state).PredictedLabel
            : Random(0f, 1f) < 0.1f;

        bool dead = StepPhysics(_pipeX, _pipeGapCenterY, _pipeScored, ref _playerY, ref _playerVelocity, flap,
            (min, max) => Random(min, max), ref _displayScore);

        if (dead)
        {
            _displayBest = Max(_displayBest, _displayScore);
            _playerY = CanvasHeight / 2f;
            _playerVelocity = 0f;
            _pipeX.Clear();
            _pipeGapCenterY.Clear();
            _pipeScored.Clear();
            _displayScore = 0;
        }
    }

    // No parameters -- exactly what Thread() looks for via reflection.
    private void TrainForever()
    {
        for (int generation = 0; generation < MaxGenerations; generation++)
        {
            var championSnapshot = _championModel;
            using var engine = championSnapshot != null ? _ml.Model.CreatePredictionEngine<BirdState, FlapPrediction>(championSnapshot) : null;

            // Exploration starts noticeable and decays -- see
            // SimulateEpisode()'s own remark for why this is a single
            // per-EPISODE bias, not independent per-frame noise.
            float exploration = Max(0.02f, 0.15f - generation * 0.005f);

            var episodes = new List<(int pipes, int frames, List<(BirdState state, bool action)> traj)>();
            for (int p = 0; p < PopulationSize; p++)
                episodes.Add(SimulateEpisode(engine, exploration, _trainRand));

            // Fitness = pipes cleared first, frames survived as the
            // fine-grained tiebreaker -- pipes-cleared alone is almost
            // always zero for a weak policy, which gives the elite
            // selection nothing to differentiate on; frames survived gives
            // a real, graded signal even before the first pipe ever clears.
            episodes.Sort((a, b) => (b.pipes * 1_000_000 + b.frames).CompareTo(a.pipes * 1_000_000 + a.frames));
            var elite = episodes.Take(EliteCount);

            // Only frames where a pipe is meaningfully close (F3 < 0.6,
            // i.e. within 60% of a canvas-width ahead) -- most of a
            // trajectory is open-space ""nothing to decide"" filler that
            // dilutes the actually decision-critical navigation frames.
            var allRows = elite.SelectMany(e => e.traj.Where(t => t.state.F3 < 0.6f).Select(t => new BirdState
            {
                F1 = t.state.F1,
                F2 = t.state.F2,
                F3 = t.state.F3,
                F4 = t.state.F4,
                F5 = t.state.F5,
                Label = t.action,
            })).ToList();

            // Flap is a rare action (a handful of frames out of hundreds) --
            // an unweighted classifier trivially ""wins"" by always
            // predicting false. Balance by oversampling the minority
            // (flap=true) class up to roughly match the majority.
            var trueRows = allRows.Where(r => r.Label).ToList();
            var falseRows = allRows.Where(r => !r.Label).ToList();
            var rows = new List<BirdState>(falseRows);
            for (int i = 0; trueRows.Count > 0 && rows.Count < falseRows.Count * 2; i++)
                rows.Add(trueRows[i % trueRows.Count]);

            var data = _ml.Data.LoadFromEnumerable(rows);
            var pipeline = _ml.Transforms.Concatenate(""Features"", nameof(BirdState.F1), nameof(BirdState.F2), nameof(BirdState.F3), nameof(BirdState.F4), nameof(BirdState.F5))
                .Append(_ml.BinaryClassification.Trainers.SdcaLogisticRegression(new SdcaLogisticRegressionBinaryTrainer.Options
                {
                    LabelColumnName = ""Label"",
                    FeatureColumnName = ""Features"",
                    MaximumNumberOfIterations = 50,
                }));

            var candidateModel = pipeline.Fit(data);

            // Elitism: only ADOPT the freshly-fit model if it genuinely
            // evaluates at least as well as the current champion, cleanly
            // (zero noise). Pure ""always take the newest fit"" collapsed
            // hard after just a few generations in testing -- a single bad
            // Fit() (SDCA's own optimizer has real run-to-run variance) got
            // entrenched forever, since every later generation then trained
            // on THAT bad model's own degraded behavior with nothing to
            // correct it. Keeping the best-known model as the thing future
            // generations explore AROUND (instead of blindly trusting
            // whatever was just fit) is standard practice in evolutionary
            // methods for exactly this reason.
            using var candidateEngine = _ml.Model.CreatePredictionEngine<BirdState, FlapPrediction>(candidateModel);
            double candidateScore = 0;
            for (int e = 0; e < EvalRuns; e++)
                candidateScore += SimulateEpisode(candidateEngine, 0f, _evalRand).pipes;
            candidateScore /= EvalRuns;

            if (candidateScore > _championScore)
            {
                _championModel = candidateModel;
                _championScore = candidateScore;
                _noImprovementStreak = 0;
            }
            else
            {
                if (candidateScore == _championScore)
                    _championModel = candidateModel; // still adopt a genuine tie (keeps exploring around an equally-good fit), just don't count it as progress below
                _noImprovementStreak++;
            }

            _generation = generation + 1;

            // Stop once it's genuinely converged instead of always grinding
            // through all MaxGenerations. Measured for real (not guessed):
            // this sample reliably does all its real improving in the first
            // handful of generations, then spends the remaining ~30 running
            // the exact same 60-episode simulation + SDCA Fit() over and
            // over for zero visible benefit -- which is also ~30 more
            // generations' worth of allocation and GC pressure on top of the
            // ones that actually mattered, and was a real contributor to
            // this sample making the rest of the IDE (scrolling, mainly)
            // feel sluggish while it trained in the background. Bailing out
            // after a real, sustained non-improvement streak keeps the
            // early, meaningful climb fully intact while cutting the
            // trailing dead weight.
            if (_noImprovementStreak >= ConvergenceStreak)
                break;

            // A deliberate pause, not busywork left in by accident. Two
            // real problems this fixes at once, confirmed by actually
            // profiling this sample earlier (not guessed at): (1) the
            // 60-episode simulation above runs single-threaded and pegs
            // one CPU core almost continuously without it, and (2) SDCA's
            // own Fit() call periodically bursts across every core it can
            // get -- together, a generation with no breathing room between
            // it and the next made the whole IDE feel sluggish while this
            // sample was open, not just this sketch's own drawing. Pausing
            // here gives the render/UI threads real gaps to catch up in,
            // and as a bonus makes each generation's result something you
            // can actually watch land, one at a time, instead of the
            // champion jumping through several generations' worth of
            // improvement before you'd even had a chance to see the
            // previous one play.
            System.Threading.Thread.Sleep(PauseBetweenGenerationsMs);
        }
        _trainingDone = true;
    }

    // Headless -- no Sketch drawing calls, no shared Sketch.Random(). Safe
    // to call from the background thread.
    private (int pipes, int frames, List<(BirdState state, bool action)> traj) SimulateEpisode(PredictionEngine<BirdState, FlapPrediction> engine, float explorationScale, Random rand)
    {
        // Episode-level parameter noise instead of per-frame action flips.
        // The first version of this sample flipped the flap decision
        // independently each frame with small probability -- even at just
        // 1%, that's roughly a 60% chance of at least one flip within a
        // typical ~90-frame stretch (1-0.99^90), and flappy bird is fragile
        // enough that a single mistimed flap near a gap is often fatal.
        // Every population member ended up corrupted before reaching
        // anything worth learning from, and training never improved past
        // pure luck. A single consistent per-EPISODE bias explores ""what
        // if the policy leaned a bit more eager/cautious"" instead, without
        // randomly sabotaging individual frames -- this one change is what
        // made training actually work.
        float biasF4 = explorationScale > 0 ? (float)((rand.NextDouble() * 2 - 1) * explorationScale) : 0f;

        float playerY = CanvasHeight / 2f;
        float playerVelocity = 0f;
        var pipeX = new List<float>();
        var pipeGapCenterY = new List<float>();
        var pipeScored = new List<bool>();
        int score = 0;
        var traj = new List<(BirdState, bool)>();
        int frame = 0;

        // Reused for every frame's Predict() call instead of allocating a
        // fresh BirdState each time -- Predict() reads it synchronously and
        // never retains a reference, so mutating the same instance is safe,
        // and it cuts a real allocation source: up to MaxSimFrames times per
        // episode, PopulationSize episodes per generation. Measured for real
        // (not guessed): the training loop was allocating ~4.8GB over one
        // full run before this, enough Gen0/Gen1 GC pauses (~1100 of them in
        // under a minute) to noticeably stall even an idle thread -- which
        // is what made scrolling elsewhere in the IDE stutter while this
        // sample trained in the background. `state` below (the TRUE,
        // unbiased reading, stored in traj for training) still allocates
        // once per frame -- it has to outlive this loop iteration, unlike
        // this scratch object.
        var predictScratch = new BirdState();

        for (; frame < MaxSimFrames; frame++)
        {
            var state = ComputeFeatures(pipeX, pipeGapCenterY, playerY, playerVelocity);

            bool flap;
            if (engine != null)
            {
                predictScratch.F1 = state.F1;
                predictScratch.F2 = state.F2;
                predictScratch.F3 = state.F3;
                predictScratch.F4 = state.F4 + biasF4;
                predictScratch.F5 = state.F5;
                flap = engine.Predict(predictScratch).PredictedLabel;
            }
            else
            {
                flap = (state.F4 + biasF4) < 0f;
            }

            traj.Add((state, flap)); // records the TRUE (unbiased) state alongside the actual action taken

            bool dead = StepPhysics(pipeX, pipeGapCenterY, pipeScored, ref playerY, ref playerVelocity, flap,
                (min, max) => min + (float)(rand.NextDouble() * (max - min)), ref score);

            if (dead) { frame++; break; }
        }

        return (score, frame, traj);
    }

    // ---- shared physics, used verbatim by both SimulateEpisode() (headless,
    // background thread) and StepDisplayBird() (Draw(), render thread) --
    // the whole point of a self-play trainer is that the physics a policy
    // was TRAINED against and the physics it's actually SHOWN playing under
    // are identical. nextGapRandom is the one deliberate difference: each
    // caller hands in whichever random generator belongs to its own thread. ----

    private static bool StepPhysics(
        List<float> pipeX, List<float> pipeGapCenterY, List<bool> pipeScored,
        ref float playerY, ref float playerVelocity, bool flap,
        Func<float, float, float> nextGapRandom, ref int score)
    {
        if (flap)
            playerVelocity = -FlapStrength;
        playerVelocity += Gravity * SimDt;
        playerY += playerVelocity * SimDt;

        bool dead = false;
        for (int i = 0; i < pipeX.Count; i++)
        {
            pipeX[i] -= PipeSpeed * SimDt;

            bool withinX = PlayerX + PlayerRadius > pipeX[i] && PlayerX - PlayerRadius < pipeX[i] + PipeWidth;
            bool withinGap = playerY - PlayerRadius > pipeGapCenterY[i] - PipeGap / 2f
                           && playerY + PlayerRadius < pipeGapCenterY[i] + PipeGap / 2f;
            if (withinX && !withinGap)
                dead = true;

            if (!pipeScored[i] && pipeX[i] + PipeWidth < PlayerX)
            {
                pipeScored[i] = true;
                score++;
            }
        }

        if (pipeX.Count == 0 || pipeX[pipeX.Count - 1] < CanvasWidth - PipeSpacing)
        {
            pipeX.Add(CanvasWidth);
            pipeGapCenterY.Add(nextGapRandom(PipeGap, CanvasHeight - PipeGap));
            pipeScored.Add(false);
        }

        while (pipeX.Count > 0 && pipeX[0] < -PipeWidth)
        {
            pipeX.RemoveAt(0);
            pipeGapCenterY.RemoveAt(0);
            pipeScored.RemoveAt(0);
        }

        if (playerY - PlayerRadius < 0 || playerY + PlayerRadius > CanvasHeight)
            dead = true;

        return dead;
    }

    // Ballistic lookahead (F5): where will the bird be in `Lookahead`
    // seconds if it does NOT flap again, given its current velocity?
    // Positive means that projected position falls below the gap's bottom
    // edge -- i.e. inaction leads to a collision. This folds the
    // velocity/position INTERACTION a plain linear model can't discover on
    // its own directly into one feature, alongside the simpler F4
    // (instantaneous gap offset) the model also gets.
    private static BirdState ComputeFeatures(List<float> pipeX, List<float> pipeGapCenterY, float playerY, float playerVelocity)
    {
        float nextPipeX = PlayerX + CanvasWidth;
        float nextGapY = CanvasHeight / 2f;
        for (int i = 0; i < pipeX.Count; i++)
        {
            if (pipeX[i] + PipeWidth >= PlayerX)
            {
                nextPipeX = pipeX[i];
                nextGapY = pipeGapCenterY[i];
                break;
            }
        }

        float predictedY = playerY + playerVelocity * Lookahead + 0.5f * Gravity * Lookahead * Lookahead;
        float gapBottom = nextGapY + PipeGap / 2f;

        return new BirdState
        {
            F1 = playerY / CanvasHeight,
            F2 = playerVelocity / 1000f,
            F3 = (nextPipeX - PlayerX) / CanvasWidth,
            F4 = (nextGapY - playerY) / CanvasHeight, // instantaneous: negative = gap is above the bird right now
            F5 = (predictedY - gapBottom) / CanvasHeight, // projected: positive = inaction leads to a collision soon
        };
    }

    private class BirdState
    {
        public float F1, F2, F3, F4, F5;
        public bool Label;
    }

    private class FlapPrediction
    {
        [ColumnName(""PredictedLabel"")]
        public bool PredictedLabel;
    }
}
";

        private const string GpuParticles =
@"// 200,000 particles, simulated entirely on the GPU every frame via
// CreateGpuParticles() -- the whole point being that a CPU port of this
// exact particle count/motion would visibly choke, unlike the hundreds
// this sample gallery's other particle samples use. simulationGlsl below
// is NOT a full shader: it's a GLSL statement body the engine splices
// into a per-particle update, reading/writing bare `position`/`velocity`
// plus uDeltaTime/uTime and the dana_noise3()/dana_curlNoise3() helpers.
//
// The SAME snippet compiles into whichever backend this machine actually
// supports, with zero branching in this sketch: a real GLSL compute
// shader (glDispatchCompute + an SSBO) on Windows/Linux with GL 4.3+, or
// a ping-pong render-to-texture simulation everywhere else -- notably
// macOS, whose native OpenGL implementation is hard-capped at 4.1, below
// what compute shaders need. _particles.UsesComputeShader (used only for
// the on-screen label below) says which one actually engaged; nothing
// about how this sketch behaves depends on it.
public class MySketch : Sketch
{
    private const int Count = 200_000;
    private GpuParticleSystem _particles = null!;

    public override void Setup()
    {
        Size(900, 600, RendererKind.Renderer3D);

        var positions = new PVector[Count];
        for (int i = 0; i < Count; i++)
            positions[i] = new PVector(Random(-300, 300), Random(-300, 300), Random(-300, 300));

        string sim = @""
            vec3 curl = dana_curlNoise3(position * 0.006 + vec3(0.0, uTime * 0.05, 0.0));
            velocity = mix(velocity, curl * 60.0, 0.05);
            position += velocity * uDeltaTime;
        "";

        _particles = CreateGpuParticles(Count, sim, positions);
    }

    public override void Draw()
    {
        Background(8, 10, 16);
        StepParticles(_particles, 1f / 60f);

        Camera(0, 0, 700, 0, 0, 0, 0, 1, 0);
        PushMatrix();
        RotateY(FrameCount / 400f);
        FillHSB((FrameCount * 0.3f) % 360f, 70, 100);
        DrawParticles(_particles, 3f);
        PopMatrix();

        Fill(255);
        NoStroke();
        TextSize(13);
        string backend = _particles.UsesComputeShader
            ? ""GLSL compute shader (glDispatchCompute)""
            : ""fragment-shader ping-pong texture (compute unavailable here)"";
        Text($""{Count:N0} particles, GPU curl noise -- backend: {backend}"", 14, Height - 16);
    }
}
";
    }
}