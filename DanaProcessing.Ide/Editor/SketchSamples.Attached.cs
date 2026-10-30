using DanaProcessing.Ide.Localization;

namespace DanaProcessing.Ide.Editor
{
    /// <summary>
    /// Samples for attached sketches (AttachedSketch): sketches with no window
    /// of their own that draw on another app's window (behind Windows Terminal's text). Listed after the
    /// live ones (see Extras in SketchSamples.Extras.cs).
    /// </summary>
    public static partial class SketchSamples
    {
        private static readonly SketchSample[] Attached =
        {
            new SketchSample(
                Loc.Tr("Lluvia sobre la terminal (sketch adjunto)", "Rain on the terminal (attached sketch)"),
                Loc.Tr("Un AttachedSketch no tiene ventana propia: dibuja sobre otra app, aquí Windows Terminal, siempre detrás de su texto (se ve a través de un perfil translúcido, con \"opacity\" por debajo de 100). Ábrela y pulsa Run: llueve sobre ella. Muévela o cámbiale el tamaño y la lluvia la sigue (Width/Height cambian y WindowResized() avisa). Los clics pasan a la terminal, así que puedes seguir escribiendo, y el cursor hace de paraguas. ■ Stop quita la lluvia. Solo Windows por ahora.",
                       "An AttachedSketch has no window of its own: it draws on another app, here Windows Terminal, always behind its text (it shows through a see-through profile, with \"opacity\" below 100). Open it and press Run: it rains on it. Move it or resize it and the rain follows (Width/Height change and WindowResized() tells you). Clicks go through to the terminal, so you can keep typing, and the cursor works as an umbrella. ■ Stop removes the rain. Windows only for now."),
                TerminalRain,
                new[] { SampleTag.Window, SampleTag.Animation, SampleTag.Particles }),

            new SketchSample(
                Loc.Tr("Agua en la terminal: inercia con TargetAcceleration", "Water in the terminal: inertia with TargetAcceleration"),
                Loc.Tr("El agua vive dentro de Windows Terminal y siente cómo la mueves: arrástrala y suéltala de golpe y el agua choca contra la pared; sacúdela y hace olas; un tirón hacia abajo la hace saltar. Es -TargetAcceleration como fuerza, igual que un vaso de agua en un coche que frena. Cada tecla que escribes en la terminal deja caer una gota y Enter salpica (ListenToTargetKeys); un clic deja caer una gota donde haces clic (ListenToTargetMouse). Si cambias a otra ventana, el agua se atenúa (IsTargetFocused). Solo Windows por ahora.",
                       "The water lives inside Windows Terminal and feels how you move it: drag it and let go suddenly and the water hits the wall; shake it and it makes waves; a yank downwards makes it jump. It's -TargetAcceleration as a force, like a glass of water in a car that brakes. Every key you type in the terminal drops a drop and Enter splashes (ListenToTargetKeys); a click drops one where you click (ListenToTargetMouse). Switch to another window and the water dims (IsTargetFocused). Windows only for now."),
                TerminalWater,
                new[] { SampleTag.Window, SampleTag.Animation, SampleTag.Interaction, SampleTag.Particles }),

            new SketchSample(
                Loc.Tr("Burbujas del teclado: lo que escribes en la terminal", "Keyboard bubbles: what you type in the terminal"),
                Loc.Tr("Cada tecla que escribes en Windows Terminal sube como una burbuja con su letra (KeyTyped con ListenToTargetKeys). Enter las revienta todas en chispas, Backspace revienta la última y sacudir la ventana las desparrama. Un clic sobre la terminal lanza un anillo de chispas sin quitarle el clic a la terminal (ListenToTargetMouse), y minimizarla y restaurarla dispara TargetHidden()/TargetShown(). Solo se leen teclas mientras la terminal está al frente. Solo Windows por ahora.",
                       "Every key you type in Windows Terminal floats up as a bubble with its letter (KeyTyped with ListenToTargetKeys). Enter pops them all into sparks, Backspace pops the newest one, and shaking the window scatters them. A click over the terminal fires a ring of sparks without taking the click from the terminal (ListenToTargetMouse), and minimizing and restoring it fires TargetHidden()/TargetShown(). Keys are only read while the terminal is in front. Windows only for now."),
                TerminalKeys,
                new[] { SampleTag.Window, SampleTag.Interaction, SampleTag.Text, SampleTag.Particles }),

            new SketchSample(
                Loc.Tr("Campo vectorial en la terminal: arrastre y errores", "Flow field in the terminal: dragging and errors"),
                Loc.Tr("Partículas que siguen un campo vectorial bajo el texto de Windows Terminal, un campo distinto en cada ventana (AttachMode.EachWindow). Al arrastrar la terminal las partículas se quedan atrás (inercia) y el campo gira con la velocidad; un comando que falla lo convierte en una tormenta roja con el código de salida, y uno que funciona lo calma con un pulso cian (ListenToShell y CommandFinished). Necesita una vez el snippet de PowerShell: ShellSignals.InstallForPowerShell(). Idea de \"Vector field with particles\" de jcponcemath en OpenProcessing, escrito de cero para Dana. Solo Windows por ahora.",
                       "Particles following a vector field behind Windows Terminal's text, a different field in each window (AttachMode.EachWindow). Drag the terminal and the particles lag behind (inertia) while the field turns with the speed; a command that fails turns it into a red storm showing the exit code, and one that works calms it with a cyan pulse (ListenToShell and CommandFinished). Needs the PowerShell snippet once: ShellSignals.InstallForPowerShell(). Idea from \"Vector field with particles\" by jcponcemath on OpenProcessing, written from scratch for Dana. Windows only for now."),
                TerminalFlowField,
                new[] { SampleTag.Window, SampleTag.Particles, SampleTag.Generative, SampleTag.Animation }),

            new SketchSample(
                Loc.Tr("Tela sobre toda la terminal: Verlet e inercia", "Cloth over the whole terminal: Verlet and inertia"),
                Loc.Tr("Una tela translúcida cubre toda la ventana de Windows Terminal, colgada de su borde de arriba. Al arrastrar la ventana se queda atrás, se balancea y hace pliegues (-TargetAcceleration); una brisa suave la mueve siempre un poco. El cursor la empuja, un clic le corta un agujero (ListenToTargetMouse, el clic llega igual a la terminal) y estirarla de más la rompe. Un comando que falla la rasga y la tiñe de rojo; uno que funciona le manda una ráfaga de viento; dana-signal reset cuelga una nueva. Se dibuja como malla sombreada por pliegues (sin antialias: rápido y sin costuras) y una trama de hairlines. Idea de \"Cloth!!\" de Millie en OpenProcessing (código oculto), escrito de cero para Dana. Solo Windows por ahora.",
                       "A see-through sheet of cloth covers the whole Windows Terminal window, hanging from its top edge. Drag the window and it lags behind, swings and folds (-TargetAcceleration); a light breeze always moves it a little. The cursor pushes it, a click cuts a hole (ListenToTargetMouse, the click still reaches the terminal) and stretching it too far tears it. A failed command rips it and tints it red; one that works sends a gust of wind through it; dana-signal reset hangs a new one. Drawn as a mesh shaded by its folds (no antialiasing: fast and seamless) plus a weave of hairlines. Idea from \"Cloth!!\" by Millie on OpenProcessing (code hidden), written from scratch for Dana. Windows only for now."),
                TerminalCloth,
                new[] { SampleTag.Window, SampleTag.Animation, SampleTag.Interaction }),

            new SketchSample(
                Loc.Tr("Blobs en la terminal: metaballs y marching squares", "Blobs in the terminal: metaballs and marching squares"),
                Loc.Tr("Blobs blandos flotan dentro de Windows Terminal como en una lámpara de lava: se juntan, se funden y de vez en cuando sueltan una gotita que se aleja y vuelve. Arrastrar o sacudir la ventana los lanza al otro lado (inercia) y rebotan en los bordes. Cada tecla suelta una gota pequeña que nada hasta los demás y se une (ListenToTargetKeys); Backspace quita una; el cursor los atrae. Un comando que falla los revienta en gotas rojas que salen volando y, al rato, se buscan y se vuelven a unir; uno que funciona los hace latir. El relleno sale de marching squares: cada celda aporta su trozo exacto de blob, todos en una sola malla sombreada suavemente por el campo, con un brillo suave encima. Idea de \"Blobules\" en OpenProcessing (código oculto), escrito de cero para Dana. Solo Windows por ahora.",
                       "Soft blobs float inside Windows Terminal like a lava lamp: they gather, melt together and now and then let a droplet go that drifts off and comes back. Dragging or shaking the window throws them the other way (inertia) and they bounce off the edges. Every key drops a small blob that swims to the others and merges (ListenToTargetKeys); Backspace removes one; the cursor pulls them. A failed command bursts them into red droplets that fly apart and, after a moment, find each other and merge again; one that works makes them pulse. The fill comes from marching squares: every cell gives its exact piece of blob, all in one mesh shaded smoothly by the field, with a soft glossy highlight on top. Idea from \"Blobules\" on OpenProcessing (code hidden), written from scratch for Dana. Windows only for now."),
                TerminalBlobs,
                new[] { SampleTag.Window, SampleTag.Generative, SampleTag.Interaction }),

            new SketchSample(
                Loc.Tr("Funciones ondulantes bajo el texto de la terminal", "Wobbly functions under the terminal's text"),
                Loc.Tr("Cintas de colores en el fondo de cada ventana de Windows Terminal (AttachMode.EachWindow). Cada línea es una función ondulante (suma de senos) y a la vez una cuerda: las ondas viajan por ella. Arrastrar la terminal agita las capas, cada una un poco más tarde que la de arriba, y arrastrar de lado desliza las ondas; cada tecla pulsa las cuerdas (Enter fuerte); el cursor las aparta como un dedo en el agua. Un comando que falla las vuelve una maraña roja; uno que funciona manda una onda de izquierda a derecha. Idea de \"Class 8/31\" de Tyler Alexander en OpenProcessing (Genuary 2024, día de la función ondulante; código oculto), escrito de cero para Dana. Solo Windows por ahora.",
                       "Colored ribbons along the bottom of every Windows Terminal window (AttachMode.EachWindow). Each line is a wobbly function (a sum of sines) and also a string: waves travel along it. Dragging the terminal shakes the layers, each a little later than the one above, and dragging sideways slides the waves; every key plucks the strings (Enter hard); the cursor pushes them aside like a finger in water. A failed command turns them into a red tangle; one that works sends a wave from left to right. Idea from \"Class 8/31\" by Tyler Alexander on OpenProcessing (Genuary 2024, wobbly function day; code hidden), written from scratch for Dana. Windows only for now."),
                TerminalWobble,
                new[] { SampleTag.Window, SampleTag.Animation, SampleTag.Generative }),
            new SketchSample(
                Loc.Tr("Blob CSS en la terminal: border-radius animado", "CSS blob in the terminal: animated border-radius"),
                Loc.Tr("El blob clásico de CSS (border-radius con ocho valores animado entre keyframes, degradado que gira, brillo suave) flotando detrás del texto de Windows Terminal. Cada esquina es un cuarto de elipse dibujado con BezierVertex, con la misma regla de CSS cuando los radios no caben. Al arrastrar la ventana se queda atrás, se estira en la dirección del movimiento y cambia de forma más rápido; se inclina hacia el cursor, late con cada tecla (ListenToTargetKeys), se pone rojo y tiembla con un comando que falla y se infla en verde-azul con uno que funciona. Inspirado en \"Create Smooth Blob Animation with CSS\" de Code With Nazia (YouTube), escrito de cero para Dana. Solo Windows por ahora.",
                       "The classic CSS blob (an eight-value border-radius animated between keyframes, a turning gradient, a soft glow) floating behind Windows Terminal's text. Each corner is a quarter ellipse drawn with BezierVertex, with CSS's rule for radii that don't fit. Drag the window and it trails behind, stretches along the motion and morphs faster; it leans towards the cursor, pulses with every key (ListenToTargetKeys), turns red and quivers when a command fails and swells green-blue when one works. Inspired by \"Create Smooth Blob Animation with CSS\" by Code With Nazia (YouTube), written from scratch for Dana. Windows only for now."),
                TerminalMorphBlob,
                new[] { SampleTag.Window, SampleTag.Animation, SampleTag.Interaction }),

            new SketchSample(
                Loc.Tr("Audio reactivo en la terminal: FFT en vivo (NAudio)", "Audio-reactive terminal: live FFT (NAudio)"),
                Loc.Tr("El sample de audio reactivo llevado a Windows Terminal: lo que esté sonando en el equipo (WasapiLoopbackCapture, sin micrófono) pasa por una FFT con bandas logarítmicas y mueve 40 blobs brillantes sobre tallos a lo largo del borde de abajo, graves a la izquierda y agudos a la derecha. En silencio se sueltan y suben flotando; cuando vuelve la música regresan a sus tallos. Arrastrar la terminal los balancea (inercia); un comando que falla los lanza hacia arriba en rojo y uno que funciona manda una ola de luz por la fila. La captura empieza en Setup() y se detiene en OnStop(). Usa // nuget: NAudio.",
                       "The audio-reactive sample moved onto Windows Terminal: whatever the computer is playing (WasapiLoopbackCapture, no microphone) goes through an FFT with log-spaced bands and moves 40 glowing blobs on stalks along the bottom edge, lows on the left and highs on the right. In silence they come loose and float up; when the music comes back they return to their stalks. Dragging the terminal swings them (inertia); a failed command flings them up in red and one that works sends a wave of light along the row. Capture starts in Setup() and stops in OnStop(). Uses // nuget: NAudio."),
                TerminalAudio,
                new[] { SampleTag.Window, SampleTag.Audio, SampleTag.NuGet }),
        };

        private const string TerminalRain =
@"// Rain on the terminal: an AttachedSketch has no window of its own,
// it draws on another app -- here, Windows Terminal, always behind its
// text: it shows through a see-through profile (""opacity"" below 100).
//
// 1. Open Windows Terminal and press Run: it rains on it.
// 2. Move it, resize it, maximize it: the sketch follows
//    (Width/Height change and WindowResized() tells you).
// 3. Clicks go through to the terminal: keep typing.
//    The mouse cursor is an umbrella. ■ Stop removes the rain.
//
// Windows only for now. Clear() at the start of Draw() keeps the terminal visible.
public class TerminalRain : AttachedSketch
{
    private readonly List<Drop> _drops = new();
    private readonly List<Ripple> _ripples = new();

    private sealed class Drop { public float X, Y, Speed, Length; }
    private sealed class Ripple { public float X, Y, R, Life = 1; }

    public override void Settings()
    {
        // Runs before the window is looked for, so Width/Height in Setup()
        // are already the terminal's.
        Attach(WindowTarget.Terminal);
    }

    public override void Setup()
    {
        FitDrops();
    }

    public override void WindowResized()
    {
        FitDrops(); // wider window, more rain
    }

    private void FitDrops()
    {
        int wanted = Math.Max(10, Width / 6);
        while (_drops.Count < wanted)
            _drops.Add(NewDrop(Random(Height)));
        if (_drops.Count > wanted)
            _drops.RemoveRange(wanted, _drops.Count - wanted);
    }

    private Drop NewDrop(float y) => new Drop
    {
        X = Random(Width),
        Y = y,
        Speed = Random(4, 9),
        Length = Random(8, 18),
    };

    public override void Draw()
    {
        Clear(); // transparent: the terminal shows through

        StrokeWeight(1.5f);
        Stroke(140, 200, 255, 150);
        foreach (var d in _drops)
        {
            Line(d.X, d.Y, d.X - 1, d.Y + d.Length);
            d.Y += d.Speed;

            // MouseX/MouseY follow the cursor even though clicks go through.
            bool umbrella = Math.Abs(d.X - MouseX) < 40 && d.Y + d.Length > MouseY - 6 && d.Y < MouseY;
            if (umbrella || d.Y > Height)
            {
                _ripples.Add(new Ripple { X = d.X, Y = umbrella ? MouseY - 6 : Height - 4 });
                d.X = Random(Width);
                d.Y = -Random(60);
            }
        }

        NoFill();
        for (int i = _ripples.Count - 1; i >= 0; i--)
        {
            var r = _ripples[i];
            Stroke(140, 200, 255, 170 * r.Life);
            Ellipse(r.X, r.Y, r.R * 2, r.R * 0.6f);
            r.R += 0.8f;
            r.Life -= 0.05f;
            if (r.Life <= 0)
                _ripples.RemoveAt(i);
        }
    }
}
";

        private const string TerminalWater =
@"// Water in the terminal: drag Windows Terminal around and the water inside
// sloshes, hits the walls and splashes -- the same trick as a glass of
// water in a moving car: what's inside feels the opposite of
// TargetAcceleration.
//
// 1. Open Windows Terminal and press Run.
// 2. Drag the window and let go suddenly. Shake it. Yank it down: the water jumps.
// 3. Type in the terminal: every key drops a drop, Enter makes a splash.
//    Click the water to drop one where you click.
// 4. Switch to another window: the water dims until you come back.
//
// ListenToTargetKeys()/ListenToTargetMouse() read keys typed into the terminal
// and clicks over it (the terminal still gets all of them). Windows only.
public class TerminalWater : AttachedSketch
{
    const float Cell = 8;          // width of one water column, in pixels
    const float Gravity = 900;     // px/s²
    const float Inertia = 0.28f;   // how strongly the window's motion pushes the water
    const float FillLevel = 0.3f;  // how full the window is

    float[] _h = new float[0];     // water height per column
    float[] _u = new float[0];     // flow between columns
    float[] _flux = new float[0];
    int _cols;
    float _dim = 1;
    readonly List<PVector> _drops = new();
    readonly List<PVector> _dropSpeeds = new();

    public override void Settings() => Attach(WindowTarget.Terminal);

    public override void Setup()
    {
        ListenToTargetKeys();
        ListenToTargetMouse();
        Rebuild();
    }

    public override void WindowResized() => Rebuild();

    void Rebuild()
    {
        _cols = Math.Max(8, (int)(Width / Cell));
        _h = new float[_cols];
        _u = new float[_cols + 1];
        _flux = new float[_cols + 1];
        for (int i = 0; i < _cols; i++)
            _h[i] = Height * FillLevel;
    }

    // --- input from the terminal -------------------------------------------

    public override void KeyTyped() => Drop(Random(Width), 1);

    public override void KeyPressed()
    {
        if (Key == ENTER)
            for (int i = 0; i < 6; i++)
                Drop(Random(Width), 2);
    }

    public override void MousePressed() => Drop(MouseX, 1.5f);

    public override void TargetShown() => Splash(_cols / 2, 60); // welcome back

    void Drop(float x, float size)
    {
        _drops.Add(new PVector(x, -10, size));
        _dropSpeeds.Add(new PVector(Random(-20, 20), Random(0, 80)));
    }

    void Splash(int col, float amount)
    {
        if (col < 1 || col >= _cols - 1) return;
        float take = Math.Min(amount, _h[col] * 0.5f);
        _h[col] -= take;               // a dent in the surface...
        _h[col - 1] += take / 2;       // ...pushes water to both sides: a wave
        _h[col + 1] += take / 2;
    }

    // --- simulation -----------------------------------------------------------

    public override void Draw()
    {
        Clear();
        float dt = DeltaTime;   // real seconds: same speed at 60 fps or in eco mode
        var a = TargetAcceleration;
        float fx = -a.X * Inertia;                                        // sideways push
        float g = Constrain(Gravity - a.Y * Inertia, Gravity * 0.25f, Gravity * 4);  // moving down = lighter

        Step(dt, fx, g);
        UpdateDrops(dt, g);

        _dim = Lerp(_dim, IsTargetFocused ? 1 : 0.45f, 0.08f);
        DrawWater();
        DrawDrops();
    }

    void Step(float dt, float fx, float g)
    {
        int sub = 8;
        float sdt = dt / sub;
        float damp = Exp(-0.8f * sdt);
        float maxU = 0.45f * Cell / sdt;
        for (int s = 0; s < sub; s++)
        {
            for (int i = 1; i < _cols; i++)
            {
                float v = _u[i] + sdt * (-g * (_h[i] - _h[i - 1]) / Cell + fx);
                if (_h[i] < 0.5f && _h[i - 1] < 0.5f) v = 0;
                _u[i] = Constrain(v * damp, -maxU, maxU);
            }
            _u[0] = _u[_cols] = 0;   // the window's walls
            for (int i = 1; i < _cols; i++)
                _flux[i] = _u[i] * (_u[i] > 0 ? _h[i - 1] : _h[i]);
            for (int i = 0; i < _cols; i++)
                _h[i] = Constrain(_h[i] - sdt * (_flux[i + 1] - _flux[i]) / Cell, 0, Height);
        }

        // Keep the volume constant (rounding slowly leaks it).
        float target = FillLevel * Height * _cols, vol = 0;
        foreach (var h in _h) vol += h;
        if (vol > 1)
            for (int i = 0; i < _cols; i++) _h[i] *= 1 + (target / vol - 1) * 0.1f;
    }

    void UpdateDrops(float dt, float g)
    {
        for (int i = _drops.Count - 1; i >= 0; i--)
        {
            var p = _drops[i];
            var v = _dropSpeeds[i];
            v.Y += g * dt;
            p.X += v.X * dt;
            p.Y += v.Y * dt;
            int col = Constrain((int)(p.X / Cell), 0, _cols - 1);
            if (p.Y >= Height - _h[col])
            {
                Splash(col, 6 * p.Z);
                _drops.RemoveAt(i);
                _dropSpeeds.RemoveAt(i);
            }
        }
    }

    void DrawWater()
    {
        // The body: one polygon through the middle of every column's top.
        NoStroke();
        Fill(20, 90, 170, 110 * _dim);
        BeginShape();
        Vertex(0, Height);
        Vertex(0, Height - _h[0]);
        for (int i = 0; i < _cols; i++)
            Vertex(i * Cell + Cell / 2, Height - _h[i]);
        Vertex(Width, Height - _h[_cols - 1]);
        Vertex(Width, Height);
        EndShape(close: true);

        // The surface line, brighter (foam) where it's steep.
        StrokeWeight(2);
        for (int i = 1; i < _cols; i++)
        {
            float steep = Abs(_h[i] - _h[i - 1]);
            Stroke(150, 230, 255, (110 + Math.Min(steep * 25, 145)) * _dim);
            Line((i - 1) * Cell + Cell / 2, Height - _h[i - 1], i * Cell + Cell / 2, Height - _h[i]);
        }
    }

    void DrawDrops()
    {
        NoStroke();
        Fill(170, 230, 255, 220 * _dim);
        foreach (var p in _drops)
            Ellipse(p.X, p.Y, 4 * p.Z, 6 * p.Z);
    }
}
";

        private const string TerminalKeys =
@"// Bubbles from the keyboard: every key you type in Windows Terminal floats up
// as a bubble with its letter inside. Enter pops them all into sparks,
// Backspace pops the newest one. Shake the window and they scatter.
//
// 1. Open Windows Terminal, press Run and type something there.
// 2. Click anywhere over the terminal: a ring of sparks (the click still
//    reaches the terminal).
// 3. Minimize it and restore it: TargetHidden()/TargetShown() count the trips.
//
// Keys are read only while the terminal is the window in front
// (ListenToTargetKeys), and nothing is ever kept from it. Windows only.
public class TerminalKeys : AttachedSketch
{
    sealed class Bubble { public PVector Pos = new(0, 0), Vel = new(0, 0); public char Letter; public float Hue, Life = 1; }
    sealed class Spark { public PVector Pos = new(0, 0), Vel = new(0, 0); public float Hue, Life = 1; }

    readonly List<Bubble> _bubbles = new();
    readonly List<Spark> _sparks = new();
    int _trips;
    float _typedX;

    public override void Settings() => Attach(WindowTarget.Terminal);

    public override void Setup()
    {
        ListenToTargetKeys();
        ListenToTargetMouse();
        ColorMode(ColorSpaceMode.HSB, 360, 100, 100, 255);
        TextAlign(TextAlignH.Center, TextAlignV.Center);
        _typedX = Width * 0.1f;
    }

    public override void KeyTyped()
    {
        if (Key == ' ')
        {
            _typedX += 14;
            return;
        }
        // Letters go left to right like the text you're typing, wrapping around.
        _typedX += 18;
        if (_typedX > Width - 30) _typedX = 30;
        _bubbles.Add(new Bubble
        {
            Letter = Key,
            Pos = new PVector(_typedX, Height - 30),
            Vel = new PVector(Random(-15, 15), Random(-90, -60)),
            Hue = char.IsDigit(Key) ? 40 : char.IsLetter(Key) ? 195 : 300,
        });
    }

    public override void KeyPressed()
    {
        if (Key == ENTER)
        {
            foreach (var b in _bubbles) Burst(b.Pos.X, b.Pos.Y, b.Hue, 8);
            _bubbles.Clear();
            _typedX = Width * 0.1f;
        }
        else if (Key == BACKSPACE && _bubbles.Count > 0)
        {
            var last = _bubbles[^1];
            Burst(last.Pos.X, last.Pos.Y, last.Hue, 6);
            _bubbles.RemoveAt(_bubbles.Count - 1);
            _typedX = Math.Max(30, _typedX - 18);
        }
    }

    public override void MousePressed() => Burst(MouseX, MouseY, Random(360), 24);

    public override void TargetHidden() => _trips++;

    public override void TargetShown() => Burst(Width / 2f, Height / 2f, 120, 40);

    void Burst(float x, float y, float hue, int n)
    {
        for (int i = 0; i < n; i++)
        {
            float a = TWO_PI * i / n + Random(0.2f);
            float speed = Random(80, 220);
            _sparks.Add(new Spark { Pos = new PVector(x, y), Vel = new PVector(Cos(a) * speed, Sin(a) * speed), Hue = hue });
        }
    }

    public override void Draw()
    {
        Clear();
        float dt = DeltaTime;   // real seconds: same speed at 60 fps or in eco mode

        // Shaking the window throws the bubbles the other way (inertia).
        var kick = TargetAcceleration;
        foreach (var b in _bubbles)
        {
            b.Vel.X -= kick.X * 0.02f * dt;
            b.Vel.Y -= kick.Y * 0.02f * dt;
            b.Vel.Y -= 10 * dt;                 // keep floating up
            b.Vel.X *= 0.99f;
            b.Pos.X += b.Vel.X * dt;
            b.Pos.Y += b.Vel.Y * dt;
            if (b.Pos.X < 16 || b.Pos.X > Width - 16) b.Vel.X *= -0.8f;
            b.Pos.X = Constrain(b.Pos.X, 16, Width - 16);
            if (b.Pos.Y < 20) b.Life -= dt * 2;  // fades out at the top
        }
        _bubbles.RemoveAll(b => b.Life <= 0);

        foreach (var s in _sparks)
        {
            s.Vel.Y += 300 * dt;
            s.Pos.X += s.Vel.X * dt;
            s.Pos.Y += s.Vel.Y * dt;
            s.Life -= dt * 1.4f;
        }
        _sparks.RemoveAll(s => s.Life <= 0);

        TextSize(16);
        foreach (var b in _bubbles)
        {
            Stroke(b.Hue, 60, 100, 200 * b.Life);
            StrokeWeight(1.5f);
            Fill(b.Hue, 70, 40, 90 * b.Life);
            Ellipse(b.Pos.X, b.Pos.Y, 28, 28);
            NoStroke();
            Fill(b.Hue, 20, 100, 255 * b.Life);
            Text(b.Letter.ToString(), b.Pos.X, b.Pos.Y);
        }

        NoStroke();
        foreach (var s in _sparks)
        {
            Fill(s.Hue, 80, 100, 255 * s.Life);
            Ellipse(s.Pos.X, s.Pos.Y, 4, 4);
        }

        if (_trips > 0)
        {
            Fill(0, 0, 100, 120);
            TextSize(12);
            Text($""minimized {_trips}×"", Width - 70, Height - 14);
        }
    }
}
";

        private const string TerminalFlowField =
@"// Flow field in the terminal: particles drift along a vector field drawn
// behind Windows Terminal's text -- and every terminal window gets a field of its own.
//
// Idea from ""Vector field with particles"" by jcponcemath on OpenProcessing
// (openprocessing.org/sketch/3017881); this is a fresh take written for Dana.
//
// 1. Open one or more Windows Terminal windows and press Run.
// 2. Drag a terminal: the particles lag behind (inertia, -TargetAcceleration)
//    and the field twists with the speed (TargetVelocity). Let go: it untwists.
// 3. Run a command that fails (try:  cmd /c exit 3): the field becomes a red
//    storm and shows the exit code. A command that works calms it with a cyan
//    pulse. dana-signal reset draws a new field.
//    Commands need the PowerShell snippet once: run ShellSignals.InstallForPowerShell()
//    (or use the tray menu of the exported app), then open a new terminal.
public class TerminalFlowField : AttachedSketch
{
    const int Tail = 10;  // positions kept per particle for its trail

    sealed class Particle
    {
        public float X, Y, VX, VY, Age, Life;
        public readonly float[] TX = new float[Tail], TY = new float[Tail];
        public int Head;
    }

    readonly List<Particle> _particles = new();

    // Trails are drawn in batches: every segment of the same color and alpha goes
    // into one shape (BeginShape(ShapeKind.Lines) = one Skia call) instead of a
    // Stroke() + Line() per segment, thousands of calls a frame otherwise.
    const int HueSteps = 24, AlphaSteps = 8;   // 15° hue steps, 8 alpha levels
    readonly List<float>[] _batches = Enumerable.Range(0, HueSteps * (AlphaSteps + 1)).Select(_ => new List<float>()).ToArray();
    float _a, _b, _swirl, _phase;   // the field's shape: different per window (each instance has its own seed)
    float _storm;                   // 1 right after a failed command, fades out
    float _calm;                    // 1 right after a command that worked
    float _twist;                   // follows how fast the window is dragged
    float _ringRadius = -1, _ringHue;

    public override void Settings() =>
        Attach(WindowTarget.Terminal, mode: AttachMode.EachWindow);

    public override void Setup()
    {
        ListenToShell();
        ColorMode(ColorSpaceMode.HSB, 360, 100, 100, 255);
        TextAlign(TextAlignH.Right, TextAlignV.Bottom);
        NewField();
        FitParticles();
    }

    public override void WindowResized() => FitParticles();

    void NewField()
    {
        _a = Random(1.5f, 3.5f);
        _b = Random(1.5f, 3.5f);
        _swirl = Random(-0.8f, 0.8f);
        _phase = Random(TWO_PI);
    }

    void FitParticles()
    {
        int wanted = Constrain(Width * Height / 900, 150, 1000);
        while (_particles.Count < wanted)
            _particles.Add(Respawn(new Particle()));
        if (_particles.Count > wanted)
            _particles.RemoveRange(wanted, _particles.Count - wanted);
    }

    Particle Respawn(Particle p)
    {
        p.X = Random(Width);
        p.Y = Random(Height);
        p.VX = p.VY = 0;
        p.Age = 0;
        p.Life = Random(2, 6);
        for (int i = 0; i < Tail; i++) { p.TX[i] = p.X; p.TY[i] = p.Y; }
        return p;
    }

    // --- the vector field -------------------------------------------------------

    (float X, float Y) Field(float x, float y, float t)
    {
        // Smooth swirls in coordinates centred on the window, scaled to it.
        float s = Math.Max(1, Math.Min(Width, Height));
        float u = (x - Width / 2f) / s * 2, v = (y - Height / 2f) / s * 2;
        float fx = Sin(_a * v + _phase + t * 0.2f) - _swirl * v;
        float fy = Cos(_b * u - _phase + t * 0.15f) + _swirl * u;

        // After an error: noisy turbulence on top.
        if (_storm > 0)
        {
            float n = Noise(x * 0.008f + t * 0.4f, y * 0.008f) * TWO_PI * 3;
            fx += Cos(n) * 2.5f * _storm;
            fy += Sin(n) * 2.5f * _storm;
        }

        // While the window is dragged: the whole field turns.
        float c = Cos(_twist), sn = Sin(_twist);
        return (fx * c - fy * sn, fx * sn + fy * c);
    }

    // --- the shell ----------------------------------------------------------------

    public override void CommandFinished()
    {
        if (LastCommandOk)
        {
            _calm = 1;
            Ring(170);
        }
        else
        {
            _storm = 1;
            Ring(5);
            foreach (var p in _particles)   // a kick in every direction
            {
                float a = Random(TWO_PI);
                p.VX += Cos(a) * 250;
                p.VY += Sin(a) * 250;
            }
        }
    }

    public override void OnShellSignal(string name)
    {
        if (name == ""reset"")
        {
            NewField();
            foreach (var p in _particles) Respawn(p);
            Ring(270);
        }
    }

    void Ring(float hue)
    {
        _ringRadius = 0;
        _ringHue = hue;
    }

    // --- every frame ----------------------------------------------------------------

    void DrawBatches()
    {
        // StrokeWeight(0) is a hairline: exactly one physical pixel at any DPI,
        // and Skia's fastest antialiased line -- about 10x cheaper than 1.6 px
        // with round ends (the trails are thousands of tiny segments).
        StrokeWeight(0);
        NoFill();
        for (int b = 0; b < _batches.Length; b++)
        {
            var batch = _batches[b];
            if (batch.Count == 0) continue;
            int hueStep = b / (AlphaSteps + 1), alphaStep = b % (AlphaSteps + 1);
            Stroke(hueStep * (360f / HueSteps), 65 + 35 * _storm, 100, 255f * alphaStep / AlphaSteps);
            BeginShape(ShapeKind.Lines);
            for (int i = 0; i < batch.Count; i += 2)
                Vertex(batch[i], batch[i + 1]);
            EndShape();
            batch.Clear();
        }
    }

    public override void Draw()
    {
        Clear(); // transparent: the terminal stays visible underneath
        float dt = DeltaTime;   // real seconds: same speed at 60 fps or in eco mode
        float t = Millis() / 1000f;

        _storm = Math.Max(0, _storm - dt / 2.5f);
        _calm = Math.Max(0, _calm - dt / 1.5f);
        _twist = Lerp(_twist, Constrain(TargetVelocity.X / 1500f, -1.3f, 1.3f), Math.Min(1, dt * 4));
        var push = TargetAcceleration;
        float speed = 110 * (1 + 1.5f * _storm) * (1 - 0.5f * _calm);
        float follow = Math.Min(1, dt * 3);

        foreach (var p in _particles)
        {
            var (fx, fy) = Field(p.X, p.Y, t);
            // Follow the field, and feel the window's motion the other way round.
            p.VX += (fx * speed - p.VX) * follow - push.X * 0.15f * dt;
            p.VY += (fy * speed - p.VY) * follow - push.Y * 0.15f * dt;
            p.X += p.VX * dt;
            p.Y += p.VY * dt;
            p.Age += dt;
            p.Head = (p.Head + 1) % Tail;
            p.TX[p.Head] = p.X;
            p.TY[p.Head] = p.Y;

            if (p.Age > p.Life || p.X < -20 || p.Y < -20 || p.X > Width + 20 || p.Y > Height + 20)
            {
                Respawn(p);
                continue;
            }

            // Blue-violet by direction; red in a storm; cyan when calm.
            float hue = 190 + 50 * Sin(Atan2(p.VY, p.VX));
            hue = Lerp(hue, 5, _storm);
            hue = Lerp(hue, 175, _calm * (1 - _storm));
            float fade = Math.Min(1, Math.Min(p.Age * 2, (p.Life - p.Age) * 2));
            int hueStep = ((int)Math.Round(hue / (360f / HueSteps)) % HueSteps + HueSteps) % HueSteps;
            for (int k = 1; k < Tail; k++)   // oldest segment first, fainter
            {
                int alphaStep = (int)Math.Round(fade * k / Tail * AlphaSteps);
                if (alphaStep == 0) continue;
                int i0 = (p.Head + k) % Tail, i1 = (p.Head + k + 1) % Tail;
                var batch = _batches[hueStep * (AlphaSteps + 1) + alphaStep];
                batch.Add(p.TX[i0]); batch.Add(p.TY[i0]); batch.Add(p.TX[i1]); batch.Add(p.TY[i1]);
            }
        }
        DrawBatches();

        // The pulse after a command, from the centre outwards.
        if (_ringRadius >= 0)
        {
            float max = Dist(0, 0, Width, Height) / 2;
            _ringRadius += 700 * dt;
            Stroke(_ringHue, 80, 100, 220 * (1 - _ringRadius / max));
            StrokeWeight(3);
            Ellipse(Width / 2f, Height / 2f, _ringRadius * 2, _ringRadius * 2);
            if (_ringRadius > max) _ringRadius = -1;
        }

        if (_storm > 0)
        {
            NoStroke();
            Fill(5, 80, 100, 255 * _storm);
            TextSize(28);
            Text($""exit {LastExitCode}"", Width - 16, Height - 12);
        }
    }
}
";

        private const string TerminalCloth =
@"// Cloth on the terminal: the whole Windows Terminal window is covered by a
// sheet of see-through cloth hanging from its top edge. Move the window and
// the cloth swings, ripples and folds.
//
// Idea from ""Cloth!!"" by Millie on OpenProcessing (openprocessing.org/sketch/2999200);
// its code is hidden, so this is written from scratch for Dana: a grid of
// points joined by sticks (Verlet integration), pinned along the top, drawn
// as one shaded mesh plus a fine weave.
//
// 1. Open Windows Terminal and press Run.
// 2. Drag the terminal: the cloth lags behind and swings (-TargetAcceleration).
//    Shake it hard and it flaps.
// 3. Move the mouse through it to push it; click to cut a hole (the click
//    still reaches the terminal). Too much stretch tears it too.
// 4. A command that fails rips a hole and flashes red; one that works sends a
//    gust of wind through it. dana-signal reset hangs a new cloth.
public class TerminalCloth : AttachedSketch
{
    sealed class Point
    {
        public float X, Y, OldX, OldY, RestX;
        public bool Pinned;
    }

    sealed class Stick
    {
        public Point A, B;
        public float Length;
        public bool Broken;
    }

    const float Gravity = 500;       // px/s²
    const float Inertia = 0.7f;      // how much the window's motion moves the cloth
    const float TearStretch = 3f;    // a stick breaks past this many times its length

    readonly List<Point> _points = new();
    readonly List<Stick> _sticks = new();
    Stick[] _right = new Stick[0], _down = new Stick[0];   // the stick to the right of / below each point
    int _cols, _rows;
    float _redness;                  // 1 right after a failed command
    float _gust;                     // 1 right after a command that worked

    public override void Settings() => Attach(WindowTarget.Terminal);

    public override void Setup()
    {
        ListenToShell();
        ListenToTargetMouse();
        Hang();
    }

    public override void WindowResized() => Hang();

    // A cloth exactly the size of the window, its top row pinned to the top edge.
    void Hang()
    {
        _points.Clear();
        _sticks.Clear();
        _cols = Constrain(Width / 28, 12, 60) + 1;
        _rows = Constrain(Height / 28, 8, 40) + 1;
        float sx = Width / (float)(_cols - 1), sy = Height / (float)(_rows - 1);
        _right = new Stick[_cols * _rows];
        _down = new Stick[_cols * _rows];
        for (int r = 0; r < _rows; r++)
            for (int c = 0; c < _cols; c++)
            {
                float x = c * sx, y = r * sy;
                _points.Add(new Point { X = x, Y = y, OldX = x, OldY = y, RestX = x, Pinned = r == 0 });
            }
        for (int r = 0; r < _rows; r++)
            for (int c = 0; c < _cols; c++)
            {
                int i = r * _cols + c;
                if (c + 1 < _cols) _right[i] = Join(_points[i], _points[i + 1], sx);
                if (r + 1 < _rows) _down[i] = Join(_points[i], _points[i + _cols], sy);
            }
    }

    Stick Join(Point a, Point b, float length)
    {
        var s = new Stick { A = a, B = b, Length = length };
        _sticks.Add(s);
        return s;
    }

    // --- input ------------------------------------------------------------------

    public override void MousePressed() => Cut(MouseX, MouseY, 22);

    public override void CommandFinished()
    {
        if (LastCommandOk) { _gust = 1; return; }
        _redness = 1;
        Cut(Random(Width * 0.2f, Width * 0.8f), Random(Height * 0.3f, Height * 0.7f), Math.Min(Width, Height) * 0.12f);
    }

    public override void OnShellSignal(string name)
    {
        if (name == ""reset"") Hang();
    }

    void Cut(float x, float y, float radius)
    {
        foreach (var s in _sticks)
        {
            float mx = (s.A.X + s.B.X) / 2, my = (s.A.Y + s.B.Y) / 2;
            if (Dist(mx, my, x, y) < radius) s.Broken = true;
        }
    }

    // --- every frame ----------------------------------------------------------------

    public override void Draw()
    {
        Clear();
        float dt = Math.Min(DeltaTime, 1 / 30f);
        float t = Millis() / 1000f;
        var a = TargetAcceleration;
        _redness = Math.Max(0, _redness - dt / 2);
        _gust = Math.Max(0, _gust - dt / 1.5f);

        // A jerk of the window is felt, but capped: a real drag shouldn't throw the cloth off.
        float ax = Constrain(-a.X * Inertia, -5000, 5000), ay = Constrain(-a.Y * Inertia, -5000, 5000);
        Simulate(dt, t, ax, Gravity + ay);
        DrawMesh();
        DrawWeave();
    }

    void Simulate(float dt, float t, float ax, float ay)
    {
        bool mouseInside = MouseX >= 0 && MouseX < Width && MouseY >= 0 && MouseY < Height;
        float gustX = _gust * 2500;

        // Verlet: the velocity is just ""where it was"" vs ""where it is"".
        foreach (var p in _points)
        {
            if (p.Pinned) continue;
            float vx = (p.X - p.OldX) * 0.985f, vy = (p.Y - p.OldY) * 0.985f;
            // A light breeze so the cloth is never perfectly still, plus the gust.
            float breeze = (Noise(p.Y * 0.004f, t * 0.4f) - 0.5f) * 90 + gustX * Noise(p.Y * 0.01f, t * 3);
            p.OldX = p.X;
            p.OldY = p.Y;
            p.X += vx + (ax + breeze) * dt * dt;
            p.Y += vy + ay * dt * dt;

            // The mouse pushes the cloth away (no click needed: MouseX/MouseY follow the cursor).
            if (mouseInside)
            {
                float d = Dist(p.X, p.Y, MouseX, MouseY);
                if (d < 45 && d > 0.01f)
                {
                    p.X += (p.X - MouseX) / d * (45 - d) * 0.25f;
                    p.Y += (p.Y - MouseY) / d * (45 - d) * 0.25f;
                }
            }
        }

        // Sticks pull their two points back to their length, a few times per frame.
        for (int iteration = 0; iteration < 6; iteration++)
            foreach (var s in _sticks)
            {
                if (s.Broken) continue;
                float dx = s.B.X - s.A.X, dy = s.B.Y - s.A.Y;
                float d = Math.Max(0.001f, Sqrt(dx * dx + dy * dy));
                if (d > s.Length * TearStretch) { s.Broken = true; continue; }
                // Cloth resists stretching fully and squeezing only a little, so it can fold.
                float stiffness = d > s.Length ? 1 : 0.3f;
                float push = (d - s.Length) / d * (s.A.Pinned || s.B.Pinned ? 1 : 0.5f) * stiffness;
                if (!s.A.Pinned) { s.A.X += dx * push; s.A.Y += dy * push; }
                if (!s.B.Pinned) { s.B.X -= dx * push; s.B.Y -= dy * push; }
            }

        // Torn-off pieces that fell away stop below the window.
        foreach (var p in _points)
            if (p.Y > Height + 300) { p.Y = p.OldY = Height + 300; p.OldX = p.X; }
    }

    // --- drawing --------------------------------------------------------------------

    // The cloth itself: every quad whose four sides hold, filled with a shade for
    // how the cloth bends there -- folds darker, stretched or swung parts lighter.
    // Quads of the same shade go in one shape (one contour each): a handful of
    // draw calls for the whole window instead of one per quad.
    const int Shades = 6;
    readonly List<int>[] _byShade = Enumerable.Range(0, Shades).Select(_ => new List<int>()).ToArray();

    void DrawMesh()
    {
        foreach (var list in _byShade) list.Clear();
        for (int r = 0; r < _rows - 1; r++)
            for (int c = 0; c < _cols - 1; c++)
            {
                int i = r * _cols + c;
                if (_right[i].Broken || _down[i].Broken || _right[i + _cols].Broken || _down[i + 1].Broken) continue;
                _byShade[(int)(Light(i) * (Shades - 0.01f))].Add(i);
            }

        // Without antialiasing: much faster for shapes this big, and neighbouring
        // quads meet exactly (no faint seams). The weave drawn on top hides the edges.
        NoSmooth();
        NoStroke();
        for (int k = 0; k < Shades; k++)
        {
            if (_byShade[k].Count == 0) continue;
            float shade = k / (float)(Shades - 1);
            Fill(Lerp(60, 150, shade) + 150 * _redness,
                 Lerp(80, 190, shade) * (1 - 0.6f * _redness),
                 Lerp(150, 255, shade) * (1 - 0.5f * _redness),
                 Lerp(26, 62, Math.Abs(shade - 0.5f) * 2) + 45 * _redness);
            BeginShape();
            foreach (int i in _byShade[k])
            {
                BeginContour();
                Vertex(_points[i].X, _points[i].Y);
                Vertex(_points[i + 1].X, _points[i + 1].Y);
                Vertex(_points[i + _cols + 1].X, _points[i + _cols + 1].Y);
                Vertex(_points[i + _cols].X, _points[i + _cols].Y);
                EndContour();
            }
            EndShape();
        }
        Smooth();
    }

    // 0 (a fold) to 1 (stretched or swung out) for the quad at point i.
    float Light(int i)
    {
        Point a = _points[i], b = _points[i + 1], d = _points[i + _cols];
        float span = Dist(a.X, a.Y, b.X, b.Y) / _right[i].Length;              // < 1 where it folds
        float slant = ((d.X - a.X) + (a.X - a.RestX) * 0.15f) / _down[i].Length;  // swinging sideways
        return Constrain(0.5f + (span - 1) * 2.5f + slant * 0.6f, 0, 1);
    }

    // The weave: every stick as a hairline (Skia's fastest line), one shape per
    // color; the stretched ones, warm, go in a second shape on top.
    readonly List<Stick> _stretched = new();

    void DrawWeave()
    {
        StrokeWeight(0);
        Stroke(170 + 85 * _redness, 200 - 120 * _redness, 255 - 150 * _redness, 55);
        _stretched.Clear();
        BeginShape(ShapeKind.Lines);
        foreach (var s in _sticks)
        {
            if (s.Broken) continue;
            float dx = s.B.X - s.A.X, dy = s.B.Y - s.A.Y;
            if (dx * dx + dy * dy > s.Length * s.Length * 1.69f) { _stretched.Add(s); continue; }   // over 1.3x its length
            Vertex(s.A.X, s.A.Y);
            Vertex(s.B.X, s.B.Y);
        }
        EndShape();

        Stroke(255, 160 - 100 * _redness, 90 - 60 * _redness, 200);
        BeginShape(ShapeKind.Lines);
        foreach (var s in _stretched)
        {
            Vertex(s.A.X, s.A.Y);
            Vertex(s.B.X, s.B.Y);
        }
        EndShape();

        // The top hem the cloth hangs from.
        Stroke(220, 230, 255, 140);
        StrokeWeight(2);
        Line(0, 1, Width, 1);
    }
}
";

        private const string TerminalBlobs =
@"// Blobs in the terminal: soft blobs (metaballs) float inside Windows Terminal
// like a lava lamp, slosh when you move the window, and melt into each other.
//
// Idea from ""Blobules"" on OpenProcessing (openprocessing.org/sketch/2993583);
// its code is hidden, so this is written from scratch for Dana.
//
// 1. Open Windows Terminal and press Run.
// 2. Drag or shake the terminal: the blobs are thrown the other way (inertia),
//    bounce off the edges and drift back together.
// 3. Type in the terminal: each key drops a small blob that swims to the others
//    and merges; Backspace takes one away. The cursor pulls them towards it.
// 4. A command that fails blows them apart into red droplets; after a moment
//    they find each other and melt back together. One that works makes them pulse.
//
// How it's drawn: a ""field"" adds up every blob's influence on a grid; where
// it's over a threshold you're inside a blob. Marching squares turns each grid
// cell into the exact piece of blob it holds: those pieces are filled as one
// mesh (shaded smoothly by the field, so blobs look round), a soft glossy
// highlight goes on top, and the pieces' edges are the outline.
public class TerminalBlobs : AttachedSketch
{
    sealed class Blob { public float X, Y, VX, VY, R, Loner; }   // Loner: seconds it won't merge

    const float Cell = 8;           // grid size in pixels: smaller = smoother and slower
    const float Threshold = 1;
    const int MaxBlobs = 40;

    readonly List<Blob> _blobs = new();
    float[] _field = new float[0];
    int _cols, _rows;
    float _boil;                    // 1 right after a failed command, fades
    float _apart;                   // seconds left before split blobs may merge again
    float _pulse;                   // 1 right after a command that worked, fades
    float _hue = 175;               // the blobs' hue now (eases to red and back)

    public override void Settings() => Attach(WindowTarget.Terminal);

    public override void Setup()
    {
        ListenToShell();
        ListenToTargetKeys();
        ColorMode(ColorSpaceMode.HSB, 360, 100, 100, 255);
        WindowResized();
        float r = BaseRadius();
        for (int i = 0; i < 4; i++)
            AddBlob(Width * Random(0.3f, 0.7f), Height * Random(0.35f, 0.65f), r * Random(0.7f, 1.1f), Random(-80, 80), Random(-80, 80));
    }

    float BaseRadius() => Math.Min(Width, Height) * 0.11f;

    public override void WindowResized()
    {
        _cols = (int)(Width / Cell) + 2;
        _rows = (int)(Height / Cell) + 2;
        _field = new float[_cols * _rows];
    }

    Blob? AddBlob(float x, float y, float r, float vx, float vy)
    {
        if (_blobs.Count >= MaxBlobs) return null;
        var b = new Blob { X = x, Y = y, R = r, VX = vx, VY = vy };
        _blobs.Add(b);
        return b;
    }

    // Lava lamp: now and then a big blob lets a droplet go, which drifts off and comes back.
    void Bud()
    {
        var big = _blobs.MaxBy(b => b.R)!;
        if (big.R < BaseRadius() * 0.9f || Random(1) > DeltaTime / 2.5f) return;
        float r = big.R * Random(0.3f, 0.45f);
        big.R = Sqrt(big.R * big.R - r * r);
        float angle = Random(TWO_PI);
        var drop = AddBlob(big.X + Cos(angle) * big.R, big.Y + Sin(angle) * big.R, r,
                           big.VX + Cos(angle) * 260, big.VY + Sin(angle) * 260);
        if (drop != null) drop.Loner = Random(1.5f, 3.5f);
    }

    // --- input ------------------------------------------------------------------

    public override void KeyTyped()
    {
        if (Key == BACKSPACE)
        {
            if (_blobs.Count > 1) _blobs.Remove(_blobs.MinBy(b => b.R)!);
        }
        else
            AddBlob(Random(Width * 0.1f, Width * 0.9f), Height * 0.05f, BaseRadius() * Random(0.3f, 0.45f), Random(-50, 50), Random(100, 250));
    }

    public override void CommandFinished()
    {
        if (LastCommandOk) { _pulse = 1; return; }

        // Blow every blob apart into droplets flying outwards.
        _boil = 1;
        _apart = 1.4f;
        var old = _blobs.ToList();
        _blobs.Clear();
        foreach (var b in old)
        {
            int pieces = b.R > BaseRadius() * 0.6f ? 4 : 2;
            float r = b.R / Sqrt(pieces);   // same total area
            for (int i = 0; i < pieces; i++)
            {
                float angle = TWO_PI * i / pieces + Random(-0.5f, 0.5f);
                float speed = Random(500, 950);
                AddBlob(b.X + Cos(angle) * r * 0.5f, b.Y + Sin(angle) * r * 0.5f, r,
                        b.VX + Cos(angle) * speed, b.VY + Sin(angle) * speed);
            }
        }
    }

    // --- every frame ----------------------------------------------------------------

    public override void Draw()
    {
        Clear();
        float dt = Math.Min(DeltaTime, 1 / 30f);
        _boil = Math.Max(0, _boil - dt / 2.5f);
        _pulse = Math.Max(0, _pulse - dt * 1.5f);
        _apart = Math.Max(0, _apart - dt);
        // Teal, and red (360) after a failed command: easing through blue and magenta, never green.
        _hue = Lerp(_hue, _boil > 0.05f ? 360 : 175, Math.Min(1, dt * 3));

        Bud();
        Move(dt);
        Merge();
        ComputeField();
        DrawFill();
        DrawShine();
        DrawOutline();
    }

    void Move(float dt)
    {
        var a = TargetAcceleration;
        float t = Millis() / 1000f;

        // Where the blobs gather: their center of mass (weighted by area).
        float cx = 0, cy = 0, mass = 0;
        foreach (var b in _blobs) { float m = b.R * b.R; cx += b.X * m; cy += b.Y * m; mass += m; }
        cx /= mass; cy /= mass;
        float cohesion = _apart > 0 ? 0 : 2.2f;   // off while freshly split, so they really fly apart
        bool mouseInside = MouseX >= 0 && MouseX < Width && MouseY >= 0 && MouseY < Height;

        for (int i = 0; i < _blobs.Count; i++)
        {
            var b = _blobs[i];
            b.Loner = Math.Max(0, b.Loner - dt);
            // The window's motion: the blob keeps going while the window moves under it.
            b.VX += -a.X * 0.9f * dt;
            b.VY += -a.Y * 0.9f * dt;
            // Lava lamp: a gentle wander, a little gravity, and the pull of the group.
            b.VX += (Noise(i * 7.1f, t * 0.3f) - 0.5f) * 220 * dt + (cx - b.X) * (b.Loner > 0 ? 0 : cohesion) * dt;
            b.VY += (Noise(i * 3.7f + 50, t * 0.3f) - 0.5f) * 220 * dt + (cy - b.Y) * (b.Loner > 0 ? 0 : cohesion) * dt + 25 * dt;
            if (mouseInside)
            {
                float dx = MouseX - b.X, dy = MouseY - b.Y, d = Math.Max(40, Sqrt(dx * dx + dy * dy));
                b.VX += dx / d * 260 * dt;
                b.VY += dy / d * 260 * dt;
            }
            if (_boil > 0)
            {
                b.VX += Random(-1, 1) * 600 * _boil * dt;
                b.VY += Random(-1, 1) * 600 * _boil * dt;
            }

            float drag = 1 - Math.Min(1, dt * 0.9f);
            b.VX = Constrain(b.VX * drag, -2500, 2500);
            b.VY = Constrain(b.VY * drag, -2500, 2500);
            b.X += b.VX * dt;
            b.Y += b.VY * dt;

            float edge = b.R * 0.6f;
            if (b.X < edge) { b.X = edge; b.VX = Math.Abs(b.VX) * 0.8f; }
            if (b.X > Width - edge) { b.X = Width - edge; b.VX = -Math.Abs(b.VX) * 0.8f; }
            if (b.Y < edge) { b.Y = edge; b.VY = Math.Abs(b.VY) * 0.8f; }
            if (b.Y > Height - edge) { b.Y = Height - edge; b.VY = -Math.Abs(b.VY) * 0.8f; }
        }
    }

    // Two blobs whose centers come close become one (same area, same momentum).
    void Merge()
    {
        if (_apart > 0) return;
        for (int i = 0; i < _blobs.Count; i++)
            for (int j = _blobs.Count - 1; j > i; j--)
            {
                Blob a = _blobs[i], b = _blobs[j];
                if (a.Loner > 0 || b.Loner > 0) continue;
                if (Dist(a.X, a.Y, b.X, b.Y) > (a.R + b.R) * 0.45f) continue;
                float ma = a.R * a.R, mb = b.R * b.R, m = ma + mb;
                a.X = (a.X * ma + b.X * mb) / m;
                a.Y = (a.Y * ma + b.Y * mb) / m;
                a.VX = (a.VX * ma + b.VX * mb) / m;
                a.VY = (a.VY * ma + b.VY * mb) / m;
                a.R = Sqrt(m);
                _blobs.RemoveAt(j);
            }
    }

    // The field: sum of r² / distance² from every blob, at each grid corner.
    void ComputeField()
    {
        float grow = 1 + 0.25f * Sin(_pulse * PI);   // a command that worked: one soft pulse
        for (int j = 0; j < _rows; j++)
            for (int i = 0; i < _cols; i++)
            {
                float x = i * Cell, y = j * Cell, sum = 0;
                foreach (var b in _blobs)
                {
                    float dx = x - b.X, dy = y - b.Y, r = b.R * grow;
                    sum += r * r / (dx * dx + dy * dy + 1);
                }
                _field[j * _cols + i] = sum;
            }
    }

    float F(int i, int j) => _field[j * _cols + i];

    // Where the field crosses the threshold between two corners (linear interpolation).
    float Cross(float a, float b) => Constrain((Threshold - a) / (b - a), 0, 1);

    // --- the fill: every cell's piece of blob, as triangles of one mesh -----------

    // Corners of a cell clockwise from top-left, and the field there.
    readonly float[] _cx = new float[4], _cy = new float[4], _cf = new float[4];
    readonly List<(float X, float Y, float F)> _poly = new();

    void DrawFill()
    {
        NoStroke();
        BeginShape(ShapeKind.Triangles);
        for (int j = 0; j < _rows - 1; j++)
            for (int i = 0; i < _cols - 1; i++)
            {
                float x = i * Cell, y = j * Cell;
                _cx[0] = x;        _cy[0] = y;        _cf[0] = F(i, j);
                _cx[1] = x + Cell; _cy[1] = y;        _cf[1] = F(i + 1, j);
                _cx[2] = x + Cell; _cy[2] = y + Cell; _cf[2] = F(i + 1, j + 1);
                _cx[3] = x;        _cy[3] = y + Cell; _cf[3] = F(i, j + 1);

                // Walk the square: keep the corners that are inside, and where an edge
                // crosses the threshold add the crossing. That's the cell's piece of blob.
                _poly.Clear();
                for (int k = 0; k < 4; k++)
                {
                    int n = (k + 1) % 4;
                    bool inK = _cf[k] > Threshold, inN = _cf[n] > Threshold;
                    if (inK) _poly.Add((_cx[k], _cy[k], _cf[k]));
                    if (inK != inN)
                    {
                        float u = Cross(_cf[k], _cf[n]);
                        _poly.Add((Lerp(_cx[k], _cx[n], u), Lerp(_cy[k], _cy[n], u), Threshold));
                    }
                }
                for (int k = 1; k + 1 < _poly.Count; k++)   // a fan of triangles
                {
                    Shade(_poly[0]);
                    Shade(_poly[k]);
                    Shade(_poly[k + 1]);
                }
            }
        EndShape();
    }

    // Each vertex gets its color from the field: see-through at the edge, brighter
    // towards the middle -- the blobs look round and the terminal still reads under them.
    void Shade((float X, float Y, float F) v)
    {
        // depth: 0 at the edge, approaching 1 towards the middle -- never a hard
        // cap, so there's no flat plateau with a jagged rim, just a smooth slope.
        float over = Math.Max(0, v.F - Threshold);
        float depth = over / (over + 1.6f);
        depth = depth * depth * (3 - 2 * depth);   // smoothstep: soft at both ends
        Fill((_hue + 20 * depth) % 360, 75 - 35 * depth + 20 * _boil, 35 + 50 * depth + 10 * _pulse, 60 + 90 * depth);
        Vertex(v.X, v.Y);
    }

    // A soft glossy highlight on every blob, up and to the left, like light on gel:
    // a few stacked translucent ellipses make a smooth radial falloff.
    void DrawShine()
    {
        NoStroke();
        foreach (var b in _blobs)
        {
            float x = b.X - b.R * 0.28f, y = b.Y - b.R * 0.32f;
            for (int k = 0; k < 14; k++)
            {
                float u = k / 13f;                      // 0 = widest and faintest, 1 = smallest
                Fill(_hue % 360, 25 * (1 - u), 100, 6 + 5 * u);
                float w = b.R * Lerp(0.75f, 0.12f, u);
                Ellipse(x, y, w * 1.25f, w);
            }
        }
    }

    // --- the outline: where the field crosses the threshold, all in one shape ---

    void DrawOutline()
    {
        Stroke(_hue % 360, 45 + 40 * _boil, 100, 235);
        StrokeWeight(1.6f);
        BeginShape(ShapeKind.Lines);
        for (int j = 0; j < _rows - 1; j++)
            for (int i = 0; i < _cols - 1; i++)
            {
                float tl = F(i, j), tr = F(i + 1, j), br = F(i + 1, j + 1), bl = F(i, j + 1);
                int code = (tl > Threshold ? 8 : 0) | (tr > Threshold ? 4 : 0) | (br > Threshold ? 2 : 0) | (bl > Threshold ? 1 : 0);
                if (code == 0 || code == 15) continue;

                float x = i * Cell, y = j * Cell;
                // The four edge crossings (top, right, bottom, left).
                float tx = x + Cross(tl, tr) * Cell, ty = y;
                float rx = x + Cell, ry = y + Cross(tr, br) * Cell;
                float bx = x + Cross(bl, br) * Cell, by = y + Cell;
                float lx = x, ly = y + Cross(tl, bl) * Cell;

                switch (code)
                {
                    case 1: case 14: Seg(lx, ly, bx, by); break;
                    case 2: case 13: Seg(bx, by, rx, ry); break;
                    case 3: case 12: Seg(lx, ly, rx, ry); break;
                    case 4: case 11: Seg(tx, ty, rx, ry); break;
                    case 5: Seg(lx, ly, tx, ty); Seg(bx, by, rx, ry); break;
                    case 6: case 9: Seg(tx, ty, bx, by); break;
                    case 7: case 8: Seg(lx, ly, tx, ty); break;
                    case 10: Seg(lx, ly, bx, by); Seg(tx, ty, rx, ry); break;
                }
            }
        EndShape();
    }

    void Seg(float x1, float y1, float x2, float y2)
    {
        Vertex(x1, y1);
        Vertex(x2, y2);
    }
}
";

        private const string TerminalWobble =
@"// Wobbly functions under the terminal's text: a stack of glowing ribbons along
// the bottom of every Windows Terminal window. Each line is a wobbly function
// (a sum of sines) and also a string you can pluck: the window's motion, your
// keys, the cursor and your commands all send waves along it.
//
// Idea from ""Class 8/31"" by Tyler Alexander on OpenProcessing
// (openprocessing.org/sketch/2998495), made for Genuary 2024's ""wobbly
// function day""; its code is hidden, so this is written from scratch for Dana.
//
// 1. Open one or more Windows Terminal windows and press Run.
// 2. Drag a terminal: the ribbons slosh the other way (each layer a little
//    later than the one above) and settle. Dragging sideways slides the waves.
// 3. Type in the terminal: every key plucks the strings; Enter plucks hard.
//    The cursor pushes them aside like a finger in water.
// 4. A command that fails snaps them into a jagged red tangle; one that works
//    sends a calm wave from left to right.
public class TerminalWobble : AttachedSketch
{
    const int Curves = 13;
    const int Steps = 96;           // points per curve

    // Each curve is also a string: a height and a speed per point (a 1-D wave equation).
    readonly float[,] _wave = new float[Curves, Steps + 1];
    readonly float[,] _speed = new float[Curves, Steps + 1];
    // Each curve hangs on a spring that the window's motion shakes (lower = later).
    readonly float[] _offset = new float[Curves], _offsetSpeed = new float[Curves];
    float _slide;                   // horizontal shift of the waves, from dragging sideways
    float _jagged;                  // 1 right after a failed command
    float _seed;

    public override void Settings() =>
        Attach(WindowTarget.Terminal, mode: AttachMode.EachWindow);

    public override void Setup()
    {
        ListenToShell();
        ListenToTargetKeys();
        ColorMode(ColorSpaceMode.HSB, 360, 100, 100, 255);
        _seed = Random(1000);   // each window's instance has its own seed: its own curves
    }

    // --- input ------------------------------------------------------------------

    public override void KeyTyped()
    {
        bool enter = Key == ENTER || Key == RETURN;
        Pluck(Random(0.05f, 0.95f), enter ? 40 : 14, enter ? Curves : 5);
    }

    public override void CommandFinished()
    {
        if (LastCommandOk)
        {
            for (int c = 0; c < Curves; c++) _speed[c, 2] -= 900;   // a wave from the left edge
            return;
        }
        _jagged = 1;
        for (int c = 0; c < Curves; c++)
            for (int s = 1; s < Steps; s++)
                _speed[c, s] += Random(-400, 400);
    }

    // Pull the strings at u (0-1 along the width): the top `count` curves, each a bit less.
    void Pluck(float u, float strength, int count)
    {
        int at = (int)(u * Steps);
        for (int c = 0; c < count && c < Curves; c++)
            for (int s = Math.Max(1, at - 4); s <= Math.Min(Steps - 1, at + 4); s++)
                _speed[c, s] -= strength * 20 * (1 - Math.Abs(s - at) / 5f) * (1 - c / (float)(count + 1));
    }

    // --- every frame ----------------------------------------------------------------

    public override void Draw()
    {
        Clear();
        float dt = Math.Min(DeltaTime, 1 / 30f);
        float t = Millis() / 1000f;
        _jagged = Math.Max(0, _jagged - dt / 2);

        Shake(dt);
        Ripple(dt);

        float band = Height * 0.4f;                   // the ribbons live in the bottom 40 %
        float top = Height - band;
        float amplitude = band / Curves * 1.3f;

        // Every curve's points first, so the ribbons and the lines share them.
        var pts = new float[Curves, Steps + 1];
        for (int c = 0; c < Curves; c++)
        {
            float baseY = top + (c + 0.7f) * band / Curves + _offset[c];
            for (int s = 0; s <= Steps; s++)
            {
                float u = s / (float)Steps;
                float y = baseY + Wobble(c, u, t) * amplitude + _wave[c, s];
                pts[c, s] = Finger(u * Width, y);
            }
        }

        DrawRibbons(pts);
        DrawLines(pts, t);
    }

    // The window's motion shakes each curve's spring; lower curves are softer, so they lag.
    void Shake(float dt)
    {
        var a = TargetAcceleration;
        _slide += TargetVelocity.X * dt * 0.0025f;
        for (int c = 0; c < Curves; c++)
        {
            float stiffness = Lerp(90, 25, c / (float)(Curves - 1));
            _offsetSpeed[c] += (-a.Y * 0.05f - stiffness * _offset[c] - 4 * _offsetSpeed[c]) * dt;
            _offset[c] = Constrain(_offset[c] + _offsetSpeed[c] * dt, -Height * 0.3f, Height * 0.3f);
        }
    }

    // The strings: each point pulled towards its neighbors, waves travel and fade.
    void Ripple(float dt)
    {
        const float Tension = 2400, Damping = 1.6f;
        for (int c = 0; c < Curves; c++)
        {
            for (int s = 1; s < Steps; s++)
            {
                float pull = (_wave[c, s - 1] + _wave[c, s + 1] - 2 * _wave[c, s]) * Tension;
                _speed[c, s] += (pull - _speed[c, s] * Damping - _wave[c, s] * 6) * dt;
            }
            for (int s = 1; s < Steps; s++)
                _wave[c, s] = Constrain(_wave[c, s] + _speed[c, s] * dt, -200, 200);
        }
    }

    // The wobbly function: a few sines of different speeds, plus noise when jagged.
    float Wobble(int curve, float u, float t)
    {
        float k = curve * 0.37f + _seed;
        float v = u * 6 + _slide;
        float y = Sin(v * (0.9f + 0.15f * (curve % 5)) + t * (0.5f + 0.05f * curve) + k)
                + 0.55f * Sin(v * (2.1f + 0.1f * (curve % 3)) - t * 1.1f + k * 1.7f)
                + 0.3f * Sin(v * 4.3f + t * 1.9f + k * 0.3f) * Sin(t * 0.37f + curve);
        if (_jagged > 0)
            y += (Noise(u * 30 + k, t * 5) - 0.5f) * 7 * _jagged;
        return y;
    }

    // The cursor pushes the lines away from it, like a finger in water.
    float Finger(float x, float y)
    {
        if (MouseX < 0 || MouseX >= Width || MouseY < 0 || MouseY >= Height) return y;
        float dx = (x - MouseX) / 90f, dy = y - MouseY;
        float near = Exp(-dx * dx) * Exp(-dy * dy / (70f * 70f));
        return y + dy / (Math.Abs(dy) + 25) * 45 * near;   // smooth on both sides of the cursor
    }

    // Teal to violet down the stack; a failed command turns it red (going on through magenta, not back through green).
    float Hue(int c, float u) => Lerp(Lerp(185, 300, c / (float)Curves) + 25 * Sin(u * 3 + c * 0.2f), 360, _jagged) % 360;

    // Between each curve and the next, a see-through ribbon: one polygon per band
    // (along one curve, back along the next), alternating light and dark.
    void DrawRibbons(float[,] pts)
    {
        NoSmooth();   // faster for big shapes; the lines drawn on top hide the edges
        NoStroke();
        for (int c = 0; c < Curves - 1; c++)
        {
            Fill(Hue(c, 0.5f), 75 + 25 * _jagged, c % 2 == 0 ? 95 : 55, 34 + 30 * _jagged);
            BeginShape();
            for (int s = 0; s <= Steps; s++)
                Vertex(s * Width / (float)Steps, pts[c, s]);
            for (int s = Steps; s >= 0; s--)
                Vertex(s * Width / (float)Steps, pts[c + 1, s]);
            EndShape(true);
        }
        Smooth();
    }

    // The lines themselves, brighter where the light passes along the stack.
    void DrawLines(float[,] pts, float t)
    {
        NoFill();
        StrokeWeight(1.3f);
        for (int c = 0; c < Curves; c++)
        {
            float bright = 0.6f + 0.4f * Sin(t * 1.3f + c * 0.5f);
            Stroke(Hue(c, 0.5f), 35 + 50 * _jagged, 100, 140 + 100 * bright);
            BeginShape();
            for (int s = 0; s <= Steps; s++)
                Vertex(s * Width / (float)Steps, pts[c, s]);
            EndShape();
        }
    }
}
";

        private const string TerminalMorphBlob =
@"// A morphing blob in the terminal, the way CSS does it: a box whose eight
// border-radius values (four corners, each with a horizontal and a vertical
// radius) are animated between a few keyframes, filled with a gradient,
// slowly turning, with a soft glow. Floating behind Windows Terminal, it also
// feels the window: it lags and stretches when you drag it, leans towards
// the cursor, pulses as you type and changes mood with your commands.
//
// Inspired by ""Create Smooth Blob Animation with CSS (No JavaScript)"" by
// Code With Nazia (youtube.com/watch?v=eXlzVo90Jr4): border-radius tricks +
// keyframe animations. Written from scratch for Dana.
//
// In CSS it would be:
//   border-radius: 60% 40% 30% 70% / 60% 30% 70% 40%;
//   animation: morph 8s ease-in-out infinite;
// Each corner is a quarter of an ellipse; here it's drawn with BezierVertex.
//
// 1. Open Windows Terminal and press Run.
// 2. Drag the terminal: the blob trails behind, stretches along the motion and
//    morphs faster; let go and it wobbles back into place.
// 3. Type: every key gives it a little pulse. Move the cursor near it: it leans in.
// 4. A command that fails turns it red and makes it quiver; one that works
//    makes it swell in a green-blue flash.
public class TerminalMorphBlob : AttachedSketch
{
    // border-radius keyframes, in % of the box: horizontal radii of the
    // top-left, top-right, bottom-right, bottom-left corners, then the vertical ones.
    readonly float[][] _keyframes =
    {
        new float[] { 60, 40, 30, 70,   60, 30, 70, 40 },
        new float[] { 30, 60, 70, 40,   50, 60, 30, 60 },
        new float[] { 50, 50, 34, 66,   56, 68, 32, 44 },
        new float[] { 70, 30, 52, 48,   38, 52, 48, 62 },
    };
    const float SecondsPerKeyframe = 2.2f;

    float _morph;                   // position in the keyframe cycle (keyframes)
    float _spin;                    // rotation, radians
    float _pulse;                   // a key or a good command: 0..1, fades
    float _anger;                   // a failed command: 1, fades
    float _joy;                     // a command that worked: 1, fades
    float _x, _y, _vx, _vy;         // where the blob is (it trails the window)

    public override void Settings() => Attach(WindowTarget.Terminal);

    public override void Setup()
    {
        ListenToShell();
        ListenToTargetKeys();
        _x = Width * 0.68f;
        _y = Height * 0.5f;
    }

    public override void KeyTyped() => _pulse = Math.Min(1, _pulse + 0.35f);

    public override void CommandFinished()
    {
        if (LastCommandOk) { _joy = 1; _pulse = 1; }
        else _anger = 1;
    }

    public override void Draw()
    {
        Clear();
        float dt = Math.Min(DeltaTime, 1 / 30f);
        _pulse = Math.Max(0, _pulse - dt * 2.5f);
        _anger = Math.Max(0, _anger - dt / 2.5f);
        _joy = Math.Max(0, _joy - dt / 1.5f);

        // --- where it is: a spring towards its home spot, pushed by the window's motion ---
        var a = TargetAcceleration;
        float homeX = Width * 0.68f, homeY = Height * 0.5f;
        bool mouseInside = MouseX >= 0 && MouseX < Width && MouseY >= 0 && MouseY < Height;
        if (mouseInside)
        {
            // Lean towards the cursor, but only so far.
            homeX += Constrain((MouseX - homeX) * 0.15f, -60, 60);
            homeY += Constrain((MouseY - homeY) * 0.15f, -60, 60);
        }
        _vx += (-a.X * 0.35f + (homeX - _x) * 18 - _vx * 4.5f) * dt;
        _vy += (-a.Y * 0.35f + (homeY - _y) * 18 - _vy * 4.5f) * dt;
        _x += _vx * dt;
        _y += _vy * dt;

        // --- the animation: faster while it moves or is upset ---
        float speed = Sqrt(_vx * _vx + _vy * _vy);
        float tempo = 1 + Math.Min(3, speed / 300) + 3 * _anger;
        _morph += dt * tempo / SecondsPerKeyframe;
        _spin += dt * (0.25f + 0.8f * _anger);

        // --- size, squash and stretch along the motion ---
        float size = Math.Min(Width, Height) * 0.5f * (1 + 0.12f * _pulse + 0.15f * _joy);
        float stretch = 1 + Math.Min(0.6f, speed / 1500);
        float heading = speed > 1 ? Atan2(_vy, _vx) : 0;
        float quiver = _anger * 0.06f * Sin(Millis() * 0.05f);

        float[] radii = Radii(_morph);
        PushMatrix();
        Translate(_x, _y);
        Rotate(heading);
        Scale(stretch * (1 + quiver), (1 / stretch) * (1 - quiver));   // stretched along its motion, same area
        Rotate(_spin - heading);

        // Glow: the same shape a few times, bigger and fainter (a CSS box-shadow, by hand).
        NoStroke();
        for (int k = 4; k >= 1; k--)
        {
            Fill(Lerp(150, 255, _anger), Lerp(110, 60, _anger) + 80 * _joy, Lerp(255, 90, _anger), 10);
            Blob(size * (1 + 0.07f * k), size * 0.9f * (1 + 0.07f * k), radii);
        }

        // The blob: a gradient that turns with time, like `background: linear-gradient(...)`.
        float g = size * 0.6f, turn = Millis() / 3000f;
        Color from = Color(Lerp(110, 255, _anger), Lerp(90, 70, _anger) + 120 * _joy, Lerp(255, 60, _anger), 120);
        Color to = Color(Lerp(255, 255, _anger), Lerp(90, 170, _anger) + 60 * _joy, Lerp(200, 40, _anger), 120);
        Fill(255);   // a gradient is tinted by the fill's alpha: start from opaque white
        LinearGradientFill(-Cos(turn) * g, -Sin(turn) * g, Cos(turn) * g, Sin(turn) * g, from, to);
        Blob(size, size * 0.9f, radii);

        // A soft highlight and a crisp rim.
        Fill(255);
        RadialGradientFill(-size * 0.15f, -size * 0.18f, size * 0.35f, Color(255, 255, 255, 60), Color(255, 255, 255, 0));
        Blob(size, size * 0.9f, radii);
        NoFill();
        Stroke(255, 255, 255, 90);
        StrokeWeight(1.2f);
        Blob(size, size * 0.9f, radii);
        PopMatrix();
    }

    // The eight radii at this point of the cycle: ease-in-out between keyframes,
    // like `animation-timing-function: ease-in-out`.
    float[] Radii(float morph)
    {
        int n = _keyframes.Length;
        int i = (int)Math.Floor(morph) % n;
        float u = morph - (float)Math.Floor(morph);
        u = u * u * (3 - 2 * u);
        var from = _keyframes[i];
        var to = _keyframes[(i + 1) % n];
        var r = new float[8];
        for (int k = 0; k < 8; k++) r[k] = Lerp(from[k], to[k], u);
        return r;
    }

    // A w×h box centered on (0, 0) with CSS border-radius `radii` (in %).
    void Blob(float w, float h, float[] radii)
    {
        // Horizontal radii in px, vertical radii in px.
        float tlh = radii[0] / 100 * w, trh = radii[1] / 100 * w, brh = radii[2] / 100 * w, blh = radii[3] / 100 * w;
        float tlv = radii[4] / 100 * h, trv = radii[5] / 100 * h, brv = radii[6] / 100 * h, blv = radii[7] / 100 * h;

        // CSS rule: if two radii on one side add up to more than that side, all
        // radii shrink by the same factor until they fit.
        float f = Math.Min(1, Math.Min(Math.Min(w / (tlh + trh), w / (blh + brh)), Math.Min(h / (tlv + blv), h / (trv + brv))));
        tlh *= f; trh *= f; brh *= f; blh *= f; tlv *= f; trv *= f; brv *= f; blv *= f;

        float l = -w / 2, r = w / 2, t = -h / 2, b = h / 2;
        const float K = 0.5523f;   // how far a cubic Bézier's handles go to draw a quarter ellipse

        BeginShape();
        Vertex(l + tlh, t);
        Vertex(r - trh, t);
        BezierVertex(r - trh + trh * K, t, r, t + trv - trv * K, r, t + trv);   // top-right corner
        Vertex(r, b - brv);
        BezierVertex(r, b - brv + brv * K, r - brh + brh * K, b, r - brh, b);   // bottom-right
        Vertex(l + blh, b);
        BezierVertex(l + blh - blh * K, b, l, b - blv + blv * K, l, b - blv);   // bottom-left
        Vertex(l, t + tlv);
        BezierVertex(l, t + tlv - tlv * K, l + tlh - tlh * K, t, l + tlh, t);   // top-left
        EndShape(true);
    }
}
";

        private const string TerminalAudio =
@"// nuget: NAudio, 2.2.1
#nullable enable

using System;
using NAudio.Wave;

// Audio-reactive terminal: whatever your computer is playing moves a row of
// 40 glowing blobs along the bottom of Windows Terminal -- low notes on the
// left, high ones on the right. It's the ""Audio-reactive: live FFT"" sample
// moved onto the terminal: the same capture (WasapiLoopbackCapture: the
// system's output, no microphone) and FFT with log-spaced bands, and the
// same idea that silence sets the blobs free.
//
// 1. Open Windows Terminal, play some music, press Run.
// 2. Each blob rides a stalk from the bottom edge; its height is its band's loudness.
// 3. When the music stops the blobs come loose and float up, drifting; when it
//    starts again they ease back onto their stalks.
// 4. Drag the terminal: the blobs swing on their stalks (inertia).
// 5. A command that fails flings them up in red; one that works sends a wave
//    of light along the row.
// Capture starts in Setup() and stops in OnStop() (Stop, or the window closing).
public class TerminalAudio : AttachedSketch
{
    const int FftSize = 1024;           // must be a power of two
    const int FftLog2 = 10;
    const int Bars = 40;
    const int MinBin = 1;               // skip bin 0 (DC offset)
    const int MaxBin = 400;             // ~18.7 kHz at 48 kHz
    const float Gain = 26f;
    const float Decay = 0.85f;          // per FFT: bars settle instead of flickering
    const float SilenceEnter = 0.06f, SilenceExit = 0.08f;   // hysteresis: no flicker in quiet passages

    // Log-spaced bands (like a real spectrum analyzer): BinBoundaries[b]..[b+1] feed bar b.
    static readonly int[] BinBoundaries = BuildBinBoundaries();

    static int[] BuildBinBoundaries()
    {
        var bounds = new int[Bars + 1];
        for (int b = 0; b <= Bars; b++)
            bounds[b] = (int)MathF.Round(MinBin * MathF.Pow((float)MaxBin / MinBin, (float)b / Bars));
        for (int b = 1; b <= Bars; b++)
            if (bounds[b] <= bounds[b - 1]) bounds[b] = bounds[b - 1] + 1;
        return bounds;
    }

    // --- capture thread -> Draw() ---
    WasapiLoopbackCapture? _capture;
    readonly NAudio.Dsp.Complex[] _fft = new NAudio.Dsp.Complex[FftSize];
    int _fftWritePos;
    readonly float[] _bars = new float[Bars];
    readonly object _lock = new object();
    string _status = """";

    // --- per blob, Draw() only ---
    readonly float[] _level = new float[Bars];       // smoothed loudness 0..1
    readonly float[] _ox = new float[Bars], _oy = new float[Bars];   // offset from the stalk tip (flight / swing)
    readonly float[] _vx = new float[Bars], _vy = new float[Bars];
    float _free;                         // 0 = on the stalks, 1 = floating free (silence)
    bool _silent = true;
    float _anger, _wave = -1;

    public override void Settings() => Attach(WindowTarget.Terminal);

    public override void Setup()
    {
        ListenToShell();
        ColorMode(ColorSpaceMode.HSB, 360, 100, 100, 255);
        StartCapture();
    }

    public override void OnStop() => StopCapture();

    public override void CommandFinished()
    {
        if (LastCommandOk) { _wave = 0; return; }
        _anger = 1;
        for (int i = 0; i < Bars; i++) { _vy[i] -= Random(400, 900); _vx[i] += Random(-200, 200); }
    }

    public override void Draw()
    {
        Clear();
        float dt = Math.Min(DeltaTime, 1 / 30f);
        float t = Millis() / 1000f;
        _anger = Math.Max(0, _anger - dt / 2);
        if (_wave >= 0 && (_wave += dt * 1.6f) > 1.3f) _wave = -1;

        float[] snapshot;
        lock (_lock) snapshot = (float[])_bars.Clone();

        float overall = 0;
        for (int i = 0; i < Bars; i++)
        {
            float target = Constrain(snapshot[i], 0, 1);
            _level[i] = Lerp(_level[i], target, Math.Min(1, dt * (target > _level[i] ? 25 : 8)));   // fast attack, slower release
            overall += _level[i];
        }
        overall /= Bars;
        if (_silent && overall > SilenceExit) _silent = false;
        if (!_silent && overall < SilenceEnter) _silent = true;
        _free = Lerp(_free, _silent ? 1 : 0, Math.Min(1, dt * (_silent ? 1.2f : 3)));

        var a = TargetAcceleration;
        float gap = Width / (float)Bars;
        float maxLen = Height * 0.45f;

        for (int i = 0; i < Bars; i++)
        {
            // Flight: a spring back to the stalk tip (weak while free), a noise wander
            // while free, and the window's motion felt as inertia.
            float spring = Lerp(60, 0.3f, _free);
            float wander = 260 * _free;
            float heading = i * 2.4f + (Noise(i * 0.7f, t * 0.35f) - 0.5f) * 8;
            _vx[i] += (-a.X * 0.3f - _ox[i] * spring + Cos(heading) * wander - _vx[i] * 2.2f) * dt;
            _vy[i] += (-a.Y * 0.3f - _oy[i] * spring + Sin(heading) * wander - 380 * _free - _vy[i] * 2.2f) * dt;   // free blobs rise
            _ox[i] += _vx[i] * dt;
            _oy[i] += _vy[i] * dt;
        }

        NoStroke();
        for (int i = 0; i < Bars; i++)
        {
            float baseX = (i + 0.5f) * gap, baseY = Height;
            float len = Lerp(gap * 0.5f, maxLen, _level[i]);
            float hx = Constrain(baseX + _ox[i], 0, Width), hy = Constrain(baseY - len + _oy[i], gap * 0.5f, Height);
            if (hy <= gap * 0.5f && _vy[i] < 0) _vy[i] = -_vy[i] * 0.5f;   // bounce off the top edge
            float lit = _wave >= 0 ? Exp(-Sq((i / (float)Bars - _wave) / 0.08f)) : 0;
            float hue = Lerp(Map(i, 0, Bars, 170, 300), 360, _anger) % 360;
            float r = gap * (0.28f + 0.35f * _level[i]) * (1 + 0.6f * lit);

            // The stalk: a tapered quad from the bottom edge to the head, fading as the blob floats away.
            float stalk = 1 - _free;
            if (stalk > 0.02f)
            {
                float dx = hx - baseX, dy = hy - baseY, d = Math.Max(1, Sqrt(dx * dx + dy * dy));
                float nx = -dy / d, ny = dx / d, w0 = gap * 0.18f, w1 = r * 0.5f;
                Fill(hue, 70, 90, 90 * stalk);
                Quad(baseX + nx * w0, baseY + ny * w0, hx + nx * w1, hy + ny * w1,
                     hx - nx * w1, hy - ny * w1, baseX - nx * w0, baseY - ny * w0);
            }

            // The head: a glow faked the classic 2D way -- the same circle, bigger and fainter.
            for (int k = 4; k >= 1; k--)
            {
                Fill(hue, 70, 100, (14 + 30 * lit) * (0.5f + _level[i]));
                Circle(hx, hy, r * 2 * (1 + 0.45f * k));
            }
            Fill(hue, 45 - 30 * lit, 100, 220);
            Circle(hx, hy, r * 2);
        }

        if (_status.Length > 0)
        {
            Fill(0, 0, 100, 160);
            TextSize(12);
            TextAlign(TextAlignH.Right, TextAlignV.Bottom);
            Text(_status, Width - 8, Height - 6);
        }
    }

    // --- capture -----------------------------------------------------------------

    void StartCapture()
    {
        if (!OperatingSystem.IsWindows()) { _status = ""audio del sistema: solo Windows""; return; }
        try
        {
            var capture = new WasapiLoopbackCapture();
            capture.DataAvailable += (s, e) => OnAudio(e.Buffer, e.BytesRecorded, capture.WaveFormat);
            capture.StartRecording();
            _capture = capture;
            _status = """";
        }
        catch (Exception ex)
        {
            _status = ""sin audio: "" + ex.Message;
        }
    }

    void StopCapture()
    {
        try { _capture?.StopRecording(); _capture?.Dispose(); } catch { }
        _capture = null;
    }

    // Capture thread: mix to mono, window it, and every FftSize samples run the FFT.
    void OnAudio(byte[] buffer, int bytes, WaveFormat format)
    {
        int bytesPerSample = format.BitsPerSample / 8, channels = format.Channels;
        int frameSize = bytesPerSample * channels;
        if (bytesPerSample != 4) return;   // loopback is 32-bit float
        for (int i = 0; i < bytes / frameSize; i++)
        {
            float sum = 0;
            for (int ch = 0; ch < channels; ch++)
                sum += BitConverter.ToSingle(buffer, i * frameSize + ch * bytesPerSample);
            _fft[_fftWritePos].X = (float)(sum / channels * NAudio.Dsp.FastFourierTransform.HammingWindow(_fftWritePos, FftSize));
            _fft[_fftWritePos].Y = 0;
            if (++_fftWritePos >= FftSize)
            {
                _fftWritePos = 0;
                NAudio.Dsp.FastFourierTransform.FFT(true, FftLog2, _fft);
                UpdateBars();
            }
        }
    }

    void UpdateBars()
    {
        lock (_lock)
            for (int b = 0; b < Bars; b++)
            {
                int start = BinBoundaries[b], end = BinBoundaries[b + 1];
                float sum = 0;
                for (int k = start; k < end; k++)
                    sum += Mag(_fft[k].X, _fft[k].Y);
                float value = Sqrt(sum / (end - start)) * Gain * Map(b, 0, Bars, 0.75f, 2.1f);   // a mild ramp: highs are quieter
                _bars[b] = Math.Max(value, _bars[b] * Decay);
            }
    }
}
";
    }
}
