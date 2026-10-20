using DanaProcessing.Ide.Localization;

namespace DanaProcessing.Ide.Editor
{
    /// <summary>
    /// Samples added after 1.0 that need features from the same release
    /// (OnStop(), LoadImage(byte[]), runtimes/ support in `// nuget:`, Shade()).
    /// Kept in their own file so SketchSample.cs stays reviewable; All (in
    /// SketchSample.cs) lists these after the original catalog, followed by
    /// the ones in SketchSamples.Live.cs.
    /// </summary>
    public static partial class SketchSamples
    {
        // The 1.x additions, then the live-performance ones (SketchSamples.Live.cs).
        private static SketchSample[] Extras => ExtrasV1.Concat(Live).ToArray();

        private static readonly SketchSample[] ExtrasV1 =
        {
            new SketchSample(
                Loc.Tr("Arduino por puerto serie (System.IO.Ports)", "Arduino over serial (System.IO.Ports)"),
                Loc.Tr("La librería Serial de Processing en una línea `// nuget:`: el Arduino manda un número por línea (un potenciómetro, un sensor de luz...) y el sketch lo grafica; mantené apretado el mouse y el sketch prende el LED del Arduino. El código del Arduino está en los comentarios. Sin Arduino conectado, simula un sensor con Noise(). OnStop() cierra el puerto para que el próximo Run lo pueda abrir.",
                       "Processing's Serial library as one `// nuget:` line: the Arduino sends one number per line (a potentiometer, a light sensor...) and the sketch plots it; hold the mouse down and the sketch lights the Arduino's LED. The Arduino code is in the comments. No Arduino plugged in? It fakes a sensor with Noise(). OnStop() closes the port so the next Run can open it."),
                SerialArduino,
                new[] { SampleTag.Hardware, SampleTag.NuGet, SampleTag.Data, SampleTag.Interaction }),

            new SketchSample(
                Loc.Tr("Webcam: espejo, slit-scan y ecos (FlashCap)", "Webcam: mirror, slit-scan and echoes (FlashCap)"),
                Loc.Tr("Tu cámara con `// nuget: FlashCap` (sin dependencias nativas; Windows y Linux) y LoadImage(byte[]) para cada frame. Tecla 2, slit-scan: cada franja horizontal es un momento distinto -- arriba es ahora, abajo hace un segundo --, así que al moverte te estirás en el tiempo. 1 es un espejo, 3 deja ecos de colores. OnStop() suelta la cámara.",
                       "Your camera with `// nuget: FlashCap` (no native dependencies; Windows and Linux) and LoadImage(byte[]) for every frame. Key 2, slit-scan: every horizontal band is a different moment -- the top is now, the bottom a second ago -- so moving smears you through time. 1 is a mirror, 3 leaves colored echoes. OnStop() releases the camera."),
                WebcamSlitScan,
                new[] { SampleTag.Hardware, SampleTag.Images, SampleTag.NuGet }),

            new SketchSample(
                Loc.Tr("Pelotas que tocan notas: salida MIDI (NAudio)", "Bouncing notes: MIDI out (NAudio)"),
                Loc.Tr("Cada vez que una pelota pega en una pared suena una nota de una escala pentatónica por la salida MIDI 0 -- en Windows es el sintetizador que ya trae el sistema, así que suena sin configurar nada; con loopMIDI o un sinte USB toca tu DAW o tu hardware. Click agrega una pelota, click derecho la saca, y el panel [Tweak] cambia gravedad, nota base, instrumento y duración en vivo.",
                       "Every time a ball hits a wall, a note from a pentatonic scale plays on MIDI output 0 -- on Windows that's the synth the system already has, so it makes sound with zero setup; with loopMIDI or a USB synth it plays your DAW or hardware. Click adds a ball, right-click removes one, and the [Tweak] panel changes gravity, root note, instrument and note length live."),
                MidiBounce,
                new[] { SampleTag.Audio, SampleTag.MidiOsc, SampleTag.Hardware, SampleTag.NuGet }),

            new SketchSample(
                Loc.Tr("Shaders en vivo: plasma que escucha el ritmo (SkSL)", "Live shaders: a plasma that hears the beat (SkSL)"),
                Loc.Tr("Un fragment shader estilo Shadertoy, en SkSL, pintando cada píxel con Shade(codigo): iTime, iMouse, iAmplitude e iSpectrum[] se completan solos, y dos [LiveLoop] le dan un ritmo al que reaccionar. Cambiá un número del shader y apretá ⚡ Hot Reload (o guardá): el shader nuevo entra al instante sin cortar la música; si no compila, sigue el anterior y el error aparece abajo.",
                       "A Shadertoy-style fragment shader in SkSL painting every pixel with Shade(source): iTime, iMouse, iAmplitude and iSpectrum[] are filled in for you, and two [LiveLoop]s give it a beat to react to. Change a number in the shader and press ⚡ Hot Reload (or save): the new shader takes over instantly without stopping the music; if it doesn't compile, the old one keeps running and the error shows at the bottom."),
                LiveShader,
                new[] { SampleTag.LiveCoding, SampleTag.Audio, SampleTag.Generative, SampleTag.HotReload }),

            new SketchSample(
                Loc.Tr("Una IA aprende a escribir en ASCII", "An AI learns to write in ASCII"),
                Loc.Tr("Una red neuronal chiquita, escrita a mano en C# (sin NuGet ni descargas), aprende en vivo a dibujar la palabra que escribís: arranca en ruido y en unos segundos las letras salen del ASCII. Cada carácter es una pregunta a la red -- ¿cuánta tinta va acá? -- y brilla donde todavía está aprendiendo. Escribí otra palabra y la red desaprende una mientras aprende la otra; el mouse le dobla las entradas, la rueda muestra lo que imagina fuera de la palabra, Tab cambia los caracteres y Enter la hace olvidar todo.",
                       "A tiny neural network, written by hand in C# (no NuGet, no download), learns live to draw the word you type: it starts as noise and within seconds the letters rise out of the ASCII. Every character is one question to the network -- how much ink goes here? -- and it glows where the network is still learning. Type another word and it unlearns one while it learns the other; the mouse bends its inputs, the wheel shows what it imagines outside the word, Tab switches glyphs and Enter makes it forget everything."),
                AsciiMind,
                new[] { SampleTag.MachineLearning, SampleTag.Text, SampleTag.Generative, SampleTag.Interaction }),

        };

        private const string AsciiMind =
@"// A neural network learns to write a word, live, and you watch it think in
// ASCII.
//
// No download and no library: the whole network is the Brain class at the
// bottom, plain C#. It only knows one thing: given a point (x, y) on the
// screen, how much ink goes there? It starts with random weights, so the
// screen is noise. Every frame it looks at a few hundred random points of the
// word, sees how wrong it was, and nudges its ~2,300 weights a little
// (backpropagation + Adam). In a few seconds the letters come out of the noise.
//
// Each character on screen is one question to the network: the more ink it
// answers, the denser the glyph. Where its answer is still changing -- where
// it's learning right now -- the glyph glows.
//
// - Type: a new word. The network doesn't start over: it unlearns the old
//   word while it learns the new one, so one melts into the other.
// - Enter: forget everything (new random weights) and learn from zero.
// - Mouse: swirls the network's input around the pointer. The letters bend,
//   because the network doesn't store a picture -- it's a function of (x, y).
// - Wheel: zoom out to see what it imagines outside the word.
// - Tab: other glyphs.  Space: pause the learning.
public class AsciiMind : Sketch
{
    const int Cols = 96, Rows = 30;                  // the ASCII screen
    const float CellW = 10, CellH = 18;
    const int SubX = 3, SubY = 5;                    // the word is learned a bit finer than it's shown
    const int StepsPerFrame = 4, BatchSize = 128;    // how much it practices per frame

    readonly string[] glyphSets = { "" .:-=+*#%@"", "" .oO0@"", "" 01"", "" .,;!|I#"", "" ·•●"" };
    int glyphSet;

    Brain brain;
    readonly System.Random rng = new System.Random(7);
    float[] target;                                  // the word as ink, 0..1, tw x th
    int tw = Cols * SubX, th = Rows * SubY;
    string word = ""HOLA"";
    PFont mono, heavy;

    readonly float[] ink = new float[Cols * Rows];   // the network's last answer for each character
    readonly float[] glow = new float[Cols * Rows];  // how much that answer moved lately
    float zoom = 1, shownZoom = 1;
    bool paused;
    readonly float[] lossHistory = new float[120];
    int lossCount;

    // For the HUD (and the tests).
    public float Loss { get; private set; } = 0.25f;
    public int Steps { get; private set; }
    public string Word => word;
    public float InkAt(int col, int row) => ink[row * Cols + col];

    public override void Setup()
    {
        Size(960, 640);
        mono = CreateFont(""Consolas"", 16);
        heavy = CreateFont(""Arial Black"", 100);
        brain = new Brain(rng);
        Learn(word);
    }

    /// <summary>Paints the word offscreen and keeps its pixels: that's what the network has to match.</summary>
    public void Learn(string newWord)
    {
        word = newWord.ToUpperInvariant();
        string text = word.Length == 0 ? ""?"" : word;
        var pg = CreateGraphics(tw, th);
        pg.BeginDraw();
        pg.Background(0);
        pg.NoStroke();
        pg.Fill(255);
        pg.TextFont(heavy);
        pg.TextAlign(TextAlignH.Center, TextAlignV.Center);
        // This canvas is squashed like the screen (a character is SubX x SubY
        // pixels here, CellW x CellH there), so draw the letters stretched to match.
        float stretch = (SubY / (float)SubX) / (CellH / CellW);
        pg.TextSize(100);
        float size = Min(100 * tw * 0.86f / Max(1, pg.TextWidth(text)), th * 0.7f / stretch);
        pg.TextSize(size);
        pg.PushMatrix();
        pg.Translate(tw / 2f, th / 2f);
        pg.Scale(1, stretch);
        pg.Text(text, 0, 0);
        pg.PopMatrix();
        pg.LoadPixels();
        target = new float[tw * th];
        for (int i = 0; i < target.Length; i++)
            target[i] = pg.Pixels[i].R / 255f;
        pg.EndDraw();
        pg.Dispose();
        // Soft edges: a light blur, so the borders of the letters come out as . : - =
        Blur(target, tw, th, 2);
        Blur(target, tw, th, 2);
    }

    public override void Draw()
    {
        Background(8, 9, 20);
        if (!paused)
            for (int s = 0; s < StepsPerFrame; s++)
                Train();

        // Ask the network about half the characters each frame (alternate rows):
        // the screen still updates 30 times a second and it costs half.
        shownZoom = Lerp(shownZoom, zoom, 0.12f);
        bool bend = MouseY > 0 && MouseY < Rows * CellH;
        float t = Millis() / 1000f;
        float left = (Width - Cols * CellW) / 2;
        for (int r = FrameCount % 2; r < Rows; r += 2)
            for (int c = 0; c < Cols; c++)
            {
                float x = (c + 0.5f) / Cols, y = (r + 0.5f) / Rows;
                if (bend)
                {
                    float dx = left + (c + 0.5f) * CellW - MouseX, dy = (r + 0.5f) * CellH - MouseY;
                    float a = 1.6f * Exp(-(dx * dx + dy * dy) / (2 * 110f * 110f)) * Sin(t * 1.7f);
                    x += (dx * Cos(a) - dy * Sin(a) - dx) / (Cols * CellW);
                    y += (dx * Sin(a) + dy * Cos(a) - dy) / (Rows * CellH);
                }
                float answer = brain.Think(0.5f + (x - 0.5f) * shownZoom, 0.5f + (y - 0.5f) * shownZoom);
                int i = r * Cols + c;
                glow[i] = Max(glow[i] * 0.9f, Abs(answer - ink[i]) * 12);
                ink[i] = answer;
            }

        TextFont(mono, 16);
        TextAlign(TextAlignH.Center, TextAlignV.Center);
        string glyphs = glyphSets[glyphSet];
        for (int r = 0; r < Rows; r++)
            for (int c = 0; c < Cols; c++)
            {
                int i = r * Cols + c;
                float v = ink[i], hot = Min(glow[i], 1);
                int g = (int)Constrain(v * glyphs.Length, 0, glyphs.Length - 1);
                if (g == 0 && hot < 0.25f)
                    continue;
                if (g == 0) g = 1;
                // Violet to pink across the screen; where it's learning, it warms up
                // through red to amber (400 = 40 degrees, one turn later).
                float hue = 245 + c * 0.8f + 12 * Sin(t * 0.5f + r * 0.15f);
                FillHSB(Lerp(hue, 400, hot * 0.85f) % 360, 65 - 20 * hot, 28 + 72 * Max(v, hot));
                Text(glyphs[g].ToString(), left + (c + 0.5f) * CellW, (r + 0.5f) * CellH);
            }

        DrawHud();
    }

    /// <summary>One lesson: a batch of random pixels of the word -- guess, compare, nudge every weight.</summary>
    void Train()
    {
        brain.BeginBatch();
        float sum = 0;
        for (int b = 0; b < BatchSize; b++)
        {
            int px = rng.Next(tw), py = rng.Next(th);
            sum += brain.Practice((px + 0.5f) / tw, (py + 0.5f) / th, target[py * tw + px]);
        }
        brain.EndBatch(BatchSize);
        Steps++;
        Loss = Lerp(Loss, sum / BatchSize, 0.05f);
        if (Steps % 8 == 0)
            lossHistory[lossCount++ % lossHistory.Length] = Loss;
    }

    void DrawHud()
    {
        float top = Rows * CellH + 20;
        NoStroke();
        Fill(255, 255, 255, 10);
        Rect(0, top - 10, Width, Height - top + 10);

        TextFont(mono, 15);
        TextAlign(TextAlignH.Left, TextAlignV.Top);
        Fill(235, 225, 255);
        Text(word + (FrameCount / 30 % 2 == 0 ? ""_"" : """"), 28, top + 4);
        Fill(140, 145, 180);
        Text($""network {Brain.Inputs} → {Brain.Hidden} → {Brain.Hidden} → 1 · {brain.WeightCount:N0} weights · lesson {Steps:N0} · error {Loss:0.0000}{(paused ? "" · paused"" : """")}"", 28, top + 28);
        Fill(95, 100, 135);
        Text(""type: new word · Enter: forget · mouse: bend · wheel: zoom · Tab: glyphs · Space: pause"", 28, top + 52);

        // The error, lesson after lesson: it falls as the network learns.
        float gx = Width - 210, gy = top + 4, gw = 180, gh = 62;
        NoFill();
        Stroke(255, 255, 255, 30);
        StrokeWeight(1);
        Rect(gx, gy, gw, gh);
        Stroke(255, 170, 110);
        StrokeWeight(1.5f);
        BeginShape();
        int n = Min(lossCount, lossHistory.Length);
        for (int k = 0; k < n; k++)
        {
            float v = lossHistory[(lossCount - n + k) % lossHistory.Length];
            Vertex(gx + gw * k / (lossHistory.Length - 1f), gy + gh - Constrain(Sqrt(v / 0.12f), 0, 1) * (gh - 4) - 2);
        }
        EndShape();
        NoStroke();
    }

    public override void KeyTyped()
    {
        if (Key == BACKSPACE)
        {
            if (word.Length > 0) Learn(word[..^1]);
        }
        else if (Key == ENTER || Key == RETURN)
        {
            brain = new Brain(rng);
            Steps = 0;
            lossCount = 0;
        }
        else if (Key == TAB)
            glyphSet = (glyphSet + 1) % glyphSets.Length;
        else if (Key == ' ')
            paused = !paused;
        else if (!char.IsControl(Key) && word.Length < 10)
            Learn(word + Key);
    }

    public override void MouseWheel(float delta) => zoom = Constrain(zoom * (delta > 0 ? 1.2f : 1 / 1.2f), 0.6f, 8);

    static void Blur(float[] img, int w, int h, int radius)
    {
        var tmp = new float[img.Length];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float s = 0; int n = 0;
                for (int k = -radius; k <= radius; k++)
                    if (x + k >= 0 && x + k < w) { s += img[y * w + x + k]; n++; }
                tmp[y * w + x] = s / n;
            }
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float s = 0; int n = 0;
                for (int k = -radius; k <= radius; k++)
                    if (y + k >= 0 && y + k < h) { s += tmp[(y + k) * w + x]; n++; }
                img[y * w + x] = s / n;
            }
    }

    /// <summary>
    /// The whole ""AI"": a small multilayer perceptron, (x, y) -> ink.
    /// The point first becomes 32 sine waves of random directions and sizes
    /// (""Fourier features"": a plain network is bad at sharp edges, waves give
    /// it something to build them with). Then two layers of 24 neurons (tanh)
    /// and one output (sigmoid, 0..1). Learns by backpropagation, with Adam.
    /// </summary>
    public class Brain
    {
        public const int Waves = 32, Inputs = Waves * 2, Hidden = 24;
        readonly float[] waveX = new float[Waves], waveY = new float[Waves];
        readonly float[] w1 = new float[Inputs * Hidden], b1 = new float[Hidden];   // w1[j * Inputs + i]: input i -> neuron j
        readonly float[] w2 = new float[Hidden * Hidden], b2 = new float[Hidden];
        readonly float[] w3 = new float[Hidden];
        float b3;
        readonly float[] input = new float[Inputs], h1 = new float[Hidden], h2 = new float[Hidden], d2 = new float[Hidden];
        // Gradients and Adam's memory, for all the weights in one row: w1 b1 w2 b2 w3 b3.
        readonly float[] grad, m, v;
        int lessons;

        public int WeightCount => grad.Length;

        public Brain(System.Random rng)
        {
            float Gauss() => (float)(System.Math.Sqrt(-2 * System.Math.Log(1 - rng.NextDouble())) * System.Math.Cos(2 * System.Math.PI * rng.NextDouble()));
            // Mostly gentle waves, some fast ones (letters are wide but their edges are sharp).
            // y is squeezed: on screen 1.0 of height is 0.56 of the width.
            for (int k = 0; k < Waves; k++) { waveX[k] = Gauss() * 10; waveY[k] = Gauss() * 10 * 0.5625f; }
            for (int k = 0; k < w1.Length; k++) w1[k] = Gauss() / MathF.Sqrt(Inputs);
            for (int k = 0; k < w2.Length; k++) w2[k] = Gauss() / MathF.Sqrt(Hidden);
            for (int k = 0; k < w3.Length; k++) w3[k] = Gauss() / MathF.Sqrt(Hidden);
            int n = w1.Length + Hidden + w2.Length + Hidden + Hidden + 1;
            grad = new float[n]; m = new float[n]; v = new float[n];
        }

        float Forward(float x, float y)
        {
            x = x * 2 - 1; y = y * 2 - 1;
            for (int k = 0; k < Waves; k++)
            {
                float a = waveX[k] * x + waveY[k] * y;
                input[2 * k] = MathF.Sin(a);
                input[2 * k + 1] = MathF.Cos(a);
            }
            for (int j = 0; j < Hidden; j++)
            {
                float s = b1[j];
                int row = j * Inputs;
                for (int i = 0; i < Inputs; i++) s += w1[row + i] * input[i];
                h1[j] = MathF.Tanh(s);
            }
            for (int j = 0; j < Hidden; j++)
            {
                float s = b2[j];
                int row = j * Hidden;
                for (int i = 0; i < Hidden; i++) s += w2[row + i] * h1[i];
                h2[j] = MathF.Tanh(s);
            }
            float o = b3;
            for (int i = 0; i < Hidden; i++) o += w3[i] * h2[i];
            return 1 / (1 + MathF.Exp(-o));
        }

        /// <summary>How much ink the network thinks is at (x, y), both 0..1 across the screen.</summary>
        public float Think(float x, float y) => Forward(x, y);

        public void BeginBatch() => System.Array.Clear(grad);

        /// <summary>Guesses one point, adds to every weight's gradient how it should move, returns the squared error.</summary>
        public float Practice(float x, float y, float want)
        {
            float got = Forward(x, y);
            float err = got - want;
            float dOut = err * got * (1 - got);          // through the sigmoid
            int gw1 = 0, gb1 = w1.Length, gw2 = gb1 + Hidden, gb2 = gw2 + w2.Length, gw3 = gb2 + Hidden, gb3 = gw3 + Hidden;
            grad[gb3] += dOut;
            for (int j = 0; j < Hidden; j++)
            {
                grad[gw3 + j] += dOut * h2[j];
                d2[j] = dOut * w3[j] * (1 - h2[j] * h2[j]);
            }
            for (int j = 0; j < Hidden; j++)
            {
                float d = d2[j];
                grad[gb2 + j] += d;
                int row = j * Hidden;
                for (int i = 0; i < Hidden; i++) grad[gw2 + row + i] += d * h1[i];
            }
            for (int i = 0; i < Hidden; i++)
            {
                float s = 0;
                for (int j = 0; j < Hidden; j++) s += d2[j] * w2[j * Hidden + i];
                float d = s * (1 - h1[i] * h1[i]);
                grad[gb1 + i] += d;
                int row = i * Inputs;
                for (int k = 0; k < Inputs; k++) grad[gw1 + row + k] += d * input[k];
            }
            return err * err;
        }

        /// <summary>Adam: every weight steps against its average gradient, scaled by how noisy that gradient has been.</summary>
        public void EndBatch(int batchSize, float rate = 0.003f)
        {
            lessons++;
            float c1 = 1 - MathF.Pow(0.9f, lessons), c2 = 1 - MathF.Pow(0.999f, lessons);
            int at = 0;
            Step(w1); Step(b1); Step(w2); Step(b2); Step(w3);
            float[] last = { b3 };
            Step(last);
            b3 = last[0];

            void Step(float[] weights)
            {
                for (int i = 0; i < weights.Length; i++, at++)
                {
                    float g = grad[at] / batchSize;
                    m[at] = 0.9f * m[at] + 0.1f * g;
                    v[at] = 0.999f * v[at] + 0.001f * g * g;
                    weights[i] -= rate * (m[at] / c1) / (MathF.Sqrt(v[at] / c2) + 1e-7f);
                }
            }
        }
    }
}
";

        private const string SerialArduino =
@"// nuget: System.IO.Ports, 10.0.12

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO.Ports;

// Arduino -> sketch and sketch -> Arduino over USB serial: Processing's
// classic Serial library, here as one `// nuget:` line. The Arduino sends one
// number per line (a potentiometer, a light sensor...) and the sketch plots it;
// hold the mouse down and the sketch lights the Arduino's LED.
//
// The Arduino side -- upload this first:
//
//   void setup() { Serial.begin(115200); pinMode(LED_BUILTIN, OUTPUT); }
//   void loop() {
//     Serial.println(analogRead(A0));
//     if (Serial.available()) digitalWrite(LED_BUILTIN, Serial.read() == '1' ? HIGH : LOW);
//     delay(10);
//   }
//
// No Arduino plugged in? The sketch fakes a sensor with Noise() so you still
// see it work. OnStop() closes the port, so the next Run can open it again
// (otherwise the port stays ""in use"" until the IDE closes).
public class MySketch : Sketch
{
    private const int Baud = 115200;
    private const int Samples = 400;

    private SerialPort? _port;
    private string _status = """";
    private readonly object _lock = new object();
    private readonly Queue<float> _values = new Queue<float>(); // 0..1, newest last
    private string _pending = """";

    public override void Setup()
    {
        Size(820, 480);

        string[] names = SerialPort.GetPortNames();
        if (names.Length == 0)
        {
            _status = ""No serial port found -- simulating a sensor"";
            return;
        }

        string name = names[names.Length - 1]; // usually the one plugged in last
        try
        {
            var port = new SerialPort(name, Baud) { DtrEnable = true };
            port.DataReceived += (s, e) => OnData(port);
            port.Open();
            _port = port;
            _status = $""{name} @ {Baud} baud"";
        }
        catch (Exception ex)
        {
            _status = $""Couldn't open {name}: {ex.Message} -- simulating"";
        }
    }

    // Runs on the serial port's own thread: split what arrived into lines.
    private void OnData(SerialPort port)
    {
        string chunk;
        try { chunk = port.ReadExisting(); }
        catch { return; }

        lock (_lock)
        {
            _pending += chunk;
            int newline;
            while ((newline = _pending.IndexOf('\n')) >= 0)
            {
                string line = _pending.Substring(0, newline).Trim();
                _pending = _pending.Substring(newline + 1);
                if (float.TryParse(line, NumberStyles.Float, CultureInfo.InvariantCulture, out float raw))
                    Push(raw / 1023f);
            }
        }
    }

    private void Push(float value)
    {
        _values.Enqueue(Constrain(value, 0, 1));
        while (_values.Count > Samples)
            _values.Dequeue();
    }

    public override void Draw()
    {
        Background(16, 18, 26);

        float[] values;
        lock (_lock)
        {
            if (_port == null)
                Push(Noise(FrameCount * 0.02f));
            values = _values.ToArray();
        }

        // The trace, oldest on the left.
        NoFill();
        Stroke(110, 220, 160);
        StrokeWeight(2);
        BeginShape();
        for (int i = 0; i < values.Length; i++)
            Vertex(Map(i, 0, Samples - 1, 40, Width - 240), Map(values[i], 0, 1, Height - 70, 50));
        EndShape();

        // The latest value, big.
        float latest = values.Length > 0 ? values[values.Length - 1] : 0;
        NoStroke();
        Fill(110, 220, 160, 60 + latest * 195);
        Circle(Width - 120, Height / 2f, 40 + latest * 150);
        Fill(255);
        TextSize(28);
        TextAlign(TextAlignH.Center);
        Text(((int)(latest * 1023)).ToString(), Width - 120, Height / 2f + 10);
        TextAlign(TextAlignH.Left);

        Fill(150);
        TextSize(13);
        Text(_status + (IsMousePressed ? ""   |   LED on"" : ""   |   hold the mouse to light the LED""), 20, Height - 20);
    }

    public override void MousePressed() => Send(""1"");
    public override void MouseReleased() => Send(""0"");

    private void Send(string text)
    {
        try
        {
            if (_port != null && _port.IsOpen)
                _port.Write(text);
        }
        catch { /* unplugged mid-run: just stop sending */ }
    }

    public override void OnStop()
    {
        var port = _port;
        _port = null;
        try
        {
            port?.Close();
            port?.Dispose();
        }
        catch { }
    }
}";

        private const string WebcamSlitScan =
@"// nuget: FlashCap, 1.12.0

using System;
using System.Collections.Generic;
using System.Linq;
using FlashCap;

// Your webcam, three ways -- with FlashCap, a camera library with no native
// dependencies (Windows: DirectShow / Video for Windows, Linux: V4L2).
//   1  a mirror
//   2  slit-scan: every horizontal band is a different moment, the top is
//      now and the bottom is about a second ago -- move and you smear in time
//   3  echoes: tinted ghosts of the last second, on top of the present
//
// The camera hands frames over on its own thread; Draw() just decodes the
// newest one with LoadImage(byte[]) and keeps the last 48 for the effects.
// OnStop() releases the camera, so the next Run (or another app) can use it.
public class MySketch : Sketch
{
    private const int History = 48;

    private CaptureDevice? _device;
    private byte[]? _incoming; // newest encoded frame, written by the camera thread
    private readonly object _lock = new object();
    private readonly List<PImage> _frames = new List<PImage>(); // newest last
    private volatile bool _stopping;
    private int _mode = 2;
    private string _status = ""Opening the camera..."";

    public override void Setup()
    {
        Size(960, 720);
        Thread(nameof(OpenCamera)); // opening a camera can take a second: don't block Setup()
    }

    private void OpenCamera()
    {
        try
        {
            var descriptor = new CaptureDevices()
                .EnumerateDescriptors()
                .FirstOrDefault(d => d.Characteristics.Length > 0);
            if (descriptor == null)
            {
                _status = ""No camera found"";
                return;
            }

            // Something near 640 wide that FlashCap knows how to decode.
            var characteristics = descriptor.Characteristics
                .Where(c => c.PixelFormat != PixelFormats.Unknown)
                .OrderBy(c => Math.Abs(c.Width - 640))
                .FirstOrDefault();
            if (characteristics == null)
            {
                _status = ""The camera offers no format FlashCap can decode"";
                return;
            }

            var device = descriptor.OpenAsync(characteristics, scope =>
            {
                byte[] image = scope.Buffer.CopyImage(); // a copy: safe to use after this callback
                lock (_lock)
                    _incoming = image;
            }).GetAwaiter().GetResult();
            device.StartAsync().GetAwaiter().GetResult();

            if (_stopping) // the sketch was stopped while the camera was still opening
            {
                device.StopAsync().GetAwaiter().GetResult();
                device.Dispose();
                return;
            }
            _device = device;
            _status = $""{descriptor.Name}  {characteristics}"";
        }
        catch (Exception ex)
        {
            _status = ""Camera error: "" + ex.Message;
        }
    }

    public override void Draw()
    {
        Background(0);

        byte[]? bytes;
        lock (_lock)
        {
            bytes = _incoming;
            _incoming = null;
        }
        if (bytes != null)
        {
            try
            {
                _frames.Add(LoadImage(bytes));
                while (_frames.Count > History)
                {
                    _frames[0].Dispose();
                    _frames.RemoveAt(0);
                }
            }
            catch { /* the odd corrupt frame: skip it */ }
        }

        if (_frames.Count == 0)
        {
            Fill(200);
            TextSize(16);
            Text(_status, 24, Height / 2f);
            return;
        }

        // Flipped horizontally, like a mirror.
        PushMatrix();
        Translate(Width, 0);
        Scale(-1, 1);
        if (_mode == 1)
            Image(_frames[_frames.Count - 1], 0, 0, Width, Height);
        else if (_mode == 2)
            SlitScan();
        else
            Echoes();
        PopMatrix();

        Fill(0, 0, 0, 150);
        Rect(0, Height - 34, Width, 34);
        Fill(230);
        TextSize(13);
        Text($""1 mirror   2 slit-scan   3 echoes   |   {_status}"", 14, Height - 12);
    }

    // Band i shows the frame from i frames ago.
    private void SlitScan()
    {
        int bands = _frames.Count;
        float bandHeight = Height / (float)bands;
        for (int i = 0; i < bands; i++)
        {
            Clip(0, i * bandHeight, Width, bandHeight + 1);
            Image(_frames[_frames.Count - 1 - i], 0, 0, Width, Height);
        }
        NoClip();
    }

    private void Echoes()
    {
        Image(_frames[_frames.Count - 1], 0, 0, Width, Height);
        for (int k = 1; k <= 5; k++)
        {
            int index = _frames.Count - 1 - k * 8;
            if (index < 0)
                break;
            Tint(255, 120 + k * 20, 255 - k * 30, 70);
            Image(_frames[index], 0, 0, Width, Height);
        }
        NoTint();
    }

    public override void KeyPressed()
    {
        if (Key >= '1' && Key <= '3')
            _mode = Key - '0';
    }

    public override void OnStop()
    {
        _stopping = true;
        var device = _device;
        _device = null;
        try
        {
            device?.StopAsync().GetAwaiter().GetResult();
            device?.Dispose();
        }
        catch { }

        foreach (var frame in _frames)
            frame.Dispose();
        _frames.Clear();
    }
}";

        private const string MidiBounce =
@"// nuget: NAudio, 2.2.1

using System;
using System.Collections.Generic;
using NAudio.Midi;

// Balls bouncing in a box -- every wall they hit plays a note through MIDI
// output 0, from a minor pentatonic scale (so it always sounds good). On
// Windows output 0 is the built-in ""Microsoft GS Wavetable Synth"", so it plays
// with no setup at all; with a virtual port (loopMIDI) or a USB synth it plays
// your DAW or hardware instead. NAudio's MIDI is Windows-only.
//
// Click to add a ball, right-click to remove one. The [Tweak] panel changes
// gravity, the root note, the instrument and how long notes ring -- live.
// OnStop() silences anything still sounding and releases the MIDI port.
public class MySketch : Sketch
{
    private static readonly int[] Pentatonic = { 0, 3, 5, 7, 10, 12, 15, 17 };

    [Tweak(0, 0.6f)] float gravity = 0.18f;
    [Tweak(36, 72)] int rootNote = 48;
    [Tweak(0, 127, Label = ""instrument (GM)"")] int program = 11; // 11 = vibraphone
    [Tweak(50, 2000, Label = ""note ms"")] int noteLength = 450;

    private MidiOut? _midi;
    private string _status = """";
    private int _sentProgram = -1;
    private readonly List<Ball> _balls = new List<Ball>();
    private readonly List<(int Note, long OffAt)> _sounding = new List<(int Note, long OffAt)>();

    private sealed class Ball
    {
        public float X, Y, VX, VY, Radius, Flash;
        public int Degree;
    }

    public override void Setup()
    {
        Size(820, 560);
        try
        {
            if (MidiOut.NumberOfDevices > 0)
            {
                _midi = new MidiOut(0);
                _status = ""MIDI out: "" + MidiOut.DeviceInfo(0).ProductName;
            }
            else
            {
                _status = ""No MIDI output found -- silent"";
            }
        }
        catch (Exception ex)
        {
            _status = ""MIDI unavailable ("" + ex.GetType().Name + "") -- silent"";
        }

        for (int i = 0; i < 4; i++)
            AddBall(Random(100, Width - 100), Random(80, Height / 2f));
    }

    private void AddBall(float x, float y)
    {
        _balls.Add(new Ball
        {
            X = x,
            Y = y,
            VX = Random(-5, 5),
            VY = Random(-2, 2),
            Radius = Random(14, 30),
            Degree = _balls.Count % Pentatonic.Length,
        });
    }

    public override void Draw()
    {
        Background(14, 16, 24, 90); // translucent: leaves trails

        if (program != _sentProgram)
        {
            Send(MidiMessage.ChangePatch(Constrain(program, 0, 127), 1));
            _sentProgram = program;
        }

        foreach (var ball in _balls)
        {
            ball.VY += gravity;
            ball.X += ball.VX;
            ball.Y += ball.VY;

            bool hit = false;
            if (ball.X < ball.Radius || ball.X > Width - ball.Radius)
            {
                ball.VX = -ball.VX;
                ball.X = Constrain(ball.X, ball.Radius, Width - ball.Radius);
                hit = true;
            }
            if (ball.Y > Height - ball.Radius)
            {
                ball.VY = -Math.Abs(ball.VY) * 0.98f;
                ball.Y = Height - ball.Radius;
                hit = true;
            }
            if (ball.Y < ball.Radius)
            {
                ball.VY = Math.Abs(ball.VY);
                ball.Y = ball.Radius;
                hit = true;
            }

            if (hit)
            {
                // Higher on screen = higher note; bigger ball = louder.
                int degree = (ball.Degree + (int)Map(ball.Y, Height, 0, 0, 4)) % Pentatonic.Length;
                PlayNote(rootNote + Pentatonic[degree], (int)Map(ball.Radius, 14, 30, 60, 120));
                ball.Flash = 1;
            }

            ball.Flash *= 0.9f;
            NoStroke();
            FillHSB(ball.Degree * 45 % 360, 60, 60 + ball.Flash * 40); // h 0-360, s and b 0-100
            Circle(ball.X, ball.Y, ball.Radius * 2 * (1 + ball.Flash * 0.3f));
        }

        ReleaseFinishedNotes();

        Fill(170);
        TextSize(13);
        Text($""{_status}   |   {_balls.Count} balls   |   click: add   right-click: remove"", 16, Height - 16);
    }

    private void PlayNote(int note, int velocity)
    {
        note = Constrain(note, 0, 127);
        Send(MidiMessage.StartNote(note, Constrain(velocity, 1, 127), 1));
        _sounding.Add((note, Millis() + noteLength));
    }

    private void ReleaseFinishedNotes()
    {
        long now = Millis();
        for (int i = _sounding.Count - 1; i >= 0; i--)
        {
            if (_sounding[i].OffAt > now)
                continue;
            Send(MidiMessage.StopNote(_sounding[i].Note, 0, 1));
            _sounding.RemoveAt(i);
        }
    }

    private void Send(MidiMessage message)
    {
        try { _midi?.Send(message.RawData); }
        catch { /* device unplugged: keep drawing, just silently */ }
    }

    public override void MousePressed()
    {
        if (MouseButton == MouseButtonKind.Right)
        {
            if (_balls.Count > 0)
                _balls.RemoveAt(_balls.Count - 1);
        }
        else
        {
            AddBall(MouseX, MouseY);
        }
    }

    public override void OnStop()
    {
        foreach (var (note, _) in _sounding)
            Send(MidiMessage.StopNote(note, 0, 1));
        _sounding.Clear();
        _midi?.Dispose();
        _midi = null;
    }
}";

        private const string LiveShader =
@"// Live shaders, Shadertoy-style -- a fragment shader in SkSL (Skia's GLSL:
// half4/float2 instead of vec4/vec2) painting every pixel, fed by the sound
// engine. Shade(source) compiles it once and caches it by its text, so:
//
//   change a number inside the shader below and press ⚡ Hot Reload (or save)
//   -> the new shader takes over instantly, time and music keep going.
//   A typo doesn't crash anything: the last good shader keeps running and the
//   compiler's message shows bottom-left.
//
// Uniforms filled in for you when declared: iTime, iResolution, iMouse
// (x, y, pressed), iFrame, iAmplitude and iSpectrum[8/16/32/64] -- here the
// bass of the beat below pushes the rings and the loudness lights the center.
// Runs on the CPU in the 2D renderer, every OS: keep the canvas modest (and
// try a lower supersampling in Settings if it gets heavy).
public class MySketch : Sketch
{
    const string Plasma = @""
uniform float iTime;
uniform float2 iResolution;
uniform float4 iMouse;
uniform float iAmplitude;
uniform float iSpectrum[16];

half4 main(float2 fragCoord) {
    float2 uv = (fragCoord - 0.5 * iResolution) / iResolution.y;
    float2 m = (iMouse.xy - 0.5 * iResolution) / iResolution.y;
    float d = length(uv - m * 0.6);
    float bass = iSpectrum[1] + iSpectrum[2];

    float v = sin(12.0 * d - iTime * 3.0 + bass * 5.0)
            + sin(7.0 * uv.x + iTime)
            + sin(6.0 * (uv.x + uv.y) - iTime * 1.3);
    v /= 3.0;

    float3 col = 0.5 + 0.5 * cos(6.2831 * (v + float3(0.0, 0.33, 0.67)) + iTime * 0.2);
    col += iAmplitude * 0.8 * exp(-d * 5.0);
    return half4(half3(col), 1.0);
}"";

    public override void Setup()
    {
        Size(720, 450);
    }

    public override void Draw()
    {
        Shade(Plasma);
    }

    [LiveLoop]
    async Task Beat()
    {
        Sample(""kick"");
        await Sleep(0.5);
        Sample(""hat"");
        await Sleep(0.5);
        Sample(""snare"");
        await Sleep(0.5);
        Sample(""hat"");
        await Sleep(0.5);
    }

    [LiveLoop]
    async Task Bass()
    {
        UseSynth(Synth.Saw);
        foreach (var note in new[] { ""E2"", ""E2"", ""G2"", ""D2"" })
        {
            Play(note, release: 0.4, cutoff: 70);
            await Sleep(1);
        }
    }
}";

    }
}
