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

        };

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
