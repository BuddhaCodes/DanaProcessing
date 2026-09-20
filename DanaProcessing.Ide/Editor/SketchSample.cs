using Avalonia.Animation;
using Avalonia.Markup.Xaml.MarkupExtensions;
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
                "Sketch mínimo",
                "Setup()/Draw() chico -- un círculo que sigue al mouse, crece mientras mantenés apretado el botón, y cambia de color con cada click.",
                MinimalSketch),
             
            new SketchSample(
                "Sketch mínimo 3D",
                "Setup()/Draw() ",
                MinimalSketch3D),

            new SketchSample(
                "Árbol fractal",
                "Árbol recursivo con ángulo controlado por el mouse, coloreado por profundidad.",
                FractalTree),

            new SketchSample(
                "Cubo 3D (Silk.NET)",
                "Box() con inercia real: arrastrá para rotarlo, soltá y sigue girando por su propia velocidad. La rueda escala el cubo, el click cambia de color y la tecla L compara con/sin Lights().",
                Box3D),

            new SketchSample(
                "Esfera 3D (Silk.NET)",
                "Sphere()/SphereDetail() en vivo -- el mouse en X cambia la resolución de la malla, el mouse en Y cambia el color (FillHSB), arrastrá para rotar con inercia, la rueda escala el radio y el click prende/apaga las luces.",
                Sphere3D),

            new SketchSample(
                "Cámara 3D (Silk.NET)",
                "Camera()/Perspective()/Ortho() en vivo -- el mouse orbita la cámara, la rueda hace zoom, la tecla P alterna perspectiva/ortográfica y el click recorre una paleta de colores sobre la grilla de cubos.",
                Camera3D),

            new SketchSample(
                "Luces 3D (Silk.NET)",
                "PointLight()/SpotLight()/LightFalloff() sobre una grilla de esferas -- el mouse mueve la luz, la rueda ajusta el falloff en vivo, la tecla L alterna point/spot light y el click prende/apaga la luz ambiente.",
                Lights3D),

            new SketchSample(
                "Material 3D (Silk.NET)",
                "Ambient()/Specular()/Emissive()/Shininess() sobre una fila de esferas -- el mouse en X barre la Shininess(), el mouse en Y cambia el color del Specular(), la rueda controla el brillo de la luz, el click cambia el Fill() y la tecla E alterna un Emissive() fijo.",
                Material3D),

            new SketchSample(
                "Cámara avanzada: beginCamera/endCamera (Silk.NET)",
                "BeginCamera()/EndCamera() en vivo -- arma un rig de cámara con Translate()/RotateY()/RotateX() (las mismas llamadas que usarías para mover un objeto, pero apuntando a la cámara) en vez de calcular eye/center a mano como en el sample de Camera3D. El mouse orbita, la rueda hace zoom acercando la cámara sobre su propio eje.",
                PerspectiveDemo3D),

            new SketchSample(
                "Coordenadas 3D→2D: modelX/Y/Z + screenX/Y/Z (Silk.NET)",
                "ModelX/Y/Z() para \"anclar\" un punto en espacio 3D después de una serie de transformaciones (igual que el ejemplo oficial de Processing), y ScreenX/Y/Z() para proyectar un punto 3D a coordenadas de pantalla y dibujar una etiqueta 2D justo encima de un cubo que gira.",
                Coordinates3D),

            new SketchSample(
                "Shader custom: PShader (Silk.NET)",
                "LoadShader()/Shader()/ResetShader() en vivo -- compila un fragment shader GLSL que colorea por normal (una esfera con cada cara pintada según hacia dónde mira, ignorando luces y Fill()) y lo compara contra el shading normal con solo un click.",
                Shader3D),

            new SketchSample(
                "normal() (Silk.NET)",
                "Muestra la firma de normal(nx, ny, nz) -- por ahora solo guarda el valor (no tiene efecto visible todavía: hace falta una API de formas 3D por vértice, tipo beginShape()/vertex(), que este motor no tiene aún). Este sample lo deja documentado en código en vez de dejarlo sin ejemplo.",
                Normal3D),

            new SketchSample(
                "Lluvia de círculos: circle()",
                "Circle(x, y, d) en vivo -- lluvia de círculos que caen y rebotan, el mouse en X controla cuántos caen por segundo, la rueda cambia el tamaño, y cada click cambia de paleta.",
                CircleRain),

            new SketchSample(
                "Flota reutilizable: createShape()",
                "CreateShape(GROUP, ...) arma una navecita una sola vez en Setup() a partir de Rect()+Triangle()+Ellipse(), y Shape() la estampa muchas veces por frame -- cada click agrega una nave nueva en el mouse, todas giran a su propia velocidad sin volver a construir la geometría.",
                ShapeFleet),

            new SketchSample(
                "Ecualizador reordenable: FloatList",
                "Un FloatList de alturas al estilo ecualizador -- click lo reordena con Shuffle(), la tecla S lo ordena con Sort(), la tecla R genera valores nuevos, y las líneas punteadas marcan Min()/Max()/Average() en vivo mientras cambian.",
                ReorderableEqualizer),

            new SketchSample(
                "Reloj de bajo consumo: delay()",
                "Un reloj analógico real (Hour()/Minute()/Second()) que llama Delay(1000) al final de cada Draw() -- en vez de redibujar cientos de veces por segundo sin necesidad, se redibuja una sola vez por segundo, como recomienda la referencia de Processing para sketches que no necesitan animación fluida.",
                LowPowerClock),
            new SketchSample(
                "Modo presentación: FullScreen()",
                "Un caleidoscopio en HSB que gira solo -- la tecla F llama FullScreen() y el HUD de abajo muestra Width/Height (lógicos) junto a PixelWidth/PixelHeight (reales) y DisplayDensity(), para ver los cuatro juntos en un caso con contenido de verdad.",
                PresentationMode),

            new SketchSample(
                "Panel de ventana: windowMove/Resizable/Title/Ratio",
                "Un panel con log en pantalla para las funciones de ventana -- F: FullScreen(), M: WindowMove() a una posición al azar, R: WindowResizable(), T: WindowTitle() al azar, A: WindowRatio(16,9). Cada tecla imprime en el log qué se pidió y qué devolvió el estado (IsFullScreen, IsWindowResizable, WindowTitleText); si el host de la IDE todavía no escucha estos eventos, el log documenta igual el llamado -- queda listo para cuando se conecte.",
                WindowControlPanel),

            new SketchSample(
                "Tamaño dinámico: Settings()",
                "Settings() corre ANTES que Setup() -- acá decide una orientación (retrato o paisaje) al azar y llama Size() con esa decisión, así Setup() ya arranca con Width/Height correctos sin tener que adivinarlos de antemano. Click reelige la orientación en cualquier momento llamando Size() directo, para contrastar con la garantía de orden que da Settings().",
                DynamicSizeSettings),
                // --- dentro de SketchSamples.All, agregar: ---
            new SketchSample(
                "Contador binario: Binary()/Unbinary()",
                "Un contador de 0 a 255 mostrado como 8 bits que se prenden y apagan -- Binary(byte) arma la fila, Unbinary() la vuelve a convertir en número para probar que van y vuelven. La rueda cambia la velocidad, y se puede forzar un bit a mano con click.",
                BinaryCounter),

            new SketchSample(
                "Búsqueda en paralelo: thread()",
                "Thread(\"SearchPrimes\") lanza la búsqueda de primos en un hilo aparte apenas arranca el sketch -- el spinner de la izquierda sigue girando fluido en Draw() mientras tanto, sin trabarse, porque el trabajo pesado vive en su propio hilo. Click reinicia la búsqueda.",
                PrimeSearchThread),

            new SketchSample(
                "Exportar a PDF: beginRaw()/endRaw()",
                "Un póster generativo (círculos en espiral con color HSB) que se dibuja normal cada frame -- la tecla V llama al MISMO método de dibujo una vez más, esta vez encerrado entre BeginRaw()/EndRaw(), y ese segundo llamado no aparece en pantalla: se va directo a poster.pdf como vector real, no como imagen.",
                PdfExport),

            new SketchSample(
                "Respaldo de trazos: saveStream()",
                "Dibujá con el mouse -- la tecla S guarda el trazo en trazo.txt con SaveStrings() y después usa CreateInput() + SaveStream() para copiar ese archivo entero a un backup con nombre único, sin leerlo a mano línea por línea.",
                StrokeBackup),

            new SketchSample(
                "Tarjeta de datos: parseJSONObject()/parseXML() + launch()",
                "Arma un JSONObject y un fragmento de XML con la API normal, los serializa a String, y los vuelve a leer con ParseJSONObject()/ParseXML() -- exactamente como llegarían datos desde una red o un campo de texto, no desde un archivo. La tecla L abre el sitio guardado en el JSON con Launch(), en el navegador del sistema.",
                DataCardParseLaunch),
            new SketchSample(
                "Gema facetada: BeginShape/Vertex(x,y,z)/Normal() en 3D",
                "Un octaedro armado a mano, cara por cara, con BeginShape(Triangles)+Vertex(x,y,z)+Normal() -- la tecla N alterna entre sombreado plano (una normal por cara, via PVector.Cross()) y suave (normales promediadas por vértice), para ver en vivo qué cambia normal() en la iluminación. Arrastrá para rotar.",
                FacetedGem),

            new SketchSample(
                "Partículas 3D: PVector con Z",
                "PVector ahora tiene X, Y, y Z -- este sistema de partículas usa Add()/Sub() de PVector para gravedad y rebote en las TRES dimensiones dentro de un cubo invisible, en vez de simular la profundidad a mano con floats sueltos. Click agrega más partículas.",
                Particles3D),

             new SketchSample(
                "Enjambre reutilizable: CreateShape3D()",
                "La misma gema facetada del sample anterior, pero armada UNA sola vez con CreateShape3D() en vez de BeginShape()/EndShape() cada frame -- la malla se sube a la GPU una vez, y después 150 copias se dibujan solo con Shape(), cada una con su propia posición y rotación, sin volver a triangular ni volver a subir nada.",
                ReusableSwarm),

            new SketchSample(
                "Cartel texturizado: Vertex(x,y,z,u,v)",
                "Un panel 3D con una textura generada en código (un PGraphics 2D convertido a PImage con Get()) mapeada por Vertex(x,y,z,u,v) -- Texture(img) antes de BeginShape() le dice al shape qué imagen indexan esas coordenadas. Arrastrá para rotar y ver el mapeo desde otros ángulos.",
                TexturedBillboard),
        };

        private const string CircleRain =
@"// Lluvia de circulos -- demo de Circle(x, y, d), el atajo nuevo para
// Ellipse(x, y, d, d). Cada particula es un circulo que cae con gravedad
// simple y rebota contra el piso perdiendo energia, hasta que se
// desvanece y se recicla arriba con un tamano nuevo.
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
 
        // El mouse en X controla cuantas gotas nuevas entran por frame --
        // de 0 (nada) a ~1 por frame cerca del borde derecho.
        float spawnChance = Map(MouseX, 0, Width, 0f, 1f);
        if (Random(1f) < spawnChance)
            _drops.Add(NewDrop(-20));
 
        NoStroke();
        Fill(_palette[_paletteIndex]);
 
        foreach (var drop in _drops)
        {
            drop.VY += 0.4f; // gravedad
            drop.Y += drop.VY;
 
            float floor = Height - drop.Size / 2f;
            if (drop.Y > floor)
            {
                drop.Y = floor;
                drop.VY *= -0.55f; // rebote con perdida de energia
            }
 
            Circle(drop.X, drop.Y, drop.Size * _sizeScale);
        }
 
        // Reciclar las que ya casi no rebotan, para no acumular para siempre.
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
@"// Flota reutilizable -- demo de CreateShape(). La navecita (un Triangle
// como nariz, un Rect como fuselaje y una Ellipse como motor) se arma UNA
// sola vez en Setup() con CreateShape(GROUP, ...), en vez de volver a
// llamar Triangle()/Rect()/Ellipse() a mano cada frame para cada nave. Cada
// click agrega una nave nueva en la posicion del mouse -- todas comparten
// la MISMA geometria (el mismo PShape), solo cambia donde y con que
// rotacion se estampa via Shape().
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
 
        // Geometria centrada en (0,0): nariz apuntando hacia -Y, fuselaje
        // rectangular, motor como elipse en la cola. Fill()/Stroke() en el
        // momento de cada CreateShape() quedan HORNEADOS en esa pieza --
        // por eso se fija el color antes de cada llamada.
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
@"// Ecualizador reordenable -- demo de FloatList. Las 24 alturas de las
// barras viven en un solo FloatList en vez de un arreglo fijo a mano --
// click llama Shuffle() para desordenarlas con una animacion, la tecla S
// llama Sort(), la tecla R genera valores nuevos con Random(), y
// Min()/Max()/Average() (todos metodos de FloatList) dibujan las lineas de
// referencia que se mueven solas cuando los datos cambian.
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
            // Interpola suave hacia el valor objetivo, para que Shuffle()/
            // Sort() se vean como una animacion en vez de un salto brusco.
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
@"// Reloj de bajo consumo -- demo de Delay(). Un reloj analogico comun
// (Hour()/Minute()/Second() para las manecillas) que, a diferencia de
// todos los demas samples de esta lista, NO necesita 60 cuadros por
// segundo: nada en pantalla cambia mas de una vez por segundo. Por eso
// Draw() termina con Delay(1000) -- el hilo de animacion se detiene ese
// segundo entero en vez de recalcular y redibujar un reloj identico
// cientos de veces sin necesidad. Es exactamente el caso de uso que
// recomienda la referencia de Processing para delay(): no para animar
// suave (para eso esta FrameRate()), sino para sketches de bajo consumo
// que solo necesitan redibujar de tanto en tanto.
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
 
        // Marcas de hora.
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
 
        // La linea que hace todo el punto de este sample: bloquea el hilo
        // de animacion un segundo entero antes de volver a Draw(), en vez
        // de recalcular/redibujar un reloj identico decenas de veces sin
        // necesidad mientras el segundero no cambio.
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

        // Mientras se mantiene apretado el mouse, el circulo crece; al
        // soltarlo vuelve a su tamano normal -- IsMousePressed se lee cada
        // frame, Lerp() suaviza el cambio en vez de saltar de golpe.
        _size = Lerp(_size, IsMousePressed ? 120f : 60f, 0.1f);

        NoStroke();
        Fill(_palette[_colorIndex]);
        Ellipse(MouseX, MouseY, _size, _size);
    }

    public override void MouseClicked()
    {
        // Cada click avanza al proximo color de la paleta.
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
@"// Arbol fractal recursivo, adaptado del ejemplo ""Recursive Tree"" de p5.js.
// Paleta: tronco en rojo oscuro, ramas interpolando de naranja a verde
// segun la profundidad, hojas/fondo en tonos calidos.
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
@"// Demo del backend 3D (Silk.NET/OpenGL) -- mismo patron que los demas
// samples 3D: un PGraphics offscreen con Renderer3D, creado UNA sola vez
// en Setup() y reusado cada frame (Renderer3DBackend.Create() levanta una
// ventana Silk.NET oculta + contexto OpenGL + shader + mesh del cubo,
// demasiado caro para repetir por frame).
//
// ROTACION: trackball de verdad, con un cuaternion acumulado (_qw/_qx/_qy/_qz)
// en vez de dos angulos de Euler sumados por separado (RotateY(rotY) +
// RotateX(rotX)). La version vieja tenia un bug real: como RotateX()/
// RotateY() giran siempre alrededor de los ejes ORIGINALES del cubo (no de
// los ejes tal como se ven en pantalla ahora mismo), arrastrar hacia
// arriba/abajo se sentia bien solo mientras la cara de adelante seguia de
// frente. En cuanto una rotacion previa dejaba una cara de costado, ese
// mismo arrastre vertical terminaba girando el cubo como una rueda en vez
// de inclinarlo -- y con la cara de atras mirando a camara, el eje quedaba
// espejado, asi que arrastrar a la derecha lo giraba a la izquierda.
//
// La solucion (arcball estandar): cada frame de arrastre calcula un eje de
// rotacion en el espacio de la PANTALLA (perpendicular al movimiento del
// mouse) y lo compone PRE-multiplicando sobre el cuaternion acumulado --
// es decir, el incremento nuevo se aplica en espacio mundo/camara, no en
// el espacio local (ya rotado) del cubo. Asi arrastrar siempre gira
// alrededor del eje que se ve en pantalla en ESE momento, sin importar
// cuanto se haya girado antes ni que cara este mirando a camara. Al
// soltar, se seguye aplicando el ultimo incremento con friccion (*0.99 por
// frame) -- misma inercia que antes, pero sin el bug de eje fijo.
// Para dibujar, el cuaternion se descompone a angulo+eje UNA sola vez por
// frame y se manda con el nuevo Rotate(angle, x, y, z) (rotacion sobre eje
// arbitrario) -- una sola rotacion combinada, en vez de dos RotateX/RotateY
// separadas que se pisan entre si.
public class MySketch : Sketch
{
    private PGraphics _scene3d;

    // Orientacion acumulada como cuaternion (w, x, y, z), identidad = sin rotar.
    private float _qw = 1f, _qx, _qy, _qz;
    // Ultimo incremento de arrastre (eje normalizado + angulo) -- se seguye
    // aplicando con friccion mientras el mouse esta suelto, para la inercia.
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

    public override void Setup()
    {
        Size(800, 600);
        _scene3d = CreateGraphics(Width, Height, RendererKind.Renderer3D);
    }

    public override void Draw()
    {
        if (IsMousePressed)
        {
            float dx = MouseX - PMouseX;
            float dy = MouseY - PMouseY;
            float dragMag = Mag(dx, dy);
            if (dragMag > 0.001f)
            {
                // Eje perpendicular al arrastre, EN ESPACIO PANTALLA -- no
                // en espacio del objeto, que es justo lo que evita el bug
                // de la version anterior. Y sin invertir: el mundo 3D es
                // Y-abajo (ver COORDINATE CONVENTION en Renderer3DBackend.cs)
                // y ese flip ya esta incluido aca abajo (-dy).
                _spinAxisX = -dy / dragMag;
                _spinAxisY = dx / dragMag;
                _spinAxisZ = 0f;
                _spinAngle = dragMag * 0.01f;
            }
        }

        ApplySpin(_spinAxisX, _spinAxisY, _spinAxisZ, _spinAngle);
        if (!IsMousePressed)
            _spinAngle *= 0.99f; // friccion solo mientras gira libre (soltado)

        _scene3d.BeginDraw();
        // Lights() cada frame -- el backend resetea la lista de luces en
        // cada BeginFrame(), igual que Processing: sin esto el cubo se ve
        // plano (Fill() sin sombrear es el default real de Processing).
        // La tecla L apaga esto a proposito para que se note la diferencia.
        if (_lit)
            _scene3d.Lights();
        _scene3d.Fill(_palette[_colorIndex]);
        _scene3d.PushMatrix();
        _scene3d.Translate(Width / 2f, Height / 2f, 0);

        // Angulo + eje total, derivados del cuaternion acumulado -- UNA
        // sola rotacion combinada por frame.
        float angle = 2f * Acos(Constrain(_qw, -1f, 1f));
        float s = Sqrt(Max(1f - _qw * _qw, 0f));
        if (s < 0.0001f)
            _scene3d.Rotate(angle, 1, 0, 0); // angulo ~0: el eje no importa
        else
            _scene3d.Rotate(angle, _qx / s, _qy / s, _qz / s);

        _scene3d.Box(_boxSize);
        _scene3d.PopMatrix();
        _scene3d.EndDraw();

        Image(_scene3d.Get(), 0, 0);

        Fill(255);
        TextSize(13);
        string lightsStatus = _lit ? ""ON"" : ""OFF"";
        Text($""Arrastra para rotar (trackball real, sin importar la cara que mires) -- rueda escala ({(int)_boxSize}px) -- click cambia color -- tecla L: luces {lightsStatus}"", 12, Height - 16);
    }

    // Compone un incremento de rotacion (eje normalizado + angulo, en
    // radianes) sobre el cuaternion acumulado, PRE-multiplicando -- el
    // incremento nuevo se aplica en espacio mundo, encima de la
    // orientacion existente, que es lo que hace que el arrastre siempre
    // corresponda a los ejes de PANTALLA en vez de a los ejes (ya girados)
    // del objeto.
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

        // Renormalizar -- sin esto, el error de punto flotante acumulado
        // frame a frame termina deformando el cubo en vez de solo rotarlo.
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
@"// Demo de Sphere()/SphereDetail() sobre el mismo backend 3D que Box3D --
// mismo patron de PGraphics offscreen con Renderer3D creado una sola vez
// en Setup(). El mouse en X controla SphereDetail() en vivo (rango 3-60)
// para que se note el efecto de la resolucion de malla: a la izquierda,
// una esfera claramente low-poly (facetada); a la derecha, un mesh mas
// fino -- EnsureSphereMesh() en Renderer3DBackend solo re-sube la malla
// cuando el detalle realmente cambia entre frames.
//
// Sumado a eso: el mouse en Y recorre el circulo de matices via FillHSB()
// (0-360 grados), la rueda cambia el radio en vivo, y el click prende/apaga
// las luces para comparar el shading contra el flat-color de Processing.
//
// ROTACION: mismo trackball por cuaternion que Box3D (ver sus comentarios
// para el porque) -- arrastrar siempre gira alrededor del eje que se ve en
// pantalla en ese momento, en vez de los ejes fijos del objeto, asi que no
// se invierte ni se pone raro segun que lado de la esfera este mirando a
// camara.
public class MySketch : Sketch
{
    private PGraphics _scene3d;

    private float _qw = 1f, _qx, _qy, _qz;
    private float _spinAxisX = 1f, _spinAxisY, _spinAxisZ, _spinAngle;

    private float _radius = 160;
    private bool _lit = true;

    public override void Setup()
    {
        Size(800, 600);
        _scene3d = CreateGraphics(Width, Height, RendererKind.Renderer3D);
    }

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

        _scene3d.BeginDraw();
        if (_lit)
            _scene3d.Lights();
        _scene3d.SphereDetail(detail);
        _scene3d.FillHSB(hue, 65, 95);
        _scene3d.PushMatrix();
        _scene3d.Translate(Width / 2f, Height / 2f, 0);

        float angle = 2f * Acos(Constrain(_qw, -1f, 1f));
        float s = Sqrt(Max(1f - _qw * _qw, 0f));
        if (s < 0.0001f)
            _scene3d.Rotate(angle, 1, 0, 0);
        else
            _scene3d.Rotate(angle, _qx / s, _qy / s, _qz / s);

        _scene3d.Sphere(_radius);
        _scene3d.PopMatrix();
        _scene3d.EndDraw();

        Image(_scene3d.Get(), 0, 0);

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
@"// Demo de Camera()/Perspective()/Ortho() -- mismo patron de PGraphics
// offscreen con Renderer3D que los samples anteriores. El mouse orbita la
// camara alrededor del origen de la escena (Camera() con eye calculado a
// mano en vez del translate/rotate de siempre -- la camara es su propia
// matriz, separada del stack de modelo). La tecla P alterna entre
// Perspective() (con foreshortening, los cubos lejanos se ven mas chicos)
// y Ortho() (proyeccion paralela, todos los cubos miden lo mismo en
// pantalla sin importar la distancia) -- comparalos sobre la misma grilla
// de cubos para notar la diferencia.
//
// Sumado a eso: la rueda del mouse acerca/aleja la camara (cambia el
// radio de la orbita, no el FOV -- un zoom de verdad, moviendo el eye),
// y cada click recorre una paleta de colores calculada con FillHSB() por
// posicion en la grilla, para que se note que cada cubo mantiene su
// propio color mientras la camara se mueve alrededor de todos.
public class MySketch : Sketch
{
    private PGraphics _scene3d;
    private bool _usePerspective = true;
    private float _orbitRadius = 500;
    private float _hueShift;

    public override void Setup()
    {
        Size(800, 600);
        _scene3d = CreateGraphics(Width, Height, RendererKind.Renderer3D);
    }

    public override void Draw()
    {
        Background(20, 20, 30);

        float orbitAngle = Map(MouseX, 0, Width, 0, TWO_PI);
        float orbitHeight = Map(MouseY, 0, Height, 400, -400);

        float eyeX = Width / 2f + Cos(orbitAngle) * _orbitRadius;
        float eyeZ = Sin(orbitAngle) * _orbitRadius;
        float eyeY = Height / 2f + orbitHeight;

        _scene3d.BeginDraw();
        _scene3d.Lights();

        _scene3d.Camera(eyeX, eyeY, eyeZ, Width / 2f, Height / 2f, 0, 0, 1, 0);

        if (_usePerspective)
            _scene3d.Perspective();
        else
            _scene3d.Ortho(-Width / 2f, Width / 2f, -Height / 2f, Height / 2f);

        for (int gx = -2; gx <= 2; gx++)
        {
            for (int gz = 0; gz <= 4; gz++)
            {
                float hue = (_hueShift + gx * 40 + gz * 25) % 360f;
                if (hue < 0)
                    hue += 360f;
                _scene3d.FillHSB(hue, 55, 90);

                _scene3d.PushMatrix();
                _scene3d.Translate(Width / 2f + gx * 140, Height / 2f, gz * -140);
                _scene3d.Box(80);
                _scene3d.PopMatrix();
            }
        }

        _scene3d.EndDraw();

        Image(_scene3d.Get(), 0, 0);

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
        @"// Demo de PointLight()/SpotLight()/LightFalloff() -- mismo patron de
// PGraphics offscreen con Renderer3D que los samples anteriores. El mouse
// mueve una luz sobre una grilla de esferas, con una AmbientLight tenue
// de base (para que se lea la forma de las esferas fuera del punto
// iluminado) mas el point/spot light siguiendo al mouse. La tecla L
// alterna entre PointLight() (ilumina en todas direcciones) y SpotLight()
// (cono apuntando hacia abajo, con angulo y concentration fijos).
//
// Sumado a eso: la rueda del mouse ajusta en vivo el coeficiente LINEAL de
// LightFalloff(1, linear, 0) -- subirlo hace que la luz se apague mas
// rapido con la distancia (halo chico y marcado); bajarlo la deja iluminar
// una zona mas amplia. El click prende/apaga la AmbientLight() para
// comparar contra el negro total fuera del alcance de la luz principal.
public class MySketch : Sketch
{
    private PGraphics _scene3d;
    private bool _useSpot;
    private bool _ambientOn = true;
    private float _falloffLinear = 0.0025f;

    public override void Setup()
    {
        Size(800, 600);
        _scene3d = CreateGraphics(Width, Height, RendererKind.Renderer3D);
    }

    public override void Draw()
    {
        Background(15, 15, 20);

        float lightX = Map(MouseX, 0, Width, Width / 2f - 300, Width / 2f + 300);
        // Rango simetrico alrededor del centro de la grilla de esferas
        // (Height/2 = 300 para un canvas de 600px de alto) -- antes iba de
        // 60 a 320, casi todo por ENCIMA del centro (240px arriba contra
        // solo 20px abajo), asi que la luz practicamente nunca se sentia
        // debajo de las esferas. 60..(Height-60) deja el mismo margen de
        // los dos lados.
        float lightY = Map(MouseY, 0, Height, 60, Height - 60);
        float lightZ = 150;

        _scene3d.BeginDraw();

        if (_ambientOn)
            _scene3d.AmbientLight(25, 25, 32);
        // LightFalloff(1, linear, 0) usa atenuacion LINEAL, no cuadratica --
        // a la escala de esta escena (distancias de ~100-400 unidades) un
        // falloff cuadratico satura muy rapido; el lineal da una caida mas
        // suave y facil de calibrar con la rueda del mouse.
        _scene3d.LightFalloff(1, _falloffLinear, 0);
        if (_useSpot)
            _scene3d.SpotLight(255, 220, 180, lightX, lightY, lightZ, 0, 1, 0, Radians(35), 8);
        else
            _scene3d.PointLight(255, 220, 180, lightX, lightY, lightZ);

        _scene3d.Fill(210, 210, 210);
        for (int gx = -2; gx <= 2; gx++)
        {
            for (int gz = -1; gz <= 1; gz++)
            {
                _scene3d.PushMatrix();
                _scene3d.Translate(Width / 2f + gx * 130, Height / 2f, gz * 130);
                _scene3d.Sphere(45);
                _scene3d.PopMatrix();
            }
        }

        _scene3d.EndDraw();

        Image(_scene3d.Get(), 0, 0);

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
        @"// Demo de Material Properties -- Ambient()/Specular()/Emissive()/Shininess() --
// mismo patron de PGraphics offscreen con Renderer3D que los samples
// anteriores. Una fila de esferas con Shininess() creciente de izquierda a
// derecha muestra como el highlight especular se va cerrando y brillando
// -- el mouse en X escala esa progresion completa para barrerla en vivo
// (a la izquierda casi no hay highlight, a la derecha uno chico y
// brillante).
//
// El mouse en Y interpola el color de Specular() entre blanco y magenta
// via LerpColor() -- separa claramente el color del highlight (el de la
// luz/specular) del color del cuerpo de la esfera (el del Fill()/ambient).
// Notese el LightSpecular(255,255,255) en Draw(): sin eso, Shininess() y
// Specular() no se ven NUNCA, sin importar el valor -- el highlight
// depende tanto del material como de que la luz aporte color especular
// (por default es negro, igual que en Processing real).
// La rueda del mouse controla el brillo de la PointLight() (0-255), asi
// se ve como el material reacciona a mas o menos luz incidente sin tocar
// ninguna propiedad del material en si. El click recorre una paleta para
// el Fill() base, y la tecla E prende un Emissive() fijo sobre la esfera
// del medio -- notese que se ve 'iluminada por dentro' sin importar el
// brillo de la luz ni donde este, porque Emissive() no depende de ella.
public class MySketch : Sketch
{
    private PGraphics _scene3d;
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

    public override void Setup()
    {
        Size(800, 600);
        _scene3d = CreateGraphics(Width, Height, RendererKind.Renderer3D);
    }

    public override void Draw()
    {
        Background(15, 15, 20);

        float shininessScale = Map(MouseX, 0, Width, 1, 40);
        float specAmt = Map(MouseY, 0, Height, 0, 1);
        var specColor = LerpColor(new Color(255, 255, 255), new Color(255, 80, 200), specAmt);
        int count = 5;

        _scene3d.BeginDraw();
        _scene3d.AmbientLight(40, 40, 48);
        // Sin esto, Shininess()/Specular() no tienen NINGUN efecto visible
        // sin importar el valor que se les ponga: el highlight especular
        // necesita tanto el material (Specular()/Shininess()) COMO que la
        // luz misma aporte color especular, y LightSpecular() es negro
        // (0,0,0) por default -- igual que en Processing real, hace falta
        // llamarlo explicitamente para que cualquier luz brille.
        _scene3d.LightSpecular(255, 255, 255);
_scene3d.PointLight(_lightBrightness, _lightBrightness, _lightBrightness, Width / 2f, 120, 220);

for (int i = 0; i < count; i++)
{
    _scene3d.PushMatrix();
    _scene3d.Translate(Width / 2f + (i - (count - 1) / 2f) * 130, Height / 2f, 0);

    _scene3d.Fill(_palette[_colorIndex]);
    _scene3d.Specular(Red(specColor), Green(specColor), Blue(specColor));
   _scene3d.Shininess((i + 1) * shininessScale);

    if (_emissiveOn && i == count / 2)
        _scene3d.Emissive(80, 20, 90);
    else
        _scene3d.Emissive(0, 0, 0);

    _scene3d.Sphere(50);
    _scene3d.PopMatrix();
}

_scene3d.EndDraw();

Image(_scene3d.Get(), 0, 0);

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
// Perspective -- traducido del sample oficial de Processing.
// Mové el mouse en X para cambiar el field of view (fov).
// Click para alternar el aspect ratio.
//
// perspective(fov, aspect, near, far) define un volumen de vista con
// forma de piramide truncada: los objetos cerca del near plane se ven
// en su tamano real, los que estan mas lejos se ven mas chicos
// (foreshortening). cameraZ se recalcula cada frame a partir del fov
// para que el cubo de adelante mantenga siempre el mismo tamano en
// pantalla sin importar cuanto abras el angulo -- es la misma formula
// que usa PGraphicsOpenGL internamente para su fov/cameraZ default.
//
// NOTA: a diferencia de Processing real, Matrix4x4.CreatePerspectiveFieldOfView
// (.NET) exige fieldOfView estrictamente > 0 y < PI -- por eso el fov se
// clampea con Constrain() antes de usarlo. Sin esto, apenas arranca el
// sketch (MouseX todavia en 0, antes del primer movimiento del mouse)
// fov da exactamente 0 y explota con ArgumentOutOfRangeException.
public class MySketch : Sketch
{
    private PGraphics _scene3d;

    public override void Setup()
    {
        Size(640, 360);
        _scene3d = CreateGraphics(Width, Height, RendererKind.Renderer3D);
    }

    public override void Draw()
    {
        _scene3d.BeginDraw();
        _scene3d.Lights();
        _scene3d.Background(0);

        float cameraY = Height / 2f;
        float fov = Constrain(MouseX / (float)Width * (PI / 2f), 0.01f, PI / 2f - 0.01f);
        float cameraZ = cameraY / Tan(fov / 2f);
        float aspect = (float)Width / Height;
        if (IsMousePressed)
            aspect = aspect / 2f;

        _scene3d.Perspective(fov, aspect, cameraZ / 10f, cameraZ * 10f);

        _scene3d.Translate(Width / 2f + 30, Height / 2f, 0);
        _scene3d.RotateX(-PI / 6f);
        _scene3d.RotateY(PI / 3f + MouseY / (float)Height * PI);
        _scene3d.Box(45);
        _scene3d.Translate(0, 0, -50);
        _scene3d.Box(30);

        _scene3d.EndDraw();

        Image(_scene3d.Get(), 0, 0);
    }
}
";

        private const string Coordinates3D =
@"// Demo de ModelX/Y/Z() + ScreenX/Y/Z() -- mismo patron de PGraphics
// offscreen con Renderer3D que los demas samples 3D. La primera mitad es
// el ejemplo oficial de Processing para modelX/Y/Z: un cubo se ubica con
// una serie de Translate()/RotateY()/RotateZ()/RotateX(), y ANTES de
// deshacer esas transformaciones con PopMatrix() se lee donde termino el
// origen local (0, 0, 0) en espacio mundo -- eso es exactamente lo que
// hacen ModelX/Y/Z(). Con las transformaciones ya deshechas, un
// Translate(x, y, z) directo a esa posicion ubica un segundo cubo en el
// mismo lugar exacto, sin repetir la cadena de rotaciones.
//
// La segunda mitad usa ScreenX/Y() sobre ese mismo punto para proyectarlo
// a coordenadas de PANTALLA y dibujar una etiqueta 2D (circulo + texto)
// pegada al cubo -- un caso de uso real: anclar UI 2D a un objeto 3D que
// se mueve, sin tener que reimplementar a mano la proyeccion camara +
// perspectiva.
public class MySketch : Sketch
{
    private PGraphics _scene3d;

    public override void Setup()
    {
        Size(800, 600);
        _scene3d = CreateGraphics(Width, Height, RendererKind.Renderer3D);
    }

    public override void Draw()
    {
        Background(15, 15, 20);

        _scene3d.BeginDraw();
        _scene3d.Lights();

        _scene3d.PushMatrix();
        _scene3d.Translate(Width / 2f, Height / 2f, -200);
        _scene3d.RotateY(1.0f);
        _scene3d.RotateZ(2.0f);
        _scene3d.RotateX(FrameCount / 100f);
        _scene3d.Translate(0, 150, 0);

        _scene3d.Fill(230, 230, 230);
        _scene3d.Box(50);

        // ANTES de PopMatrix(): donde quedo el origen local (0,0,0) en
        // espacio mundo (ModelX/Y/Z) y en espacio de pantalla (ScreenX/Y).
        float ax = _scene3d.ModelX(0, 0, 0);
        float ay = _scene3d.ModelY(0, 0, 0);
        float az = _scene3d.ModelZ(0, 0, 0);
        float sx = _scene3d.ScreenX(0, 0, 0);
        float sy = _scene3d.ScreenY(0, 0, 0);

        _scene3d.PopMatrix();

        // Transformaciones ya deshechas -- Translate(ax, ay, az) ubica el
        // cubo chico exactamente en el mismo lugar que el grande, prueba
        // de que ModelX/Y/Z() devolvieron la posicion correcta.
        _scene3d.PushMatrix();
        _scene3d.Translate(ax, ay, az);
        _scene3d.Fill(255, 70, 100);
        _scene3d.Box(18);
        _scene3d.PopMatrix();

        _scene3d.EndDraw();

        Image(_scene3d.Get(), 0, 0);

        // Etiqueta 2D anclada al cubo usando la posicion que ScreenX/Y()
        // proyecto -- se mueve solita cuadro a cuadro, sin tocar ninguna
        // matriz 3D desde aca.
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
@"// Demo de PShader -- Shader()/ResetShader()/LoadShader() -- mismo patron
// de PGraphics offscreen con Renderer3D que los demas samples 3D. Como
// LoadShader() lee un archivo GLSL de disco (igual que Processing real),
// y este sample tiene que ser un solo archivo autocontenido, el fragment
// shader se escribe a un archivo temporal en Setup() y se carga desde ahi
// -- en un sketch normal simplemente tendrias el .glsl como asset propio
// y llamarias LoadShader(""miShader.frag"") directo.
//
// El shader de ejemplo es un clasico ""debug de normales"": en vez de usar
// las luces o el Fill(), pinta cada pixel segun hacia donde mira su normal
// (vNormal * 0.5 + 0.5, para llevar el rango [-1,1] a un color [0,1]) --
// por eso cada cara de la esfera se ve de un color distinto y BIEN
// diferenciado de las luces normales, sirve para comprobar a simple vista
// que el shader custom esta realmente activo. Como reusa el vertex shader
// propio del motor (LoadShader(fragFilename), un solo argumento), no hace
// falta declarar el layout de atributos a mano -- ver los comentarios de
// PShader para el porque.
public class MySketch : Sketch
{
    private PGraphics _scene3d;
    private PShader _normalShader;
    private bool _useCustomShader = true;

    public override void Setup()
    {
        Size(800, 600);
        _scene3d = CreateGraphics(Width, Height, RendererKind.Renderer3D);

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
        _normalShader = _scene3d.LoadShader(tempPath);
    }

    public override void Draw()
    {
        Background(15, 15, 20);

        _scene3d.BeginDraw();
        _scene3d.Lights();

        if (_useCustomShader)
            _scene3d.Shader(_normalShader);
        else
            _scene3d.ResetShader();

        _scene3d.Fill(200, 200, 200);
        _scene3d.PushMatrix();
        _scene3d.Translate(Width / 2f, Height / 2f, 0);
        _scene3d.RotateY(FrameCount / 60f);
        _scene3d.RotateX(FrameCount / 90f);
        _scene3d.Sphere(180);
        _scene3d.PopMatrix();

        _scene3d.EndDraw();

        Image(_scene3d.Get(), 0, 0);

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
@"// Demo de normal(nx, ny, nz) -- a diferencia de los demas samples de esta
// lista, este NO tiene un efecto visual que mostrar todavia: la normal()
// real de Processing solo afecta vertices definidos DESPUES, dentro de
// beginShape()/vertex(), y ese API de formas 3D por vertice no existe
// todavia en este motor (Box()/Sphere() son las unicas primitivas 3D, y
// calculan sus propias normales a partir de la malla -- no hay vertices
// sueltos a los que normal() pueda aplicarles nada). Por ahora normal()
// solo GUARDA el valor que le pasas, para el dia que exista esa API.
//
// Este sample llama normal() igual, para dejar documentado en codigo como
// se usa la firma -- pero la esfera de abajo se ve identica la llames o
// no, y eso es exactamente lo esperado hoy.
public class MySketch : Sketch
{
    private PGraphics _scene3d;

    public override void Setup()
    {
        Size(800, 600);
        _scene3d = CreateGraphics(Width, Height, RendererKind.Renderer3D);
    }

    public override void Draw()
    {
        Background(15, 15, 20);

        _scene3d.BeginDraw();
        _scene3d.Lights();

        // Llamada real a la API -- no tira error, pero hoy no cambia nada
        // visible (ver el comentario de arriba y el de Normal() en
        // GraphicsContext.3D.cs).
        _scene3d.Normal(0, 0, 1);

        _scene3d.Fill(140, 200, 255);
        _scene3d.PushMatrix();
        _scene3d.Translate(Width / 2f, Height / 2f, 0);
        _scene3d.RotateY(FrameCount / 80f);
        _scene3d.Sphere(160);
        _scene3d.PopMatrix();

        _scene3d.EndDraw();

        Image(_scene3d.Get(), 0, 0);

        Fill(255);
        TextSize(13);
        Text(""normal(0, 0, 1) se llama arriba, pero todavia no tiene efecto visible -- hace falta beginShape()/vertex() en 3D para que tenga donde aplicarse."", 12, Height - 16);
    }
}
";
        private const string PresentationMode =
@"// Modo presentacion -- demo de FullScreen() + PixelWidth/PixelHeight. El
// dibujo en si (un caleidoscopio girando, HSB puro) no necesita nada
// especial: lo interesante es el HUD de abajo, que muestra los 4 numeros
// de entorno juntos -- Width/Height logicos, PixelWidth/PixelHeight reales
// (en este motor siempre iguales, ver el comentario de PixelWidth en
// Sketch.cs) y DisplayDensity().
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
// Panel de ventana -- demo de WindowMove()/WindowResizable()/WindowTitle()/
// WindowRatio(), todas expuestas como eventos que un host real (la ventana
// de la IDE, por ejemplo) puede escuchar para mover/redimensionar/titular
// la ventana de verdad. Este sample no depende de que ese cableado ya
// exista: cada tecla llama a la funcion igual, y el log en pantalla
// muestra el llamado y el estado resultante (IsFullScreen,
// IsWindowResizable, WindowTitleText) leido directamente del Sketch --
// eso funciona SIEMPRE, este o no conectado un host que reaccione.
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
@"// Tamano dinamico -- demo de Settings(). A diferencia de todos los demas
// samples de esta lista (que llaman Size() dentro de Setup()), este lo
// llama dentro de Settings() -- el hook que corre ANTES que Setup(), igual
// que en Processing real. La diferencia practica: para cuando Setup()
// arranca, Width/Height YA reflejan lo que decidio Settings(), sin
// depender del orden en que el host llame a los metodos del ciclo de vida.
// En C# puro esto no es estrictamente necesario (Size() funciona igual de
// bien llamado directo en Setup()), pero mantiene el mismo orden
// garantizado que esperan los sketches portados de Processing.
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
        // Width/Height ya estan fijados por Settings() -- no hace falta
        // llamar Size() de nuevo aca.
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
@"// Contador binario visual -- demo de Binary()/Unbinary(). Un valor de 0 a
// 255 se muestra como 8 celdas (bits): Binary((byte)valor) arma la cadena
// de 8 caracteres, y Unbinary() la vuelve a convertir en numero para
// demostrar que el viaje de ida y vuelta da el mismo valor.
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
@"// Busqueda en paralelo -- demo de Thread(). SearchPrimes() es un metodo
// sin parametros normal y corriente; Thread(nameof(SearchPrimes)) lo busca
// por reflexion y lo corre en un hilo aparte. Mientras tanto, Draw() sigue
// corriendo a su propio ritmo (el spinner no se traba), porque el trabajo
// pesado del bucle de primos vive en otro hilo por completo. El handoff de
// datos entre los dos hilos es deliberadamente simple -- solo un par de
// campos volatiles que un hilo escribe y el otro lee, sin locks.
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
 
    // Sin parametros -- exactamente lo que Thread() busca por reflexion.
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
@"// Exportar a PDF -- demo de BeginRaw()/EndRaw(). DrawPoster() es un metodo
// comun que dibuja la escena -- Draw() lo llama cada frame para mostrarla
// en pantalla, igual que cualquier sketch. La tecla V llama a ESE MISMO
// metodo una vez mas, pero encerrado entre BeginRaw('poster.pdf') y
// EndRaw() -- durante ese llamado extra, todo lo que DrawPoster() dibuja
// se redirige a la pagina PDF en vez de a pantalla (por eso la pantalla no
// 'parpadea' ni cambia en ese instante), y al llamar EndRaw() el archivo
// queda escrito con la misma escena, pero como vectores reales -- se puede
// abrir y hacer zoom infinito sin pixelarse, a diferencia de un
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
@"// Respaldo de trazos -- demo de SaveStream(). El trazo dibujado con el
// mouse se guarda primero como texto con SaveStrings() (una funcion que ya
// existia), y despues la tecla S abre ESE archivo con CreateInput() y lo
// copia entero a un backup con nombre unico usando SaveStream(path, input)
// -- una copia de archivo a archivo sin leerlo a mano linea por linea.
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
// Tarjeta de datos -- demo de ParseJSONObject()/ParseXML() + Launch(). El
// JSON y el XML se arman primero con la API normal (JSONObject/XML) y se
// serializan a texto con ToString() -- simulando datos que llegaron como
// STRING (una respuesta de red, un campo de texto pegado), no como
// archivo en disco. Despues ESE texto se vuelve a leer con
// ParseJSONObject()/ParseXML(), que es la funcion nueva. La tecla L abre
// el sitio guardado en el JSON con Launch(), en el navegador del sistema
// operativo.
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
// Gema facetada -- demo de BeginShape(Triangles) + Vertex(x,y,z) + Normal()
// bajo Renderer3D, la pieza que hasta ahora faltaba: normal() ya tenia
// donde guardarse pero ningun vertice que la usara. Un octaedro (8 caras
// triangulares) se arma a mano, cara por cara -- cada cara llama Normal()
// ANTES de sus 3 Vertex(), y esa normal es la que queda pegada a esos 3
// vertices quando EndShape() sube la malla.
//
// Sombreado PLANO: la normal de cada cara sale de PVector.Cross() entre
// dos de sus aristas (el metodo de PVector que agrega el cross product en
// 3D) -- por eso cada cara se ve como una faceta solida, con un borde
// marcado contra la de al lado, igual que una gema real tallada.
// Sombreado SUAVE: en un octaedro regular centrado en el origen, la normal
// 'promedio' de cada vertice es sencillamente su propia direccion desde el
// centro (normalizada) -- asi que ni siquiera hace falta promediar caras a
// mano, y las facetas se funden en una superficie que se ve redondeada.
public class MySketch : Sketch
{
    private PGraphics _scene3d;
    private float _qw = 1f, _qx, _qy, _qz;
    private float _spinAxisX = 1f, _spinAxisY, _spinAxisZ, _spinAngle;
    private bool _smooth;
    private float _radius = 150;
 
    public override void Setup()
    {
        Size(700, 500);
        _scene3d = CreateGraphics(Width, Height, RendererKind.Renderer3D);
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
 
        _scene3d.BeginDraw();
        _scene3d.Lights();
        _scene3d.Fill(160, 220, 255);
        _scene3d.PushMatrix();
        _scene3d.Translate(Width / 2f, Height / 2f, 0);
 
        float angle = 2f * Acos(Constrain(_qw, -1f, 1f));
        float s = Sqrt(Max(1f - _qw * _qw, 0f));
        if (s < 0.0001f)
            _scene3d.Rotate(angle, 1, 0, 0);
        else
            _scene3d.Rotate(angle, _qx / s, _qy / s, _qz / s);
 
        DrawGem(_scene3d, _radius, _smooth);
 
        _scene3d.PopMatrix();
        _scene3d.EndDraw();
 
        Image(_scene3d.Get(), 0, 0);
 
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
 
    private void DrawGem(PGraphics g, float r, bool smooth)
    {
        var points = new PVector[]
        {
            new PVector(0, -r, 0),   // 0: arriba
            new PVector(0,  r, 0),   // 1: abajo
            new PVector( r, 0,  0),  // 2: ecuador +X
            new PVector( 0, 0,  r),  // 3: ecuador +Z
            new PVector(-r, 0,  0),  // 4: ecuador -X
            new PVector( 0, 0, -r),  // 5: ecuador -Z
        };
 
        var smoothNormals = new PVector[points.Length];
        for (int i = 0; i < points.Length; i++)
            smoothNormals[i] = points[i].Copy().Normalize();
 
        int[,] faces =
        {
            { 0, 2, 3 }, { 0, 3, 4 }, { 0, 4, 5 }, { 0, 5, 2 },
            { 1, 3, 2 }, { 1, 4, 3 }, { 1, 5, 4 }, { 1, 2, 5 },
        };
 
        g.BeginShape(ShapeKind.Triangles);
        for (int f = 0; f < faces.GetLength(0); f++)
        {
            int ia = faces[f, 0], ib = faces[f, 1], ic = faces[f, 2];
            var a = points[ia];
            var b = points[ib];
            var c = points[ic];
 
            if (smooth)
            {
                g.Normal(smoothNormals[ia].X, smoothNormals[ia].Y, smoothNormals[ia].Z);
                g.Vertex(a.X, a.Y, a.Z);
                g.Normal(smoothNormals[ib].X, smoothNormals[ib].Y, smoothNormals[ib].Z);
                g.Vertex(b.X, b.Y, b.Z);
                g.Normal(smoothNormals[ic].X, smoothNormals[ic].Y, smoothNormals[ic].Z);
                g.Vertex(c.X, c.Y, c.Z);
            }
            else
            {
                var faceNormal = PVector.Cross(PVector.Sub(b, a), PVector.Sub(c, a)).Normalize();
                g.Normal(faceNormal.X, faceNormal.Y, faceNormal.Z);
                g.Vertex(a.X, a.Y, a.Z);
                g.Vertex(b.X, b.Y, b.Z);
                g.Vertex(c.X, c.Y, c.Z);
            }
        }
        g.EndShape();
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
// Particulas 3D -- demo de PVector con Z. Antes PVector solo tenia X e Y;
// ahora Add()/Sub()/Mult()/Normalize()/etc. operan en las tres dimensiones,
// asi que un sistema de particulas con posicion/velocidad/gravedad reales
// en 3D se escribe exactamente igual que uno 2D -- sin tener que manejar
// un tercer float 'z' suelto a mano por separado.
public class MySketch : Sketch
{
    private PGraphics _scene3d;
    private readonly List<PVector> _pos = new List<PVector>();
    private readonly List<PVector> _vel = new List<PVector>();
    private const float BoxHalf = 200;
 
    public override void Setup()
    {
        Size(700, 500);
        _scene3d = CreateGraphics(Width, Height, RendererKind.Renderer3D);
        for (int i = 0; i < 40; i++)
            SpawnParticle();
    }
 
    public override void Draw()
    {
        Background(12, 14, 20);
 
        _scene3d.BeginDraw();
        _scene3d.Lights();
        _scene3d.PushMatrix();
        _scene3d.Translate(Width / 2f, Height / 2f, 0);
        _scene3d.RotateY(FrameCount / 200f); // gira la escena entera para que se note la profundidad
 
        var gravity = new PVector(0, 0.15f, 0);
 
        for (int i = 0; i < _pos.Count; i++)
        {
            var p = _pos[i];
            var v = _vel[i];
 
            v.Add(gravity);
            p.Add(v);
 
            // Rebote elastico contra las 6 paredes de un cubo invisible --
            // cada eje se revisa por separado, pero es el MISMO PVector en
            // los tres casos gracias al soporte nuevo de Z.
            if (Abs(p.X) > BoxHalf) { p.X = Constrain(p.X, -BoxHalf, BoxHalf); v.X *= -0.8f; }
            if (Abs(p.Y) > BoxHalf) { p.Y = Constrain(p.Y, -BoxHalf, BoxHalf); v.Y *= -0.8f; }
            if (Abs(p.Z) > BoxHalf) { p.Z = Constrain(p.Z, -BoxHalf, BoxHalf); v.Z *= -0.8f; }
 
            _pos[i] = p;
            _vel[i] = v;
 
            _scene3d.PushMatrix();
            _scene3d.Translate(p.X, p.Y, p.Z);
            _scene3d.FillHSB(Map(p.Y, -BoxHalf, BoxHalf, 0, 300), 70, 90);
            _scene3d.Sphere(10);
            _scene3d.PopMatrix();
        }
 
        _scene3d.PopMatrix();
        _scene3d.EndDraw();
 
        Image(_scene3d.Get(), 0, 0);
 
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
using System.Collection.Generic;
// Enjambre reutilizable -- demo de CreateShape3D(). La geometria de la
// gema (el mismo octaedro facetado del sample anterior) se arma UNA sola
// vez en Setup() -- CreateShape3D() graba los mismos Vertex()/Normal() que
// usariamos con BeginShape()/EndShape(), pero en vez de dibujar
// inmediatamente, sube la malla a un VAO/VBO persistente y devuelve un
// PShape. De ahi en mas, cada gema del enjambre se dibuja con un simple
// Shape() -- sin volver a triangular en la CPU ni volver a subir datos a
// la GPU en cada frame, que es exactamente lo que SI hacia (a proposito,
// para mostrar el contraste) el sample de la 'Gema facetada'.
public class MySketch : Sketch
{
    private PGraphics _scene3d;
    private PShape _gemShape;
    private readonly List<(float x, float y, float z, float spin, float angle)> _gems =
        new List<(float, float, float, float, float)>();
 
    public override void Setup()
    {
        Size(800, 600);
        _scene3d = CreateGraphics(Width, Height, RendererKind.Renderer3D);
 
        // CreateShape3D() reclama y libera el contexto de GL por su cuenta
        // -- se puede llamar suelto, una sola vez, sin BeginDraw()/EndDraw().
        _gemShape = _scene3d.CreateShape3D(ShapeKind.Triangles, () => BuildGemVertices(_scene3d, 22));
 
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
 
        _scene3d.BeginDraw();
        _scene3d.Lights();
        _scene3d.Fill(180, 220, 255);
        _scene3d.PushMatrix();
        _scene3d.Translate(Width / 2f, Height / 2f, 0);
        _scene3d.RotateY(FrameCount / 300f);
 
        for (int i = 0; i < _gems.Count; i++)
        {
            var g = _gems[i];
            float angle = g.angle + FrameCount * 0.01f * g.spin;
 
            _scene3d.PushMatrix();
            _scene3d.Translate(g.x, g.y, g.z);
            _scene3d.RotateY(angle);
            _scene3d.RotateX(angle * 0.6f);
            _scene3d.Shape(_gemShape, 0, 0);
            _scene3d.PopMatrix();
        }
 
        _scene3d.PopMatrix();
        _scene3d.EndDraw();
 
        Image(_scene3d.Get(), 0, 0);
 
        Fill(255);
        TextSize(13);
        Text($""CreateShape3D() x1 (subida una vez) -- Shape() x{_gems.Count} por frame, sin re-triangular ni re-subir nada"", 12, Height - 16);
    }
 
    private void BuildGemVertices(PGraphics g, float r)
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
            g.Normal(normal.X, normal.Y, normal.Z);
            g.Vertex(a.X, a.Y, a.Z);
            g.Vertex(b.X, b.Y, b.Z);
            g.Vertex(c.X, c.Y, c.Z);
        }
    }
}
";
        private const string TexturedBillboard =
@"// Cartel texturizado -- demo de Vertex(x,y,z,u,v) + Texture() bajo
// Renderer3D. La textura no viene de un archivo: se genera dibujando un
// patron con la API 2D de siempre sobre un PGraphics chico, y Get() lo
// convierte en un PImage comun -- Texture()/Vertex(...,u,v) no distinguen
// entre una imagen generada en codigo y una cargada con LoadImage(), asi
// que ambas funcionan igual.
//
// El panel es un solo Quad con las 4 esquinas de la textura (u,v) de
// (0,0) a (1,1) -- CreateShape3D() lo sube a la GPU una sola vez en
// Setup(), y despues cada frame solo se dibuja con Shape().
public class MySketch : Sketch
{
    private PGraphics _scene3d;
    private PImage _texture;
    private PShape _panel;
 
    private float _qw = 1f, _qx, _qy, _qz;
    private float _spinAxisX, _spinAxisY = 1f, _spinAxisZ, _spinAngle;
 
    public override void Setup()
    {
        Size(700, 500);
        _scene3d = CreateGraphics(Width, Height, RendererKind.Renderer3D);
 
        _texture = BuildCheckerTexture();
 
        // OJO: BeginShape() (llamado por CreateShape3D() internamente)
        // resetea _shapeTexture a null -- por eso Texture() va DENTRO del
        // callback, como primera línea, en vez de antes de CreateShape3D().
        _panel = _scene3d.CreateShape3D(ShapeKind.Quads, () =>
        {
            _scene3d.Texture(_texture);
 
            float s = 180;
            _scene3d.Normal(0, 0, 1);
            _scene3d.Vertex(-s, -s, 0, 0, 0);
            _scene3d.Vertex(s, -s, 0, 1, 0);
            _scene3d.Vertex(s, s, 0, 1, 1);
            _scene3d.Vertex(-s, s, 0, 0, 1);
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
 
        _scene3d.BeginDraw();
        _scene3d.Lights();
        _scene3d.Fill(255);
        _scene3d.PushMatrix();
        _scene3d.Translate(Width / 2f, Height / 2f, 0);
 
        float angle = 2f * Acos(Constrain(_qw, -1f, 1f));
        float s = Sqrt(Max(1f - _qw * _qw, 0f));
        if (s < 0.0001f)
            _scene3d.Rotate(angle, 1, 0, 0);
        else
            _scene3d.Rotate(angle, _qx / s, _qy / s, _qz / s);
 
        _scene3d.Shape(_panel, 0, 0);
 
        _scene3d.PopMatrix();
        _scene3d.EndDraw();
 
        Image(_scene3d.Get(), 0, 0);
 
        Fill(255);
        TextSize(13);
        Text(""CreateShape3D(Quads) + Vertex(x,y,z,u,v) + Texture() -- arrastrá para rotar"", 12, Height - 16);
    }
 
    // Genera un tablero de 8x8 en un PGraphics 2D chico y lo devuelve como
    // PImage -- exactamente la misma API que ya usarías para dibujar en
    // pantalla (Rect/Fill), solo que acá el resultado se guarda como
    // textura en vez de mostrarse directo.
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
    }
}