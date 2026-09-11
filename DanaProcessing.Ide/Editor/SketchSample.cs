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
    }
}