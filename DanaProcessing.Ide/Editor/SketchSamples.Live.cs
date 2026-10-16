using DanaProcessing.Ide.Localization;

namespace DanaProcessing.Ide.Editor
{
    /// <summary>
    /// Samples for the "play it live / generative art / day to day" release:
    /// MIDI learn, Beat()/Phase(), the microphone, seeds and variations, print
    /// export, the performance panel and the data/ folder. All lists them after
    /// the Extras (see SketchSamples.Extras.cs).
    /// </summary>
    public static partial class SketchSamples
    {
        private static readonly SketchSample[] Live =
        {
            new SketchSample(
                Loc.Tr("MIDI learn: una flor que se toca con perillas", "MIDI learn: a flower you play with knobs"),
                Loc.Tr("Conectá cualquier controlador MIDI: clic en MIDI en el panel [Tweak], tocá un slider y girá una perilla -- quedan conectados y desde ahí la flor se toca con hardware (las conexiones duran toda la sesión, también con Run y Hot Reload). Los pads y teclas lanzan destellos con OnMidi() según la velocidad del golpe, y la rueda de pitch bend tuerce la flor. Sin controlador: los sliders andan con el mouse y las teclas 1-8 simulan pads.",
                       "Plug in any MIDI controller: click MIDI in the [Tweak] panel, touch a slider and turn a knob -- they're connected, and from then on the flower is played with hardware (connections last the whole session, through Run and Hot Reload too). Pads and keys fire bursts through OnMidi() sized by how hard you hit, and the pitch-bend wheel twists the flower. No controller? The sliders work with the mouse and keys 1-8 fake pad hits."),
                MidiLearnFlower,
                new[] { SampleTag.MidiOsc, SampleTag.Hardware, SampleTag.Interaction, SampleTag.LiveCoding }),

            new SketchSample(
                Loc.Tr("Visuales en el tiempo: Beat(), Phase() y easings", "Visuals in time: Beat(), Phase() and easings"),
                Loc.Tr("Dos live loops tocan a 110 BPM y todo lo que se mueve está amarrado a ese reloj sin una sola cuenta a mano: Pulse() hace latir el anillo en cada golpe, BeatTween() con Ease.OutBack desliza los bloques y aterrizan justo en el siguiente beat, OnBeat(4) cambia la paleta en cada compás y Beat() marca el paso del secuenciador. Cambiá Bpm en Setup() y apretá ⚡: la animación sigue a la música.",
                       "Two live loops play at 110 BPM and everything that moves is tied to that clock without a single hand-written calculation: Pulse() makes the ring throb on every hit, BeatTween() with Ease.OutBack slides the blocks so they land exactly on the next beat, OnBeat(4) changes the palette every bar and Beat() drives the sequencer cursor. Change Bpm in Setup() and press ⚡: the animation follows the music."),
                BeatVisuals,
                new[] { SampleTag.Audio, SampleTag.LiveCoding, SampleTag.Animation }),

            new SketchSample(
                Loc.Tr("Micrófono: la sala hace bailar el espectro", "Microphone: the room makes the spectrum dance"),
                Loc.Tr("ListenTo(AudioSource.Microphone) y Amplitude, Spectrum() y Waveform() escuchan el mundo real en vez del sonido del sketch: hablá, aplaudí o poné música cerca de la compu. Un aplauso (un salto de volumen) lanza chispas. El slider gain sube el micrófono (MicGain) y la tecla S alterna entre micrófono, el sonido del sketch y los dos a la vez. La primera vez, el sistema puede pedir permiso para usar el micrófono.",
                       "ListenTo(AudioSource.Microphone) makes Amplitude, Spectrum() and Waveform() hear the real world instead of the sketch's own sound: talk, clap or play music near the computer. A clap (a jump in loudness) fires sparks. The gain slider boosts the mic (MicGain) and the S key switches between the microphone, the sketch's sound and both. The first time, the OS may ask for microphone permission."),
                MicSpectrum,
                new[] { SampleTag.Audio, SampleTag.Hardware, SampleTag.Interaction }),

            new SketchSample(
                Loc.Tr("Paisajes con semilla: 🎲, 🔒 y hoja de variaciones", "Seeded landscapes: 🎲, 🔒 and the variations sheet"),
                Loc.Tr("Un paisaje generativo hecho solo con Random() y Noise(), así que la semilla lo decide todo. F6 (🎲) tira otra semilla, Sketch > Mantener la semilla la fija, y Ctrl+Shift+V abre la hoja de variaciones: 9 o 16 paisajes con semillas o valores de [Tweak] distintos; clic en el mejor y se queda con sus valores. La tecla S guarda un PNG que lleva adentro la semilla y los sliders -- Sketch > Abrir semilla desde PNG lo vuelve a dibujar igual.",
                       "A generative landscape made only with Random() and Noise(), so the seed decides everything. F6 (🎲) rolls another seed, Sketch > Keep the seed locks it, and Ctrl+Shift+V opens the variations sheet: 9 or 16 landscapes with different seeds or [Tweak] values; click the best one and keep its values. The S key saves a PNG that carries the seed and the sliders inside -- Sketch > Open seed from PNG draws it again exactly."),
                SeededLandscape,
                new[] { SampleTag.Generative, SampleTag.Images, SampleTag.Files }),

            new SketchSample(
                Loc.Tr("Para plotter: líneas de flujo a dos plumas", "For a plotter: flow lines with two pens"),
                Loc.Tr("Arte hecho solo de líneas, pensado para una plotter de pluma (AxiDraw y similares) o para imprimir grande: cientos de trazos que siguen un campo de Noise(), en dos colores. Archivo > Exportar para imprimir (Ctrl+Shift+E) lo saca como SVG para plotter -- una capa por pluma y los trazos reordenados para que la pluma viaje lo menos posible en el aire -- o como PNG a 8x para un póster. La semilla decide el dibujo: F6 para otro.",
                       "Art made only of lines, meant for a pen plotter (AxiDraw and friends) or a big print: hundreds of strokes following a Noise() field, in two colors. File > Export for print (Ctrl+Shift+E) saves it as a plotter SVG -- one layer per pen and the strokes reordered so the pen travels as little as possible in the air -- or as an 8x PNG for a poster. The seed decides the drawing: F6 for another one."),
                PlotterFlow,
                new[] { SampleTag.Generative, SampleTag.Files, SampleTag.Shapes }),

            new SketchSample(
                Loc.Tr("Panel de rendimiento (F3): ¿por qué va lento?", "Performance panel (F3): why is it slow?"),
                Loc.Tr("F3 (o ShowStats()) muestra fps, cuánto tarda Draw(), cuánta memoria pide cada frame y las recolecciones de basura, con un gráfico de los últimos frames contra el presupuesto de 16,7 ms. Subí partículas con el slider para ver dónde se rompe, y activá 'garbage' para ver el costo de crear listas nuevas en cada frame en vez de reusarlas.",
                       "F3 (or ShowStats()) shows fps, how long Draw() takes, how much memory each frame asks for and garbage collections, with a graph of the last frames against the 16.7 ms budget. Raise the particle slider to see where it breaks, and turn on 'garbage' to see the cost of new lists every frame instead of reusing them."),
                StatsDemo,
                new[] { SampleTag.Particles, SampleTag.Basics, SampleTag.Animation }),

            new SketchSample(
                Loc.Tr("Carpeta data/: un CSV y una foto al lado del sketch", "The data/ folder: a CSV and a photo next to the sketch"),
                Loc.Tr("Como en Processing, los archivos del sketch van en una carpeta data/ al lado del .cs: LoadTable(\"clima.csv\") o LoadImage(\"foto.jpg\") los encuentran con solo el nombre, y al exportar la app la carpeta viaja con ella, así que las mismas rutas funcionan. Guardá el sketch (Ctrl+S): la primera vez crea data/clima.csv con datos de ejemplo. Agregá una foto con Sketch > Agregar archivo a data/ y aparece de fondo.",
                       "Like Processing, a sketch's files go in a data/ folder next to its .cs: LoadTable(\"clima.csv\") or LoadImage(\"foto.jpg\") find them by name alone, and an exported app takes the folder with it, so the same paths keep working. Save the sketch (Ctrl+S): the first run creates data/clima.csv with sample data. Add a photo with Sketch > Add file to data/ and it shows up behind."),
                DataFolderChart,
                new[] { SampleTag.Data, SampleTag.Files, SampleTag.Images }),

            new SketchSample(
                Loc.Tr("Rebobinado de verdad: el Juego de la Vida", "Real rewind: the Game of Life"),
                Loc.Tr("Pausá con ⏸ debajo del canvas, arrastrá la línea de tiempo hacia atrás y apretá ⤺: el sketch vuelve al estado exacto de ese cuadro -- la grilla, la generación, el azar -- y sigue corriendo desde ahí. Pintá células con el mouse para que esta vez la historia salga distinta. El panel F3 muestra cuánto cuesta cada cuadro.",
                       "Pause with ⏸ under the canvas, drag the timeline back and press ⤺: the sketch goes back to the exact state of that frame -- the grid, the generation, the randomness -- and runs on from there. Paint cells with the mouse so the story turns out differently this time. The F3 panel shows what each frame costs."),
                LifeRewind,
                new[] { SampleTag.Generative, SampleTag.Interaction, SampleTag.Animation }),
        };

        private const string MidiLearnFlower =
@"// MIDI learn: play this flower with a MIDI controller.
//
// 1. Click MIDI in the [Tweak] panel (top right): it lights up.
// 2. Touch a slider, then turn a knob or move a fader: they're connected.
// 3. Do the same with as many sliders as you like, then click MIDI again.
//
// Connections last the whole session (Run and Hot Reload keep them). To make
// one permanent, write it in Setup(): MapMidi(""petals"", 21);
// Pads and keys fire bursts (OnMidi), sized by velocity; the pitch-bend wheel
// twists the whole flower. No controller? Keys 1-8 fake pad hits.
public class MySketch : Sketch
{
    [Tweak(3, 24)] int petals = 9;
    [Tweak(30, 260)] float radius = 150;
    [Tweak(0, 3)] float spin = 0.5f;
    [Tweak(0, 360)] float hue = 200;
    [Tweak(0, 1)] float wobble = 0.35f;
    [Tweak] bool trails = true;

    private readonly List<Burst> _bursts = new();
    private float _bend;   // -1..1 from the pitch-bend wheel
    private float _angle;
    private string _status = """";

    private sealed class Burst
    {
        public float X, Y, Size, Hue, Life = 1;
    }

    public override void Setup()
    {
        Size(720, 520);
        var inputs = MidiInputs();
        _status = inputs.Length == 0
            ? ""No MIDI controller found -- keys 1-8 fake pad hits""
            : ""MIDI: "" + string.Join("", "", inputs);
    }

    public override void OnMidi(MidiMessage m)
    {
        if (m.Kind == MidiKind.NoteOn)
            Fire(m.Note, m.Velocity / 127f);
        else if (m.Kind == MidiKind.PitchBend)
            _bend = m.Bend / 8192f;
    }

    public override void KeyPressed()
    {
        if (Key >= '1' && Key <= '8')
            Fire(36 + (Key - '1'), 0.8f);
    }

    private void Fire(int note, float strength)
    {
        float a = note * 0.7f;
        _bursts.Add(new Burst
        {
            X = Width / 2f + Cos(a) * radius * 0.8f,
            Y = Height / 2f + Sin(a) * radius * 0.8f,
            Size = 30 + strength * 160,
            Hue = (hue + note * 15) % 360,
        });
    }

    public override void Draw()
    {
        if (trails)
            Background(10, 12, 20, 40);
        else
            Background(10, 12, 20);

        _angle += spin * 0.02f;
        float t = Millis() / 1000f;

        PushMatrix();
        Translate(Width / 2f, Height / 2f);
        Rotate(_angle + _bend * 1.2f);
        NoFill();
        StrokeWeight(2);
        for (int ring = 0; ring < 4; ring++)
        {
            float r = radius * (1 - ring * 0.2f);
            StrokeHSB((hue + ring * 25) % 360, 70, 100, 200);
            BeginShape();
            for (int i = 0; i <= 200; i++)
            {
                float a = TWO_PI * i / 200;
                float petal = Sin(a * petals + t * 1.5f + ring) * wobble;
                float rr = r * (0.6f + 0.4f * Abs(Cos(a * petals / 2f))) * (1 + petal * 0.3f);
                Vertex(Cos(a) * rr, Sin(a) * rr);
            }
            EndShape(true);
        }
        PopMatrix();

        NoStroke();
        for (int i = _bursts.Count - 1; i >= 0; i--)
        {
            var b = _bursts[i];
            FillHSB(b.Hue, 60, 100, 180 * b.Life);
            Circle(b.X, b.Y, b.Size * (2 - b.Life));
            b.Life -= 0.03f;
            if (b.Life <= 0)
                _bursts.RemoveAt(i);
        }

        Fill(255, 160);
        TextSize(13);
        Text(_status, 14, Height - 16);
    }
}
";

        private const string BeatVisuals =
@"// Visuals in time with the music -- no math with Millis() or frame counts.
//
//   Beat()          where the music is, in beats (2.5 = halfway through beat 3)
//   Phase(n)        0 -> 1 ramp every n beats (0 = right on the beat)
//   Pulse(n)        1 on the beat, fading out before the next one
//   OnBeat(n)       true on the one frame a new beat (or every n beats) starts
//   BeatTween(a, b, Ease.OutBack)   a -> b, landing exactly on the next beat
//
// While sound plays they follow what you HEAR; the loops' first downbeat is
// beat 0 of a bar. Change Bpm and press Hot Reload: everything keeps up.
public class MySketch : Sketch
{
    private int _palette;
    private readonly float[] _hues = { 200, 330, 40, 160 };

    public override void Setup()
    {
        Size(720, 480);
        Bpm = 110;
    }

    [LiveLoop]
    async Task Drums()
    {
        for (int i = 0; i < 4; i++)
        {
            Sample(""kick"");
            if (i % 2 == 1) Sample(""snare"", amp: 0.7);
            await Sleep(0.5);
            Sample(""hat"", amp: 0.4);
            await Sleep(0.5);
        }
    }

    [LiveLoop]
    async Task Bass()
    {
        foreach (var note in new[] { ""E2"", ""E2"", ""G2"", ""A2"" })
        {
            UseSynth(Synth.Saw);
            Play(note, amp: 0.4, release: 0.5, cutoff: 75);
            await Sleep(1);
        }
    }

    public override void Draw()
    {
        if (OnBeat(4))
            _palette = (_palette + 1) % _hues.Length;  // a new color every bar
        float hue = _hues[_palette];

        Background(12, 12, 18);

        // 1. The ring throbs on every beat.
        float throb = Pulse(1, sharpness: 5);
        NoFill();
        StrokeWeight(6 + throb * 10);
        StrokeHSB(hue, 70, 100);
        Circle(180, 200, 140 + throb * 60);

        // 2. Blocks slide to a new spot on every beat, landing on the next one.
        NoStroke();
        for (int i = 0; i < 4; i++)
        {
            int step = (int)Beat() + i;
            float from = 360 + (step % 4) * 80;
            float to = 360 + ((step + 1) % 4) * 80;
            float x = BeatTween(from, to, Ease.OutBack);
            FillHSB((hue + i * 30) % 360, 60, 95);
            Rect(x, 90 + i * 60, 50, 40);
        }

        // 3. A 16-step sequencer row: the cursor is Beat() in sixteenths.
        int sixteenth = (int)(Beat() * 4) % 16;
        for (int i = 0; i < 16; i++)
        {
            bool on = i == sixteenth;
            if (on) FillHSB(hue, 50, 100); else Fill(255, i % 4 == 0 ? 70 : 35);
            Rect(40 + i * 40, 400, 32, on ? 40 : 24);
        }

        // 4. Bars and beats as text.
        Fill(255, 200);
        TextSize(14);
        Text($""bar {(int)Bar() + 1}   beat {(int)Beat() % 4 + 1}   {Bpm:0} BPM"", 40, 380);
    }
}
";

        private const string MicSpectrum =
@"// The microphone: Amplitude, Spectrum() and Waveform() hear the room.
//
// ListenTo(AudioSource.Microphone) switches them from the sketch's own sound
// to your default microphone. Talk, clap, whistle, play music nearby.
// S cycles: microphone -> sketch sound -> both. The microphone turns off by
// itself when the sketch stops.
public class MySketch : Sketch
{
    [Tweak(1, 12)] float gain = 4;
    [Tweak(0.02f, 0.4f)] float clapJump = 0.12f;

    private readonly float[] _bands = new float[48];
    private readonly float[] _smooth = new float[48];
    private readonly float[] _wave = new float[512];
    private readonly List<PVector> _sparks = new();
    private readonly List<PVector> _velocities = new();
    private float _lastLevel;

    public override void Setup()
    {
        Size(720, 520);
        ListenTo(AudioSource.Microphone);
    }

    public override void KeyPressed()
    {
        if (Key == 's')
            ListenTo(Listening switch
            {
                AudioSource.Microphone => AudioSource.Sketch,
                AudioSource.Sketch => AudioSource.Both,
                _ => AudioSource.Microphone,
            });
    }

    public override void Draw()
    {
        MicGain = gain;
        Background(8, 10, 16, 60);

        float level = Amplitude;
        if (level - _lastLevel > clapJump)
            Clap();
        _lastLevel = level;

        // Radial spectrum: one bar per band, smoothed so it breathes instead of flickers.
        Spectrum(_bands);
        PushMatrix();
        Translate(Width / 2f, Height / 2f);
        StrokeWeight(5);
        for (int i = 0; i < _bands.Length; i++)
        {
            _smooth[i] = Lerp(_smooth[i], _bands[i], 0.35f);
            float a = TWO_PI * i / _bands.Length - HALF_PI;
            float r0 = 90 + level * 200;
            float r1 = r0 + _smooth[i] * 170;
            StrokeHSB(190 + i * 3.5f, 70, 100);
            Line(Cos(a) * r0, Sin(a) * r0, Cos(a) * r1, Sin(a) * r1);
        }

        // The waveform as a ring inside.
        Waveform(_wave);
        NoFill();
        StrokeWeight(2);
        Stroke(255, 180);
        BeginShape();
        for (int i = 0; i < _wave.Length; i += 4)
        {
            float a = TWO_PI * i / _wave.Length;
            float r = 70 + _wave[i] * 120;
            Vertex(Cos(a) * r, Sin(a) * r);
        }
        EndShape(true);

        // Sparks from claps.
        NoStroke();
        for (int i = _sparks.Count - 1; i >= 0; i--)
        {
            _sparks[i].Add(_velocities[i]);
            _velocities[i].Mult(0.96f);
            FillHSB(30, 80, 100, 220);
            Circle(_sparks[i].X, _sparks[i].Y, 6);
            if (_velocities[i].Mag() < 0.3f)
            {
                _sparks.RemoveAt(i);
                _velocities.RemoveAt(i);
            }
        }

        PopMatrix();
        Fill(255, 170);
        TextSize(13);
        Text($""listening to: {Listening}   level {level:0.00}   (S to switch)"", 14, Height - 16);
        if (MicStatus.StartsWith(""No""))
            Text(MicStatus, 14, Height - 38);
    }

    private void Clap()
    {
        for (int i = 0; i < 40; i++)
        {
            _sparks.Add(new PVector(0, 0));
            _velocities.Add(PVector.FromAngle(Random(TWO_PI)).Mult(Random(4, 12)));
        }
    }
}
";

        private const string SeededLandscape =
@"// Seeded generative art: same seed, same landscape.
//
// Everything random here comes from Random() and Noise(), which follow the
// sketch's Seed -- so:
//   F6 (🎲)              a new seed (Sketch menu: keep the seed to lock it)
//   Ctrl+Shift+V         the variations sheet: pick the best of 9 or 16
//   S                    save a PNG; it remembers the seed and the sliders
//                        (Sketch > Open seed from PNG... brings them back)
//
// RandomSeed(Seed) at the top of Draw() makes every frame repeat the same
// random numbers, so moving a slider changes only what that slider controls.
public class MySketch : Sketch
{
    [Tweak(2, 8)] int layers = 5;
    [Tweak(0.5f, 6)] float roughness = 2.2f;
    [Tweak(0, 360)] float hue = 215;
    [Tweak(0, 400)] int stars = 160;
    [Tweak] bool sun = true;

    public override void Setup() => Size(800, 500);

    public override void Draw()
    {
        RandomSeed(Seed);

        // Sky: a vertical gradient.
        for (int y = 0; y < Height; y += 4)
        {
            float t = y / (float)Height;
            FillHSB(hue, 50 - t * 20, 18 + t * 30);
            NoStroke();
            Rect(0, y, Width, 4);
        }

        // Stars.
        for (int i = 0; i < stars; i++)
        {
            float x = Random(Width), y = Random(Height * 0.6f);
            Fill(255, Random(80, 230));
            Circle(x, y, Random(1, 3));
        }

        // Sun (or moon).
        if (sun)
        {
            FillHSB((hue + 180) % 360, 40, 100, 230);
            Circle(Random(120, Width - 120), Random(80, 200), Random(60, 120));
        }

        // Mountain layers, back to front: lighter and smoother far away.
        for (int layer = 0; layer < layers; layer++)
        {
            float depth = layer / (float)Math.Max(1, layers - 1);
            float baseY = Height * (0.45f + depth * 0.4f);
            float amp = 60 + depth * 90;
            float offset = Random(1000);
            FillHSB(hue, 35 + depth * 30, 70 - depth * 55);
            BeginShape();
            Vertex(0, Height);
            for (int x = 0; x <= Width; x += 6)
                Vertex(x, baseY - Noise(offset + x * 0.004f * roughness, layer * 10) * amp * 2 + amp);
            Vertex(Width, Height);
            EndShape(true);
        }

        Fill(255, 200);
        TextSize(13);
        Text($""seed {Seed}   ·   F6 new seed   ·   Ctrl+Shift+V variations   ·   S save PNG"", 14, Height - 14);
    }

    public override void KeyPressed()
    {
        if (Key == 's')
        {
            HideTweaks();
            SaveFrame($""landscape-{Seed}-####.png"");
            ShowTweaks();
            Println($""Saved landscape-{Seed}-{FrameCount:0000}.png (seed and sliders are inside the PNG)"");
        }
    }
}
";

        private const string PlotterFlow =
@"// Plotter-friendly art: only strokes, two pens.
//
// File > Export for print (Ctrl+Shift+E):
//   ""Plotter SVG""  one layer per color (= per pen), strokes reordered so the
//                  pen travels as little as possible between them
//   ""Big PNG""      2x-16x, drawn again as vectors: sharp at poster size
// Same seed, same drawing -- F6 rolls another one.
public class MySketch : Sketch
{
    [Tweak(50, 900)] int lines = 420;
    [Tweak(10, 160)] int steps = 70;
    [Tweak(0.5f, 6)] float zoom = 2.2f;
    [Tweak(0, 1)] float secondPen = 0.25f;

    public override void Setup()
    {
        Size(700, 500);
        FrameRate(12); // a still drawing: a few frames a second is plenty for the sliders
    }

    public override void Draw()
    {
        RandomSeed(Seed);
        Background(250, 248, 240);
        NoFill();
        StrokeWeight(1);

        for (int i = 0; i < lines; i++)
        {
            float x = Random(-50, Width + 50);
            float y = Random(-50, Height + 50);
            bool red = Random(1) < secondPen;
            if (red) Stroke(200, 40, 40); else Stroke(20, 20, 30);

            BeginShape();
            for (int s = 0; s < steps; s++)
            {
                Vertex(x, y);
                float angle = Noise(x / Width * zoom, y / Height * zoom) * TWO_PI * 2;
                x += Cos(angle) * 4;
                y += Sin(angle) * 4;
            }
            EndShape();
        }
    }

}
";

        private const string StatsDemo =
@"// The performance panel: press F3 (here it starts open via ShowStats()).
//
//   fps          frames per second actually reached (target: FrameRate())
//   Draw ms      how long your Draw() takes -- the bars; the line is the budget
//   KB/frame     memory Draw() allocates every frame
//   GC 0/1/2     garbage collections since the panel opened
//
// Raise ""particles"" until the bars go red. Then switch on ""garbage"": the same
// work, but a new List every frame -- watch KB/frame and the GC counter climb.
public class MySketch : Sketch
{
    [Tweak(100, 20000)] int particles = 3000;
    [Tweak] bool garbage = false;

    private readonly List<PVector> _pos = new();
    private readonly List<PVector> _vel = new();
    private readonly List<PVector> _reused = new();

    public override void Setup()
    {
        Size(720, 480);
        ShowStats();
    }

    public override void Draw()
    {
        while (_pos.Count < particles)
        {
            _pos.Add(new PVector(Random(Width), Random(Height)));
            _vel.Add(PVector.Random2D().Mult(Random(0.5f, 2)));
        }
        if (_pos.Count > particles)
        {
            _pos.RemoveRange(particles, _pos.Count - particles);
            _vel.RemoveRange(particles, _vel.Count - particles);
        }

        // The wasteful way allocates a fresh list (and fresh vectors) every frame.
        var visible = garbage ? new List<PVector>() : _reused;
        visible.Clear();

        for (int i = 0; i < _pos.Count; i++)
        {
            var p = _pos[i];
            p.Add(_vel[i]);
            if (p.X < 0) p.X += Width; else if (p.X > Width) p.X -= Width;
            if (p.Y < 0) p.Y += Height; else if (p.Y > Height) p.Y -= Height;
            visible.Add(garbage ? p.Copy() : p);
        }

        Background(14, 16, 24);
        NoStroke();
        Fill(120, 200, 255, 160);
        foreach (var p in visible)
            Circle(p.X, p.Y, 3);

        Fill(255, 180);
        TextSize(13);
        Text($""{particles} particles  ·  garbage {(garbage ? ""ON"" : ""off"")}  ·  F3 hides the panel"", 14, Height - 14);
    }
}
";

        private const string DataFolderChart =
@"// The data/ folder: files that belong to the sketch, found by name.
//
//   LoadTable(""clima.csv"", ""header"")   looks in data/ first, then next to the .cs
//   LoadImage(""foto.jpg"")               same
//   SaveTable(t, DataPath(""x.csv""))     DataPath() = the full path inside data/
//
// Exporting the sketch copies data/ next to the app, so these paths work there
// too. Save this sketch (Ctrl+S) first: an unsaved sketch has no folder yet.
public class MySketch : Sketch
{
    private Table? _table;
    private PImage? _photo;
    private string _note = """";
    private readonly string[] _months = { ""Jan"", ""Feb"", ""Mar"", ""Apr"", ""May"", ""Jun"", ""Jul"", ""Aug"", ""Sep"", ""Oct"", ""Nov"", ""Dec"" };

    public override void Setup()
    {
        Size(760, 460);

        if (SketchFolder == null)
        {
            _note = ""Save this sketch (Ctrl+S) to give it a data/ folder -- showing data made in memory meanwhile."";
            _table = MakeSampleTable();
            return;
        }

        // First run: write a CSV into data/ so there's something to load.
        if (!File.Exists(DataPath(""clima.csv"")))
            SaveTable(MakeSampleTable(), DataPath(""clima.csv""));

        _table = LoadTable(""clima.csv"", ""header"");
        if (File.Exists(DataPath(""foto.jpg"")))
            _photo = LoadImage(""foto.jpg"");
        _note = $""data/ = {DataFolder}"" + (_photo == null ? ""   (add foto.jpg there for a background)"" : """");
    }

    private Table MakeSampleTable()
    {
        var t = new Table();
        t.SetColumnTitle(0, ""month"");
        t.SetColumnTitle(1, ""temp"");
        t.SetColumnTitle(2, ""rain"");
        for (int m = 0; m < 12; m++)
        {
            var row = t.AddRow();
            row.SetString(""month"", _months[m]);
            row.SetFloat(""temp"", 22 + 6 * Sin((m - 1) / 12f * TWO_PI));
            row.SetFloat(""rain"", 40 + 120 * Abs(Sin((m - 3) / 12f * PI)));
        }
        return t;
    }

    public override void Draw()
    {
        Background(250, 247, 240);
        if (_photo != null)
        {
            Tint(70); // see-through
            Image(_photo, 0, 0, Width, Height);
            NoTint();
        }

        int n = _table!.RowCount;
        float left = 60, right = Width - 30, bottom = Height - 70, top = 50;
        float step = (right - left) / n;

        NoStroke();
        for (int i = 0; i < n; i++)
        {
            var row = _table.GetRow(i);
            float rain = row.GetFloat(""rain"");
            float h = Map(rain, 0, 200, 0, bottom - top);
            Fill(90, 150, 220, 180);
            Rect(left + i * step + 6, bottom - h, step - 12, h);
            Fill(60);
            TextSize(11);
            Text(row.GetString(""month""), left + i * step + 8, bottom + 16);
        }

        NoFill();
        Stroke(220, 90, 50);
        StrokeWeight(3);
        BeginShape();
        for (int i = 0; i < n; i++)
            Vertex(left + i * step + step / 2, Map(_table.GetRow(i).GetFloat(""temp""), 10, 35, bottom, top));
        EndShape();

        NoStroke();
        Fill(40);
        TextSize(15);
        Text(""Rain (bars) and temperature (line), from data/clima.csv"", left, 30);
        Fill(90);
        TextSize(11);
        Text(_note, 14, Height - 18);
    }
}
";

        private const string LifeRewind =
@"// Real rewind. Under the canvas:
//   ⏸  pause            the slider scrubs through the last frames
//   ⤺  continue from here: the sketch's fields (the grid, the generation,
//      even the random generator) go back to that frame, and it runs on --
//      the future after it is forgotten.
// Paint cells with the mouse (drag) to make the new future different.
public class MySketch : Sketch
{
    const int Cols = 90, Rows = 60, Cell = 8;
    bool[,] _grid = new bool[Cols, Rows];
    bool[,] _next = new bool[Cols, Rows];
    int _generation;

    public override void Setup()
    {
        Size(Cols * Cell, Rows * Cell + 30);
        FrameRate(15);
        for (int x = 0; x < Cols; x++)
            for (int y = 0; y < Rows; y++)
                _grid[x, y] = Random(1) < 0.22f;
    }

    public override void Draw()
    {
        Step();
        Background(16, 18, 26);
        NoStroke();
        for (int x = 0; x < Cols; x++)
            for (int y = 0; y < Rows; y++)
                if (_grid[x, y])
                {
                    FillHSB((x * 2 + _generation) % 360, 55, 95);
                    Rect(x * Cell, y * Cell, Cell - 1, Cell - 1);
                }

        Fill(255, 190);
        TextSize(13);
        Text($""generation {_generation}  ·  ⏸ then ⤺ under the canvas to rewind for real  ·  drag to paint"", 10, Height - 10);
    }

    void Step()
    {
        for (int x = 0; x < Cols; x++)
            for (int y = 0; y < Rows; y++)
            {
                int n = 0;
                for (int dx = -1; dx <= 1; dx++)
                    for (int dy = -1; dy <= 1; dy++)
                        if ((dx != 0 || dy != 0) && _grid[(x + dx + Cols) % Cols, (y + dy + Rows) % Rows])
                            n++;
                _next[x, y] = _grid[x, y] ? n is 2 or 3 : n == 3;
            }
        (_grid, _next) = (_next, _grid);
        _generation++;
    }

    public override void MouseDragged() => Paint();
    public override void MousePressed() => Paint();

    void Paint()
    {
        int cx = (int)(MouseX / Cell), cy = (int)(MouseY / Cell);
        for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                int x = cx + dx, y = cy + dy;
                if (x >= 0 && x < Cols && y >= 0 && y < Rows)
                    _grid[x, y] = true;
            }
    }
}
";
    }
}
