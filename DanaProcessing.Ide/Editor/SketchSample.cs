using Avalonia.Animation;
using Avalonia.Markup.Xaml.MarkupExtensions;
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

            // TODO samples pendientes -- agregar cuando el feature
            // correspondiente esté listo en Renderer3DBackend.cs (ver su
            // lista "NOT YET BUILT" al final del archivo):
            // - beginCamera()/endCamera()
            // - normal() + shapes 3D custom (beginShape()/vertex() en 3D)
            // - PShader / shaders custom
        };

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
@"// Demo del backend 3D (Silk.NET/OpenGL) -- Sketch.Size(w, h, Renderer3D)
// todavia no esta cableado (ver GraphicsContext.3D.cs), asi que la forma
// de probar el pipeline hoy es un PGraphics offscreen con Renderer3D,
// creado UNA sola vez en Setup() y reusado cada frame -- Renderer3DBackend
// .Create() levanta una ventana Silk.NET oculta + contexto OpenGL + shader
// + mesh del cubo, algo demasiado caro para repetir por frame.
//
// A diferencia de la version original (que solo giraba solo), este cubo
// tiene INERCIA de verdad: mientras arrastras el mouse, la rotacion sigue
// el delta de movimiento (MouseX/Y - PMouseX/Y) 1:1; al soltar, esa misma
// velocidad angular queda guardada y sigue girando, con una friccion leve
// (*0.99 por frame) que la va apagando de a poco -- no es un truco visual,
// es la misma matriz de rotacion acumulandose frame a frame.
public class MySketch : Sketch
{
    private PGraphics _scene3d;
    private float _rotY, _rotX;
    private float _velY = 0.02f, _velX = 0.012f;
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
            _velY = (MouseX - PMouseX) * 0.01f;
            // Negado: el mundo 3D de este motor es Y-abajo (ver la nota
            // COORDINATE CONVENTION en Renderer3DBackend.cs), asi que sin este
            // signo arrastrar hacia abajo giraba el objeto al reves de lo
            // intuitivo -- este signo lo deja como si agarraras la forma.
            _velX = -(MouseY - PMouseY) * 0.01f;
        }
    _rotY += _velY;
        _rotX += _velX;
        _velY *= 0.99f;
        _velX *= 0.99f;

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
        _scene3d.RotateY(_rotY);
        _scene3d.RotateX(_rotX);
        _scene3d.Box(_boxSize);
        _scene3d.PopMatrix();
        _scene3d.EndDraw();

        Image(_scene3d.Get(), 0, 0);

   Fill(255);
TextSize(13);
string lightsStatus = _lit ? ""ON"" : ""OFF"";
Text($""Arrastra para rotar (con inercia) -- rueda escala ({(int)_boxSize}px) -- click cambia color -- tecla L: luces {lightsStatus}"", 12, Height - 16);
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
// (0-360 grados), arrastrar el mouse rota la esfera con la misma inercia
// que Box3D, la rueda cambia el radio en vivo, y el click prende/apaga
// las luces para comparar el shading contra el flat-color de Processing.
public class MySketch : Sketch
{
    private PGraphics _scene3d;
    private float _rotY, _rotX;
    private float _velY = 0.015f, _velX = 0.01f;
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
            _velY = (MouseX - PMouseX) * 0.01f;
            // Negado: el mundo 3D de este motor es Y-abajo (ver la nota
            // COORDINATE CONVENTION en Renderer3DBackend.cs), asi que sin este
            // signo arrastrar hacia abajo giraba el objeto al reves de lo
            // intuitivo -- este signo lo deja como si agarraras la forma.
            _velX = -(MouseY - PMouseY) * 0.01f;
        }
        _rotY += _velY;
_rotX += _velX;
_velY *= 0.98f;
_velX *= 0.98f;

_scene3d.BeginDraw();
if (_lit)
    _scene3d.Lights();
_scene3d.SphereDetail(detail);
_scene3d.FillHSB(hue, 65, 95);
_scene3d.PushMatrix();
_scene3d.Translate(Width / 2f, Height / 2f, 0);
_scene3d.RotateY(_rotY);
_scene3d.RotateX(_rotX);
_scene3d.Sphere(_radius);
_scene3d.PopMatrix();
_scene3d.EndDraw();

Image(_scene3d.Get(), 0, 0);

Fill(255);
string lightsStatus = _lit ? ""ON"" : ""OFF"";
Text($""SphereDetail({detail}) por mouse X -- color por mouse Y -- arrastra para rotar -- rueda escala ({(int)_radius}) -- click: luces {lightsStatus}"", 12, Height - 16);
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
    }
}