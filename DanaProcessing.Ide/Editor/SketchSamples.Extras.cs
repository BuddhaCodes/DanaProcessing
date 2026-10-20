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

            new SketchSample(
                Loc.Tr("Disco ASCII en 3D: la cámara se aleja y aparece un bailarín", "3D ASCII disco: the camera pulls back and a dancer appears"),
                Loc.Tr("Estás parado en el medio de un campo 3D de caracteres ASCII que flotan y laten con la música. La cámara mira alrededor, camina, sube y gira, y después se aleja bien arriba: desde lejos el campo resulta ser una pantalla, y los caracteres juntos son un bailarín en una pista de disco, bailando la canción que sonó todo el tiempo. Todo es código: el bailarín se dibuja en un PGraphics cada frame, la cámara 3D es matemática simple (vectores y división por la distancia) y la canción son cinco [LiveLoop] a 116 BPM. Espacio congela la cámara, H oculta los textos.",
                       "You're standing in the middle of a 3D field of ASCII characters that float and throb with the music. The camera looks around, walks, climbs and circles, then pulls far up: from there the field turns out to be a screen, and all the characters together are a dancer on a disco floor, dancing to the song that's been playing all along. It's all code: the dancer is drawn on a PGraphics every frame, the 3D camera is plain math (vectors and a divide by distance) and the song is five [LiveLoop]s at 116 BPM. Space freezes the camera, H hides the labels."),
                AsciiDisco,
                new[] { SampleTag.ThreeD, SampleTag.Text, SampleTag.Audio, SampleTag.Animation }),

            new SketchSample(
                Loc.Tr("Agujero negro en ASCII 3D, con música de película", "A 3D ASCII black hole, with a film score"),
                Loc.Tr("Cada carácter es un rayo de luz trazado hacia atrás desde la cámara y curvado por la gravedad (la ecuación de un rayo en la geometría de Schwarzschild, paso a paso). Por eso se ve la parte de atrás del disco de gas doblada por encima y por debajo de la sombra, y el lado que gira hacia vos más brillante y más blanco (efecto Doppler). La cámara llega desde lejos, baja al plano del disco y lo rodea acercándose cada vez más, y al final se aleja despacio hasta donde empezó, mientras suena una orquesta hecha con sintetizadores en ocho [LiveLoop]: cuerdas, cellos, un piano que persigue su propio eco, coro, órgano, timbales y un reloj que no para. El disco brilla más a medida que la música crece. Espacio congela la cámara, la rueda acerca o aleja, H oculta los textos.",
                       "Every character is a ray of light traced backwards from the camera and bent by gravity (the equation of a light ray in Schwarzschild geometry, stepped through). That's why you see the far side of the gas disk bent over and under the shadow, and the side spinning towards you brighter and whiter (Doppler beaming). The camera comes in from far away, sinks to the disk's plane and circles it ever closer, then slowly pulls back out to where it began, while an orchestra made of synths plays in eight [LiveLoop]s: strings, cellos, a piano chasing its own echo, choir, organ, timpani and a clock that never stops. The disk glows brighter as the music grows. Space freezes the camera, the wheel moves closer or farther, H hides the labels."),
                AsciiBlackHole,
                new[] { SampleTag.ThreeD, SampleTag.Text, SampleTag.Audio, SampleTag.Generative }),

            new SketchSample(
                Loc.Tr("Tu clip en ASCII 3D: la nube se arma en video", "Your clip in 3D ASCII: the cloud assembles into video"),
                Loc.Tr("Cualquier video convertido en un volumen de caracteres. Empezás dentro de una nube de glifos; la cámara mira alrededor y retrocede, y los glifos vuelan a su lugar hasta formar el clip, reproduciéndose. No es una pantalla plana: cada carácter está a una profundidad (lo claro adelante, lo oscuro atrás, lo más brillante en columnitas), así que cuando la cámara se abre de costado se ve el grosor. Poné tus cuadros en Documentos/DanaClip/frames (y opcionalmente audio.wav, que suena sincronizado); en los comentarios están los comandos de ffmpeg. Los cuadros se cargan de a poco, sin congelar el arranque. Sin carpeta, muestra una animación propia y una canción hecha con [LiveLoop]. Espacio congela la cámara, R recarga, H oculta los textos.",
                       "Any video turned into a volume of characters. You start inside a cloud of glyphs; the camera looks around and backs away, and the glyphs fly into place until they form the clip, playing. It isn't a flat screen: every character sits at a depth (light in front, dark behind, the brightest in little columns), so when the camera swings to the side you see its thickness. Put your frames in Documents/DanaClip/frames (and optionally audio.wav, played in sync); the ffmpeg commands are in the comments. Frames load a few at a time, so the start never freezes. Without a folder it shows its own animation and a song made with [LiveLoop]s. Space freezes the camera, R reloads, H hides the labels."),
                AsciiClip3D,
                new[] { SampleTag.ThreeD, SampleTag.Text, SampleTag.Images, SampleTag.Audio }),

        };

        private const string AsciiClip3D =
@"// Any video, as a 3D volume of ASCII characters.
//
// You start inside a cloud of floating glyphs. The camera looks around and
// backs away, and as it does the glyphs fly into place: the cloud becomes the
// clip, playing. It isn't a flat screen -- every character sits at a depth
// (bright parts closer to you, dark parts further back, the brightest ones
// extruded into short columns), so when the camera swings to the side you see
// the picture has thickness. Then the camera dives back in, the clip scatters
// into a cloud again, and it all loops.
//
// YOUR CLIP: put it in a folder (ClipDir below; by default Documents/DanaClip):
//     frames/0001.png, 0002.png ...   the pictures, in order
//     audio.wav                       optional: its sound, played in sync
// From any video, with ffmpeg:
//     ffmpeg -i video.mp4 -vf ""fps=30,scale=192:-2"" frames/%04d.png
//     ffmpeg -i video.mp4 -vn -ac 2 -ar 48000 audio.wav
// Without frames it plays a built-in animation; without audio.wav, a little
// disco song made with [LiveLoop]s starts when the picture comes together.
//
// Space: freeze the camera.  H: hide the labels.  R: reload the clip.
public class AsciiClip3D : Sketch
{
    // ---- settings --------------------------------------------------------------------
    static readonly string ClipDir = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), ""DanaClip"");
    const float ClipFps = 30;          // the fps you extracted the frames at
    const int GH = 54;                 // characters from top to bottom (the width follows the clip's shape)
    const float Gap = 0.5f;            // world units between characters
    const float Thickness = 9;         // how deep the picture is, front (white) to back (black)
    const float InsideSeconds = 11, DiveSeconds = 6;
    const string Glyphs = "" .:-=+*#%@"";

    // ---- the clip ----------------------------------------------------------------------
    string[] files = System.Array.Empty<string>();
    readonly List<PImage> frames = new();
    SampleBuffer audio;
    string status = """";

    // ---- the field ---------------------------------------------------------------------
    int GW, PicW, PicH, cells;
    const int S = 2;                   // clip pixels averaged per character, each way
    PGraphics pic;
    PFont mono;
    float[] lum, chaosX, chaosY, chaosZ, delay, depth, sx, sy, ss;
    int[] order, glyphOf;
    Color[] tint;
    char[] noiseGlyph;
    int[] itemCell; float[] itemBack;  // what gets drawn: a cell, and how far behind its face (extrusion)

    // ---- time ----------------------------------------------------------------------------
    float camSeconds, revealSeconds = 20;
    bool frozen, labels = true, musicOn, wasRevealing;
    long lastMillis, revealStartMillis;
    int padBar, bassBar, leadBar, drumBar;

    public override void Setup()
    {
        Size(960, 540);
        Bpm = 116;
        mono = CreateFont(""Consolas"", 32);
        FindClip();
        BuildField(16f / 9f);
        lastMillis = Millis();
    }

    // =====================================================================================
    // Loading: the file list now, the pictures a few per frame (no frozen start).
    // =====================================================================================
    void FindClip()
    {
        frames.Clear();
        audio = null;
        files = System.Array.Empty<string>();
        string dir = System.IO.Path.Combine(ClipDir, ""frames"");
        if (!Directory.Exists(dir))
        {
            status = $""no clip in {dir} -- playing the built-in animation"";
            return;
        }
        files = Directory.GetFiles(dir, ""*.png"").OrderBy(f => f, StringComparer.Ordinal).ToArray();
        string wav = System.IO.Path.Combine(ClipDir, ""audio.wav"");
        if (File.Exists(wav))
        {
            try { audio = LoadSample(wav); }
            catch (Exception e) { status = ""audio.wav: "" + e.Message; }
        }
    }

    bool Loading => frames.Count < files.Length;
    bool HasClip => files.Length > 0 && !Loading;
    float ClipSeconds => HasClip ? frames.Count / ClipFps : 8;

    void LoadSome()
    {
        var start = Millis();
        while (Loading && Millis() - start < 12)          // ~12 ms of loading per frame
        {
            frames.Add(LoadImage(files[frames.Count]));
            if (frames.Count == 1)
                BuildField(frames[0].Width / (float)frames[0].Height);
        }
        if (!Loading && files.Length > 0)
            status = $""{frames.Count} frames ({ClipSeconds:0.0} s)"" + (audio != null ? $"" + audio.wav ({audio.Duration:0.0} s)"" : "" -- no audio.wav, playing the disco loops"");
    }

    /// <summary>Sizes everything for a clip of this shape, and scatters the cloud.</summary>
    void BuildField(float aspect)
    {
        // Characters are about twice as tall as wide on screen in the clip's terms, but here
        // they're laid out in a square grid in 3D, so: one cell per (aspect) pixels across.
        GW = Math.Clamp((int)MathF.Round(GH * aspect), 16, 140);
        cells = GW * GH;
        PicW = GW * S; PicH = GH * S;
        pic?.Dispose();
        pic = CreateGraphics(PicW, PicH);
        lum = new float[cells]; tint = new Color[cells]; glyphOf = new int[cells];
        chaosX = new float[cells]; chaosY = new float[cells]; chaosZ = new float[cells]; delay = new float[cells];
        noiseGlyph = new char[cells];
        int maxItems = cells * 3;
        depth = new float[maxItems]; order = new int[maxItems]; itemCell = new int[maxItems]; itemBack = new float[maxItems];
        sx = new float[maxItems]; sy = new float[maxItems]; ss = new float[maxItems];
        for (int i = 0; i < cells; i++)
        {
            // The cloud: a ball around the start position, denser in the middle.
            float u = Hash(i), v = Hash(i + 7919), w = Hash(i + 104729);
            float th = u * TWO_PI, ph = Acos(2 * v - 1), rad = 3 + 14 * Pow(w, 0.6f);
            chaosX[i] = Sin(ph) * Cos(th) * rad;
            chaosY[i] = Cos(ph) * rad * 0.6f;
            chaosZ[i] = Sin(ph) * Sin(th) * rad;
            // Assembly order: the middle of the picture first, the edges last, with some jitter.
            int col = i % GW, row = i / GW;
            float dx = (col - GW / 2f) / GW, dy = (row - GH / 2f) / GH;
            delay[i] = Constrain(Sqrt(dx * dx + dy * dy) * 1.3f + Hash(i + 31337) * 0.25f, 0, 1);
            noiseGlyph[i] = Glyphs[2 + (int)(Hash(i + 4242) * (Glyphs.Length - 2))];
        }
    }

    // =====================================================================================
    // Each frame
    // =====================================================================================
    public override void Draw()
    {
        long now = Millis();
        float dt = (now - lastMillis) / 1000f;
        lastMillis = now;
        if (Loading) LoadSome();

        // The reveal lasts as long as the sound (or two passes of the clip, at least 16 s).
        revealSeconds = audio != null ? (float)audio.Duration : Max(16, ClipSeconds * 2);
        float cycle = InsideSeconds + revealSeconds + DiveSeconds;
        if (!frozen) camSeconds = (camSeconds + dt) % cycle;
        float s = camSeconds;

        // Phase changes: entering the reveal starts the clip and its sound from the top.
        bool revealing = s >= InsideSeconds && s < InsideSeconds + revealSeconds;
        if (revealing && !wasRevealing)
        {
            revealStartMillis = now - (long)((s - InsideSeconds) * 1000);
            if (audio != null && HasClip) Sample(audio, amp: 0.9);
        }
        wasRevealing = revealing;
        musicOn = revealing && (audio == null || !HasClip);

        float clipT = revealing ? (now - revealStartMillis) / 1000f : 0;
        PaintPicture(clipT);

        var (eye, look, assembled) = Camera(s);
        Background(4, 3, 12);
        DrawField(eye, look, assembled, now / 1000f);
        if (labels) Labels(s, revealing, clipT);
    }

    void PaintPicture(float clipT)
    {
        pic.BeginDraw();
        if (HasClip)
        {
            int index = (int)(clipT * ClipFps) % frames.Count;
            pic.Image(frames[index], 0, 0, PicW, PicH);
        }
        else
            PaintDemo(clipT);
        pic.LoadPixels();
        var px = pic.Pixels;
        for (int r = 0; r < GH; r++)
            for (int c = 0; c < GW; c++)
            {
                int R = 0, G = 0, B = 0;
                for (int dy = 0; dy < S; dy++)
                    for (int dx = 0; dx < S; dx++)
                    {
                        var p = px[(r * S + dy) * PicW + c * S + dx];
                        R += p.R; G += p.G; B += p.B;
                    }
                int i = r * GW + c, n = S * S;
                lum[i] = (0.3f * R + 0.55f * G + 0.15f * B) / (n * 255f);
                tint[i] = Color(R / n, G / n, B / n);
                glyphOf[i] = (int)Constrain(Sqrt(lum[i]) * Glyphs.Length, 1, Glyphs.Length - 1);
            }
        pic.EndDraw();
    }

    /// <summary>The built-in clip, for when there's no folder: rings, a rotating word, a beat.</summary>
    void PaintDemo(float t)
    {
        pic.Background(12, 8, 30);
        pic.NoFill();
        for (int k = 0; k < 7; k++)
        {
            float r = ((t * 30 + k * 22) % 154);
            pic.StrokeWeight(3);
            pic.StrokeHSB((200 + k * 25 + t * 40) % 360, 70, 100);
            pic.Circle(PicW / 2f, PicH / 2f, r);
        }
        pic.NoStroke();
        pic.PushMatrix();
        pic.Translate(PicW / 2f, PicH / 2f);
        pic.Rotate(Sin(t * 0.8f) * 0.35f);
        pic.Scale(1 + 0.08f * Sin(t * PI * 116 / 60));
        pic.Fill(255, 240, 220);
        pic.TextFont(mono, 34);
        pic.TextAlign(TextAlignH.Center, TextAlignV.Center);
        pic.Text(""DANA"", 0, 0);
        pic.PopMatrix();
    }

    // =====================================================================================
    // The camera: inside the cloud -> back away while it assembles -> swing round to show
    // the depth -> dive back in as it scatters. Starts and ends at the same spot.
    // =====================================================================================
    (PVector Eye, PVector Look, float Assembled) Camera(float s)
    {
        float worldW = GW * Gap, hfov = 2 * Atan(Tan(Radians(30)) * Width / (float)Height);
        float far = worldW / 2 / Tan(hfov / 2) * 1.2f + Thickness;   // the whole picture fits, front included

        if (s < InsideSeconds)
        {
            // Looking around inside the cloud, drifting backwards.
            float k = s / InsideSeconds;
            float yaw = Radians(150) * Sin(PI * k), pitch = Radians(18) * Sin(TWO_PI * k);
            var eye = new PVector(0, 0.3f, -5 * Smooth(0.4f, 1, k));
            var dir = new PVector(Sin(yaw) * Cos(pitch), Sin(pitch), Cos(yaw) * Cos(pitch));
            return (eye, PVector.Add(eye, dir), 0);
        }
        s -= InsideSeconds;
        if (s < revealSeconds)
        {
            // Pull back to see the whole picture as it assembles, then swing to one side
            // and the other, slowly, so the depth shows; back to the front to finish.
            float pull = Smooth(0, 4, s);
            float u = Constrain((s - 4) / Max(1, revealSeconds - 6), 0, 1);
            float swing = Radians(38) * Sin(TWO_PI * u) * Smooth(0, 0.12f, u) * (1 - Smooth(0.88f, 1, u));
            float d = Lerp(5, far, pull);
            var eye = new PVector(-Sin(swing) * d, 0.3f + 2.5f * Sin(PI * u) * pull, -Cos(swing) * d);
            return (eye, new PVector(0, 0, 0), Smooth(0.5f, 5, s));
        }
        s -= revealSeconds;
        {
            // Dive back in; the picture scatters into the cloud again.
            float k = Smooth(0, DiveSeconds, s);
            var eye = new PVector(0, 0.3f, Lerp(-far, 0, k));
            return (eye, PVector.Add(eye, new PVector(0, 0, 1)), 1 - Smooth(0.2f, 0.8f, k));
        }
    }

    void DrawField(PVector eye, PVector look, float assembled, float time)
    {
        var f = PVector.Sub(look, eye).Normalize();
        var right = f.Cross(new PVector(0, 1, 0)).Normalize();
        var up = right.Cross(f);
        float focal = Height / 2f / Tan(Radians(30));
        int n = 0;
        float spin = time * 0.08f, cs = Cos(spin), sn = Sin(spin);

        for (int i = 0; i < cells; i++)
        {
            int col = i % GW, row = i / GW;
            // Where it sits in the picture (x mirrored so it reads right from the front).
            float px = ((GW - 1) / 2f - col) * Gap, py = ((GH - 1) / 2f - row) * Gap;
            float pz = (0.45f - lum[i]) * Thickness;
            // Where it floats in the cloud (the cloud turns slowly).
            float cx = chaosX[i] * cs - chaosZ[i] * sn, cz = chaosX[i] * sn + chaosZ[i] * cs;
            float cy = chaosY[i] + Sin(time * 0.7f + i * 0.13f) * 0.4f;
            // Each one flies in on its own schedule, along a curve.
            float a = Smooth(delay[i] * 0.55f, delay[i] * 0.55f + 0.45f, assembled);
            float swirl = (1 - a) * 2.2f;
            float bx = Lerp(cx, px, a), bz = Lerp(cz, pz, a);
            float wx = bx * Cos(swirl) - bz * Sin(swirl), wz = bx * Sin(swirl) + bz * Cos(swirl);
            float wy = Lerp(cy, py, a);
            // The brightest characters get one or two copies behind them: little columns.
            int copies = a > 0.9f ? (lum[i] > 0.75f ? 3 : lum[i] > 0.5f ? 2 : 1) : 1;
            for (int k = 0; k < copies; k++)
            {
                float back = k * 0.9f;
                float dx = wx - eye.X, dy = wy - eye.Y, dz = wz + back - eye.Z;
                float zc = dx * f.X + dy * f.Y + dz * f.Z;
                if (zc < 0.5f) continue;
                float xc = dx * right.X + dy * right.Y + dz * right.Z;
                float yc = dx * up.X + dy * up.Y + dz * up.Z;
                float size = focal * Gap * 1.05f / zc;
                float x = Width / 2f + focal * xc / zc, y = Height / 2f - focal * yc / zc;
                if (size < 1.6f || x < -size || x > Width + size || y < -size || y > Height + size) continue;
                sx[n] = x; sy[n] = y; ss[n] = Min(size, 140);
                itemCell[n] = i; itemBack[n] = k;
                depth[n] = -zc;
                order[n] = n;
                n++;
            }
        }
        System.Array.Sort(depth, order, 0, n);

        TextFont(mono, 32);
        TextAlign(TextAlignH.Center, TextAlignV.Center);
        for (int q = 0; q < n; q++)
        {
            int k = order[q], i = itemCell[k];
            float z = -depth[q];
            float a = Smooth(delay[i] * 0.55f, delay[i] * 0.55f + 0.45f, assembled);
            float v = lum[i];
            var c = tint[i];
            // In the cloud: cool blues; in place: the clip's own color. Copies behind are dimmer.
            float r = Lerp(60 + 80 * Hash(i + 3), c.R * 1.25f + 10, a);
            float g = Lerp(100 + 50 * Hash(i + 5), c.G * 1.25f + 10, a);
            float b = Lerp(150 + 80 * Hash(i + 9), c.B * 1.25f + 20, a);
            float fade = Constrain((z - 0.5f) / 2.5f, 0, 1) * (itemBack[k] == 0 ? 1 : 0.45f / itemBack[k]);
            float presence = a < 0.5f ? 0.85f : 0.4f + 0.6f * Min(1, v * 2.2f + 0.3f);
            Fill(Min(255, r), Min(255, g), Min(255, b), 255 * fade * presence);
            float size = ss[k];
            TextSize(size < 24 ? MathF.Round(size) : MathF.Round(size / 6) * 6);   // few sizes: Skia's glyph cache hits
            char glyph = a < 0.5f ? noiseGlyph[i] : Glyphs[glyphOf[i]];
            Text(glyph.ToString(), sx[k], sy[k]);
        }
    }

    void Labels(float s, bool revealing, float clipT)
    {
        TextFont(mono, 13);
        TextSize(13);
        TextAlign(TextAlignH.Left, TextAlignV.Bottom);
        Fill(255, 255, 255, 120);
        string where = s < InsideSeconds ? ""inside the cloud"" : revealing ? $""the clip · {clipT:0.0} s"" : ""diving back in"";
        if (Loading) where = $""loading frames {frames.Count}/{files.Length}"";
        Text($""{where}  ·  {status}{(frozen ? ""  ·  camera frozen"" : """")}"", 16, Height - 30);
        Fill(255, 255, 255, 60);
        Text($""Space: freeze camera  ·  H: hide labels  ·  R: reload  ·  clip folder: {ClipDir}"", 16, Height - 12);
    }

    public override void KeyPressed()
    {
        if (Key == ' ') frozen = !frozen;
        else if (Key == 'h' || Key == 'H') labels = !labels;
        else if (Key == 'r' || Key == 'R') { FindClip(); camSeconds = 0; wasRevealing = false; }
    }

    static float Smooth(float a, float b, float x)
    {
        float k = Math.Clamp((x - a) / (b - a), 0, 1);
        return k * k * (3 - 2 * k);
    }

    static float Hash(int n)
    {
        unchecked
        {
            uint x = (uint)n;
            x ^= x >> 16; x *= 0x7feb352d; x ^= x >> 15; x *= 0x846ca68b; x ^= x >> 16;
            return (x & 0xFFFFFF) / 16777215f;
        }
    }

    // =====================================================================================
    // The fallback song (when there's no audio.wav): an original disco loop at 116 BPM,
    // playing only while the picture is together. Fmaj7 - Em7 - Dm7 - Cmaj7.
    // =====================================================================================
    static readonly string[][] Chords =
    {
        new[] { ""F3"", ""A3"", ""C4"", ""E4"" }, new[] { ""E3"", ""G3"", ""B3"", ""D4"" },
        new[] { ""D3"", ""F3"", ""A3"", ""C4"" }, new[] { ""C3"", ""E3"", ""G3"", ""B3"" },
    };
    static readonly string[] Roots = { ""F2"", ""E2"", ""D2"", ""C2"" };

    [LiveLoop]
    async Task Pad()
    {
        if (!musicOn) { padBar = 0; await Sleep(1); return; }
        foreach (var note in Chords[padBar++ % 4])
            Play(note, amp: 0.06, attack: 0.3, sustain: 2.6, release: 1, cutoff: 88, synth: Synth.Saw);
        await Sleep(4);
    }

    [LiveLoop]
    async Task Bass()
    {
        if (!musicOn) { bassBar = 0; await Sleep(1); return; }
        double root = Notes.Parse(Roots[bassBar++ % 4]);
        for (int i = 0; i < 8; i++)
        {
            Play(root + (i % 2 == 1 ? 12 : 0), amp: 0.24, release: 0.2, cutoff: 76, res: 0.3, synth: Synth.Saw);
            await Sleep(0.5);
        }
    }

    [LiveLoop]
    async Task Lead()
    {
        if (!musicOn) { leadBar = 0; await Sleep(1); return; }
        (string Note, double Beats)[] climb = { (""E5"", 0.5), (""G5"", 0.5), (""A5"", 1), (""G5"", 0.5), (""E5"", 0.5), (""D5"", 1) };
        (string Note, double Beats)[] home = { (""C5"", 0.5), (""D5"", 0.5), (""E5"", 1.5), ("""", 0.5), (""G4"", 0.5), (""A4"", 0.5) };
        foreach (var (note, beats) in leadBar++ % 2 == 0 ? climb : home)
        {
            if (note != """")
                Play(note, amp: 0.09, release: beats * 0.45, cutoff: 96, pan: -0.15, synth: Synth.Square);
            await Sleep(beats);
        }
    }

    [LiveLoop]
    async Task Kit()
    {
        if (!musicOn) { drumBar = 0; await Sleep(1); return; }
        drumBar++;
        for (int beat = 0; beat < 4; beat++)
        {
            Sample(""kick"", amp: 0.85);
            if (beat % 2 == 1) Sample(""snare"", amp: 0.5);
            Sample(""hat"", amp: 0.1, pan: 0.2);
            await Sleep(0.5);
            Sample(""hat"", amp: 0.28, pan: 0.25);
            await Sleep(0.5);
        }
    }
}
";

        private const string AsciiBlackHole =
@"// A black hole in ASCII, ray traced in 3D, with a slow cinematic score.
//
// Every character on screen is a ray of light traced backwards from the
// camera. Near the black hole the rays bend: in Schwarzschild's geometry a
// light ray's path obeys  p'' = -1.5 h² p / |p|^5  (h = how much it swings
// around the hole), so we just step that equation. Then, for each ray:
// - it falls in (r < 1, the event horizon): black, no character;
// - it crosses the disk of hot gas around the hole: that glow, brighter on
//   the side spinning towards us (Doppler beaming), hotter near the middle;
// - it escapes: whatever star lies in the direction it ended up going.
// The bending is why you see the disk's far side arching OVER and UNDER the
// shadow: light from behind the hole is bent around it towards you.
//
// The camera drifts in from far away, sinks to the plane of the disk and
// circles it, while an orchestra made of synths plays: strings, cellos, a
// piano figure chasing its own echo, choir, pipe organ, timpani and a clock
// that never stops ticking -- eight [LiveLoop]s, 32 bars at 76 BPM, looping.
// The disk glows brighter as the music grows.
//
// Space: freeze the camera.  H: hide the labels.  Wheel: closer / farther.
public class AsciiBlackHole : Sketch
{
    // ---- the screen -------------------------------------------------------------
    const int Cols = 120, Rows = 36;
    const float CellW = 8, CellH = 15;
    const string Ramp = "" .,:;-~=+*xX#%&@$"";       // the gas, faint to blazing
    const string StarGlyphs = "".+*"";

    PFont mono;
    readonly float[] glow = new float[Cols * Rows];   // 0..1 brightness of each ray
    readonly float[] heat = new float[Cols * Rows];   // 0 = star / cool gas, 1 = hottest gas
    readonly float[] blue = new float[Cols * Rows];   // Doppler: >0 coming at us, <0 going away

    // ---- the physics (units: the event horizon has radius 1) ------------------------
    const float DiskIn = 3, DiskOut = 11;             // the disk, from the innermost stable orbit outward
    float[,] swirl;                                   // the gas texture: [radius bin, angle bin]
    const int SwirlR = 64, SwirlA = 256;

    // ---- the camera ---------------------------------------------------------------------
    double musicStart = -1;
    bool heardMusic, frozen, labels = true;
    float frozenFor, zoomOut = 1;
    double lastBeat;

    public override void Setup()
    {
        Size(960, 540);
        Bpm = 76;
        mono = CreateFont(""Consolas"", 15);

        // Gas: arcs of brighter and darker gas. Noise sampled around circles, so it wraps
        // seamlessly; the angle is stretched so the clumps come out as long streaks.
        swirl = new float[SwirlR, SwirlA];
        for (int ri = 0; ri < SwirlR; ri++)
            for (int ai = 0; ai < SwirlA; ai++)
            {
                float a = ai * TWO_PI / SwirlA, r = ri / (float)SwirlR;
                float n = Noise(r * 14 + Cos(a) * 1.6f, Sin(a) * 1.6f + r * 3) * 0.7f
                        + Noise(r * 40 + Cos(a) * 4, Sin(a) * 4) * 0.3f;
                swirl[ri, ai] = Constrain((n - 0.3f) * 2.2f, 0, 1);
            }
    }

    public override void OnNote(NoteEvent e) => heardMusic = true;

    double SongBeat()
    {
        double b = Beat();
        if (heardMusic && musicStart < 0)
            musicStart = Math.Floor(b / 4 + 1e-6) * 4;
        if (musicStart < 0)
            return Millis() > 2500 ? b : 0;
        return Math.Max(0, b - musicStart);
    }

    public override void Draw()
    {
        double song = SongBeat();
        if (frozen) frozenFor += (float)(song - lastBeat);
        lastBeat = song;
        float camBeat = (float)song - frozenFor;
        float t = Millis() / 1000f;

        var (eye, look) = Camera(camBeat);
        Trace(eye, look, t, FrameCount % 2);             // half the rows each frame: 30 full pictures a second

        Background(0);
        TextFont(mono, 15);
        TextAlign(TextAlignH.Center, TextAlignV.Center);
        float swell = Swell((float)song);
        for (int r = 0; r < Rows; r++)
            for (int c = 0; c < Cols; c++)
            {
                int i = r * Cols + c;
                float v = glow[i];
                if (v < 0.03f) continue;
                float h = heat[i];
                char glyph;
                if (h <= 0)
                {
                    glyph = StarGlyphs[(int)Constrain(v * StarGlyphs.Length, 0, StarGlyphs.Length - 1)];
                    Fill(190, 205, 255, 255 * Min(1, v * 1.5f + 0.2f));    // a star
                }
                else
                {
                    // Gas: deep red outside, orange, then white-hot inside; the side coming at us bluer-white.
                    float d = blue[i];
                    float red = 255, green = Constrain(70 + 200 * h + 60 * d, 40, 255), bl = Constrain(20 + 230 * h * h + 120 * d, 10, 255);
                    float k = 0.45f + 0.55f * Min(1, v * (1.2f + 0.5f * swell));
                    Fill(red * k, green * k, bl * k);
                    glyph = Ramp[(int)Constrain(Sqrt(v) * Ramp.Length, 1, Ramp.Length - 1)];
                }
                Text(glyph.ToString(), (c + 0.5f) * CellW, (r + 0.5f) * CellH);
            }

        if (labels) Labels(song, eye);
    }

    // =================================================================================
    // Ray tracing
    // =================================================================================
    void Trace(PVector eye, PVector look, float t, int parity)
    {
        var f = PVector.Sub(look, eye).Normalize();
        var right = f.Cross(new PVector(0, 1, 0)).Normalize();
        var up = right.Cross(f);
        float tanHalf = Tan(Radians(50) / 2);
        float aspect = Cols * CellW / (Rows * CellH);

        for (int r = parity; r < Rows; r += 2)
            for (int c = 0; c < Cols; c++)
            {
                float sx = ((c + 0.5f) / Cols * 2 - 1) * tanHalf * aspect;
                float sy = (1 - (r + 0.5f) / Rows * 2) * tanHalf;
                float dx = f.X + right.X * sx + up.X * sy, dy = f.Y + right.Y * sx + up.Y * sy, dz = f.Z + right.Z * sx + up.Z * sy;
                float dl = Sqrt(dx * dx + dy * dy + dz * dz);
                dx /= dl; dy /= dl; dz /= dl;
                Ray(eye.X, eye.Y, eye.Z, dx, dy, dz, t, r * Cols + c);
            }
    }

    void Ray(float px, float py, float pz, float vx, float vy, float vz, float t, int i)
    {
        // h² = |p x v|²: constant along the ray; it sets how hard the hole pulls it sideways.
        float cx = py * vz - pz * vy, cy = pz * vx - px * vz, cz = px * vy - py * vx;
        float h2 = cx * cx + cy * cy + cz * cz;
        float light = 0, hot = 0, shift = 0, seen = 1;  // seen: how much of what's behind still gets through
        float start = Sqrt(px * px + py * py + pz * pz);

        // Far from the hole the ray is a straight line: skip the stepping.
        bool straight = h2 > 22 * 22;
        for (int step = 0; step < (straight ? 1 : 260); step++)
        {
            float r2 = px * px + py * py + pz * pz, rr = Sqrt(r2);
            float dt = straight ? 1e6f : Constrain(0.08f * rr, 0.04f, 1.2f);
            if (straight)
            {
                // Straight line: does it cross the disk plane ahead of us?
                if (vy != 0 && py * vy < 0)
                {
                    float k = -py / vy;
                    Disk(px + vx * k, pz + vz * k, vx, vy, vz, t, ref light, ref hot, ref shift, ref seen);
                }
                break;
            }
            if (rr < 1) { seen = 0; break; }                                  // fell in
            if (rr > start + 2 && px * vx + py * vy + pz * vz > 0) break;       // on its way out

            float a = -1.5f * h2 / (r2 * r2 * rr);
            vx += px * a * dt; vy += py * a * dt; vz += pz * a * dt;
            float nx = px + vx * dt, ny = py + vy * dt, nz = pz + vz * dt;
            if (py * ny < 0)                                                    // crossed the disk plane
            {
                float k = py / (py - ny);
                Disk(px + (nx - px) * k, pz + (nz - pz) * k, vx, vy, vz, t, ref light, ref hot, ref shift, ref seen);
                if (seen < 0.02f) break;
            }
            px = nx; py = ny; pz = nz;
        }

        if (seen > 0.02f)
        {
            // Escaped: a star, if there's one in that direction.
            float vl = Sqrt(vx * vx + vy * vy + vz * vz);
            // The sky is cut in tiny cells (~a third of a character); a few of them hold a star.
            int sx = (int)Math.Floor(vx / vl * 400), sy = (int)Math.Floor(vy / vl * 400), sz = (int)Math.Floor(vz / vl * 400);
            uint hsh = (uint)(sx * 73856093 ^ sy * 19349663 ^ sz * 83492791);
            hsh ^= hsh >> 13; hsh *= 0x5bd1e995; hsh ^= hsh >> 15;
            float s = (hsh & 0xffff) / 65535f;
            if (s > 0.9965f)
                light += seen * (0.25f + (s - 0.9965f) / 0.0035f * 0.75f);
        }
        glow[i] = Min(1, light);
        heat[i] = hot;
        blue[i] = shift;
    }

    /// <summary>A ray going through the disk at (x, 0, z): adds the gas' glow, and the gas hides what's behind it.</summary>
    void Disk(float x, float z, float vx, float vy, float vz, float t, ref float light, ref float hot, ref float shift, ref float seen)
    {
        float r = Sqrt(x * x + z * z);
        if (r < DiskIn || r > DiskOut) return;
        // The gas orbits: inner rings faster (Kepler), so the streaks wind up over time.
        float omega = 0.45f / (r * Sqrt(r));
        float angle = Atan2(z, x) - omega * t * 6;
        int ai = (int)((angle / TWO_PI % 1 + 1) % 1 * SwirlA) % SwirlA;
        int ri = (int)Constrain((r - DiskIn) / (DiskOut - DiskIn) * SwirlR, 0, SwirlR - 1);
        float edge = Smooth(DiskIn, DiskIn + 0.6f, r) * (1 - Smooth(DiskOut - 3, DiskOut, r));
        float temp = Pow(DiskIn / r, 1.6f);                                   // hotter inside
        // Doppler: the orbital velocity (counterclockwise seen from above) against the
        // direction to the camera (-v). Coming at us: brighter and bluer.
        float speed = Sqrt(0.5f / r);
        float tx = -z / r, tz = x / r;
        float vl = Sqrt(vx * vx + vy * vy + vz * vz);
        float towards = -(tx * vx + tz * vz) / vl * speed;                     // -0.4 .. 0.4
        float beam = Pow(1 / Max(0.2f, 1 - towards), 3);
        float gas = edge * (0.35f + 0.65f * swirl[ri, ai]);
        float add = seen * gas * temp * beam * 1.5f;
        light += add;
        if (add > 0.02f && hot == 0) { hot = Constrain(temp * beam * 0.8f, 0.05f, 1); shift = Constrain(towards * 2.5f, -1, 1); }
        seen *= 1 - Constrain(gas * 0.9f, 0, 0.92f);
    }

    static float Smooth(float a, float b, float x)
    {
        float k = Math.Clamp((x - a) / (b - a), 0, 1);
        return k * k * (3 - 2 * k);
    }

    // =================================================================================
    // The camera: one continuous move per pass of the song, ending where it began
    // (same place, standing still), so the loop never jumps.
    // =================================================================================
    (PVector, PVector) Camera(float beat)
    {
        float b = beat % 128;                                       // the 32-bar song
        // In from far away, then a slow creep closer all through the build and the horizon...
        float dist = Lerp(44, 14, Smooth(0, 40, b)) - 4.5f * Smooth(36, 108, b);
        // Tilt above the disk: high at first, almost edge-on by the climax (the classic
        // view, the far side of the disk bent over the top).
        float tilt = Lerp(28, 4, Smooth(4, 40, b));
        // ...and in ""after"", a long pull back up and out to where it started.
        float leave = Smooth(108, 128, b);
        dist = Lerp(dist, 44, leave) * zoomOut;
        tilt = Lerp(tilt, 28, leave);
        float around = Radians(-20 + beat * 1.4f);                  // an orbit that never stops
        float el = Radians(tilt);
        var eye = new PVector(Cos(around) * Cos(el) * dist, Sin(el) * dist, Sin(around) * Cos(el) * dist);
        return (eye, new PVector(0, 0, 0));
    }

    /// <summary>How loud the orchestra is in this bar, 0..1: the disk glows with it.</summary>
    static float Dynamics(int bar) =>
        bar < 4 ? 0.15f :
        bar < 12 ? 0.4f :
        bar < 16 ? 0.4f + 0.6f * (bar - 11) / 4f :     // the build
        bar < 28 ? 1f :
        0.4f - 0.1f * (bar - 28);                       // after

    float Swell(float beat)
    {
        int bar = (int)(beat / 4) % 32;
        // In the climax the disk also throbs with the timpani's heartbeat.
        return Dynamics(bar) + (bar >= 16 && bar < 28 ? Pulse(4, 4) * 0.5f : 0);
    }

    void Labels(double song, PVector eye)
    {
        int bar = (int)(song / 4) % 32;
        string part = bar < 4 ? ""the void"" : bar < 12 ? ""the pull"" : bar < 16 ? ""the build"" : bar < 28 ? ""the horizon"" : ""after"";
        float d = Sqrt(eye.X * eye.X + eye.Y * eye.Y + eye.Z * eye.Z);
        TextFont(mono, 13);
        TextAlign(TextAlignH.Left, TextAlignV.Bottom);
        Fill(255, 255, 255, 100);
        Text($""bar {bar + 1}/32 · {part} · distance {d:0.0} horizon radii{(frozen ? "" · camera frozen"" : """")}"", 14, Height - 26);
        Fill(255, 255, 255, 55);
        Text(""Space: freeze camera · wheel: closer/farther · H: hide labels"", 14, Height - 9);
    }

    public override void KeyPressed()
    {
        if (Key == ' ') frozen = !frozen;
        else if (Key == 'h' || Key == 'H') labels = !labels;
    }

    public override void MouseWheel(float delta) => zoomOut = Constrain(zoomOut * (delta > 0 ? 1.1f : 1 / 1.1f), 0.6f, 2.5f);

    // =================================================================================
    // The score: 76 BPM, 32 bars in A minor, looping. An orchestra made of synths:
    //   bars 0-3    the void:    a clock, a low drone, a high shimmer
    //   bars 4-11   the pull:    a piano figure (with its echo) and soft strings
    //   bars 12-15  the build:   strings and cellos grow, timpani roll, a rising wind
    //   bars 16-27  the horizon: everything -- organ, choir, timpani like a heartbeat
    //   bars 28-31  after:       one last chord, the piano alone, the clock
    // Each chord lasts two bars: Am F C G | Dm F Esus4 E.
    // =================================================================================
    static readonly string[][] Harmony =
    {
        new[] { ""A2"", ""E3"", ""A3"", ""C4"", ""E4"" },     // Am
        new[] { ""F2"", ""C3"", ""A3"", ""C4"", ""F4"" },     // F
        new[] { ""C3"", ""G3"", ""C4"", ""E4"", ""G4"" },     // C
        new[] { ""G2"", ""D3"", ""B3"", ""D4"", ""G4"" },     // G
        new[] { ""D3"", ""A3"", ""D4"", ""F4"", ""A4"" },     // Dm
        new[] { ""F2"", ""C3"", ""A3"", ""C4"", ""F4"" },     // F
        new[] { ""E2"", ""B2"", ""E3"", ""A3"", ""B3"" },     // Esus4: holds its breath...
        new[] { ""E2"", ""B2"", ""G#3"", ""B3"", ""E4"" },    // E: ...and leans back home
    };

    static string[] ChordAt(int bar) => Harmony[(bar / 2) % Harmony.Length];

    int clockBar, droneBar, stringBar, celloBar, pianoBar, choirBar, organBar, timpaniBar;

    [LiveLoop]
    async Task Clock()
    {
        int bar = clockBar++ % 32;
        // The tick that never stops; it steps back a little when the orchestra is loud.
        double soft = bar >= 16 && bar < 28 ? 0.6 : 1;
        for (int i = 0; i < 8; i++)
        {
            Sample(""hat"", amp: (i % 2 == 0 ? 0.20 : 0.08) * soft, rate: 1.6, pan: -0.3);
            await Sleep(0.5);
        }
    }

    [LiveLoop]
    async Task Drone()
    {
        int bar = droneBar++ % 32;
        if (bar % 2 == 0)
        {
            double root = Notes.Parse(ChordAt(bar)[0]) - (bar >= 16 && bar < 28 ? 12 : 0);
            Play(root, amp: bar < 4 ? 0.15 : bar >= 16 && bar < 28 ? 0.26 : 0.2, attack: 2, sustain: 4, release: 3, synth: Synth.Sine);
            if (bar < 4)   // the void: a faint shimmer of high harmonics
                foreach (int up in new[] { 31, 36, 40 })
                    Play(root + up, amp: 0.025, attack: 3, sustain: 2, release: 3, pan: up % 2 == 0 ? 0.5 : -0.5, synth: Synth.Triangle);
        }
        await Sleep(4);
    }

    [LiveLoop]
    async Task Strings()
    {
        int bar = stringBar++ % 32;
        float dyn = Dynamics(bar);
        if (bar >= 4 && bar % 2 == 0 && bar < 30)
        {
            // A section: each note played by two saws a hair out of tune with each other,
            // so they beat slowly against each other like a row of violins. Louder = brighter.
            var chord = ChordAt(bar);
            bool last = bar == 28;                  // the last chord rings out
            for (int k = 1; k < chord.Length; k++)
            {
                double note = Notes.Parse(chord[k]) + 12;
                foreach (double detune in new[] { -0.08, 0.08 })
                    Play(note + detune, amp: 0.012 + 0.05 * dyn, attack: 2, sustain: last ? 8 : 5, release: last ? 6 : 3,
                         cutoff: 72 + 22 * dyn, pan: detune * 4, synth: Synth.Saw);
            }
        }
        await Sleep(4);
    }

    [LiveLoop]
    async Task Cellos()
    {
        int bar = celloBar++ % 32;
        // Half notes that walk under the strings: root, fifth, third, root.
        var chord = ChordAt(bar);
        // chord[0] is the root, chord[1] the fifth, chord[3] the third (an octave down here).
        double[] walk = bar % 2 == 0
            ? new double[] { Notes.Parse(chord[0]), Notes.Parse(chord[1]) }
            : new double[] { Notes.Parse(chord[3]) - 12, Notes.Parse(chord[0]) };
        foreach (double note in walk)
        {
            if (bar >= 8 && bar < 29)
            {
                float dyn = Dynamics(bar);
                Play(note, amp: 0.10 + 0.10 * dyn, attack: 0.25, sustain: 1.4, release: 0.6, cutoff: 66 + 14 * dyn, synth: Synth.Saw);
            }
            await Sleep(2);
        }
    }

    [LiveLoop]
    async Task Piano()
    {
        int bar = pianoBar++ % 32;
        // Eight notes a bar, rising and falling through the chord, and an echo three
        // sixteenths behind on the other side -- the figure seems to chase itself.
        var chord = ChordAt(bar);
        int[] shape = { 2, 3, 4, 3, 2, 4, 3, 4 };
        bool plays = bar >= 4 && bar != 15;      // one bar of silence before the horizon
        double amp = bar >= 16 && bar < 28 ? 0.12 : 0.15;
        for (int s = 0; s < 16; s++)
        {
            if (plays)
            {
                if (s % 2 == 0)
                    Play(Notes.Parse(chord[shape[s / 2]]) + 12, amp: amp, release: 1.2, pan: 0.25, synth: Synth.Pluck);
                if (s >= 3 && (s - 3) % 2 == 0)
                    Play(Notes.Parse(chord[shape[(s - 3) / 2]]) + 12, amp: amp * 0.35, release: 1.0, pan: -0.45, synth: Synth.Pluck);
            }
            await Sleep(0.25);
        }
    }

    [LiveLoop]
    async Task Choir()
    {
        int bar = choirBar++ % 32;
        // High voices on the third and fifth, only at the horizon.
        if (bar >= 16 && bar < 28 && bar % 2 == 0)
        {
            var chord = ChordAt(bar);
            foreach (int k in new[] { 3, 4 })
            {
                double note = Notes.Parse(chord[k]) + 12;
                Play(note, amp: 0.05, attack: 2.5, sustain: 4, release: 2.5, pan: k == 3 ? -0.3 : 0.3, synth: Synth.Triangle);
                Play(note + 0.05, amp: 0.035, attack: 3, sustain: 3.5, release: 2.5, pan: k == 3 ? 0.3 : -0.3, synth: Synth.Sine);
            }
        }
        await Sleep(4);
    }

    [LiveLoop]
    async Task Organ()
    {
        int bar = organBar++ % 32;
        // The pipe organ: squares in octaves, slow to open, at the horizon only.
        if (bar >= 16 && bar < 28 && bar % 2 == 0)
            foreach (var note in ChordAt(bar))
            {
                Play(note, amp: 0.055, attack: 1.2, sustain: 5, release: 2.5, cutoff: 84, synth: Synth.Square);
                Play(Notes.Parse(note) + 12, amp: 0.03, attack: 1.6, sustain: 4.5, release: 2.5, cutoff: 90, synth: Synth.Square);
            }
        // The final chord: everything rings out over A.
        if (bar == 28)
            foreach (var note in Harmony[0])
                Play(note, amp: 0.05, attack: 0.3, sustain: 4, release: 8, cutoff: 80, synth: Synth.Square);
        await Sleep(4);
    }

    [LiveLoop]
    async Task Timpani()
    {
        int bar = timpaniBar++ % 32;
        if (bar >= 12 && bar < 16)
        {
            // The build: a roll that gets faster and louder, with a wind rising under it in the last bar.
            if (bar == 15)
                Play(48, amp: 0.10, attack: 3.8, release: 0.3, cutoff: 96, synth: Synth.Noise);
            int perBeat = bar < 14 ? 2 : bar == 14 ? 4 : 6;
            for (int i = 0; i < 4 * perBeat; i++)
            {
                double grow = (bar - 12 + i / (4.0 * perBeat)) / 4;
                Sample(""kick"", amp: 0.15 + 0.45 * grow, rate: 0.55);
                await Sleep(1.0 / perBeat);
            }
        }
        else if (bar >= 16 && bar < 28)
        {
            // The horizon: a heartbeat, and a big hit at the start of every chord.
            Sample(""kick"", amp: bar % 2 == 0 ? 0.9 : 0.6, rate: 0.55);
            await Sleep(0.5);
            Sample(""kick"", amp: 0.35, rate: 0.6);
            await Sleep(3.5);
        }
        else if (bar == 28)
        {
            Sample(""kick"", amp: 0.9, rate: 0.5);     // the last chord lands
            await Sleep(4);
        }
        else
            await Sleep(4);
    }
}
";

        private const string AsciiDisco =
@"// A 3D field of ASCII characters, and you're standing in the middle of it.
//
// The camera looks around, walks, climbs and orbits, and then pulls far
// away and up -- and from up there the field turns out to be a screen: the
// characters, all together, are a dancer on a disco floor, dancing to the
// song that's been playing the whole time.
//
// Everything is DanaProcessing and code:
// - The picture: every frame a small dancer is drawn on an offscreen
//   PGraphics (lines and circles, posed from Beat()), and each pixel pair
//   becomes one character of the field: its brightness picks the glyph
//   ("" .:-=+*#%@"") and how high it floats, its color is the glyph's color.
// - The 3D: a tiny camera in plain math (position + where it looks ->
//   right/up/forward vectors -> perspective divide). Every character is
//   placed in 3D, sorted far to near and drawn with Text() at the size its
//   distance gives it.
// - The music: five [LiveLoop]s (pad, bass, lead, drums, hats) at 116 BPM, a
//   32-bar song that loops. The camera path is timed in beats of that song.
//
// Space: freeze the camera (the music goes on).  H: hide the labels.
public class AsciiDisco : Sketch
{
    // ---- the field ----------------------------------------------------------
    const int GW = 96, GH = 54;                 // characters across and deep
    const int PicW = GW * 2, PicH = GH * 2;     // the dancer is drawn 2x finer, then averaged
    const string Glyphs = "" .:-=+*#%@"";

    PGraphics pic;
    readonly float[] lum = new float[GW * GH];
    readonly Color[] tint = new Color[GW * GH];
    PFont mono;

    // ---- the camera -----------------------------------------------------------
    const float FovY = 60;
    const int SongBeats = 128;                  // 32 bars, then it all loops
    readonly float[] depth = new float[GW * GH];
    readonly int[] order = new int[GW * GH];
    readonly float[] sx = new float[GW * GH], sy = new float[GW * GH], ss = new float[GW * GH];
    float frozenFor;                            // beats the camera spent frozen
    bool frozen, labels = true;
    double lastBeat;

    // Where the camera is and what it looks at, at a beat of the song.
    // Catmull-Rom through these: smooth, no stops.
    static readonly (float Beat, PVector Pos, PVector Look)[] Path =
    {
        (0,   new PVector(0, 1.4f, 0),     new PVector(0, 1.6f, 12)),    // in the middle, looking ahead
        (12,  new PVector(0, 1.4f, 0),     new PVector(12, 1.2f, 2)),    // turn to the right
        (22,  new PVector(-6, 1.8f, -3),   new PVector(10, 0.6f, 6)),    // step back and sideways
        (32,  new PVector(-12, 3.5f, 6),   new PVector(6, 0, -4)),       // up a little, looking across
        (42,  new PVector(-6, 9, 18),      new PVector(0, 0, 0)),        // climbing, circling
        (50,  new PVector(16, 16, 14),     new PVector(0, 0, 0)),
        (58,  new PVector(22, 32, -10),    new PVector(0, 0, 0)),
        (66,  new PVector(0, 52, -9),      new PVector(0, 0, 0.5f)),     // way up: the field is a screen
        (100, new PVector(0, 54, -8),      new PVector(0, 0, 0.5f)),     // ...and stays there a while
        (110, new PVector(10, 18, -18),    new PVector(0, 1, 2)),        // dive back in
        (120, new PVector(2, 2.5f, -6),    new PVector(0, 1.6f, 8)),
        (128, new PVector(0, 1.4f, 0),     new PVector(0, 1.6f, 12)),    // = beat 0: the loop closes
    };

    // ---- the song ---------------------------------------------------------------
    double musicStart = -1;
    bool heardMusic;
    int padBar, bassBar, leadBar, drumBar, hatBar;

    static readonly string[][] Chords =
    {
        new[] { ""F3"", ""A3"", ""C4"", ""E4"" },   // Fmaj7
        new[] { ""E3"", ""G3"", ""B3"", ""D4"" },   // Em7
        new[] { ""D3"", ""F3"", ""A3"", ""C4"" },   // Dm7
        new[] { ""C3"", ""E3"", ""G3"", ""B3"" },   // Cmaj7
    };
    static readonly string[] Roots = { ""F2"", ""E2"", ""D2"", ""C2"" };

    // Sections, in bars of the 32-bar song.
    static bool Drums(int bar) => bar >= 4 && bar < 28;
    static bool Full(int bar) => bar >= 12 && bar < 28;
    static bool Lead(int bar) => bar >= 16 && bar < 28;

    public override void Setup()
    {
        Size(960, 540);
        Bpm = 116;
        mono = CreateFont(""Consolas"", 32);
        pic = CreateGraphics(PicW, PicH);
        for (int i = 0; i < order.Length; i++) order[i] = i;
    }

    public override void OnNote(NoteEvent e) => heardMusic = true;

    /// <summary>Beats since the song started (the loops wait for the next bar to start; no sound at all: since Run).</summary>
    double SongBeat()
    {
        double b = Beat();
        if (heardMusic && musicStart < 0)
            musicStart = Math.Floor(b / 4 + 1e-6) * 4;
        if (musicStart < 0)
            return Millis() > 2500 ? b : 0;
        return Math.Max(0, b - musicStart);
    }

    public override void Draw()
    {
        double song = SongBeat();
        if (frozen) frozenFor += (float)(song - lastBeat);
        lastBeat = song;

        PaintDancer((float)song);
        Background(6, 4, 14);
        DrawField((float)(song - frozenFor), (float)song);
        if (labels) DrawLabels(song);
    }

    // ===========================================================================
    // The picture: a dancer on a disco floor, posed from the beat.
    // ===========================================================================
    void PaintDancer(float beat)
    {
        int bar = (int)(beat / 4) % 32;
        float kick = Drums(bar) ? Pulse(1, 5) : 0;
        pic.BeginDraw();
        pic.Background(26, 12, 52);
        pic.NoStroke();

        // A purple haze behind the dancer, breathing with the kick.
        for (int k = 9; k >= 1; k--)
        {
            pic.Fill(90 + 12 * k * kick, 40, 150, 30);
            pic.Ellipse(PicW / 2f, PicH * 0.55f, k * 22 * (1 + 0.08f * kick), k * 13);
        }
        // Little lights from the mirror ball, drifting across the room.
        for (int k = 0; k < 40; k++)
        {
            float a = k * 2.39996f + beat * 0.25f;
            float x = PicW / 2f + Cos(a) * (20 + (k * 37) % 90), y = 14 + ((k * 53) % 60) + Sin(a) * 6;
            pic.FillHSB((k * 31 + beat * 20) % 360, 50, 100, 120 + 100 * Sin(beat * PI + k));
            pic.Circle(x, y, 2.2f);
        }

        // Two spotlights sweeping, crossing on the downbeats.
        for (int s = 0; s < 2; s++)
        {
            float a = Sin(beat * PI / 8 + s * PI) * 0.6f;
            float ox = s == 0 ? 30 : PicW - 30;
            float tx = PicW / 2f + Sin(a + beat * 0.2f) * 70, ty = PicH - 18;
            pic.Fill(s == 0 ? 255 : 120, s == 0 ? 90 : 200, s == 0 ? 200 : 255, 60 + 60 * kick);
            pic.Triangle(ox, -4, tx - 22, ty, tx + 22, ty);
        }

        // The floor: tiles that light up with the beat, one color per bar.
        int tilesX = 12, tilesY = 3;
        float floorTop = PicH - 22;
        for (int ty = 0; ty < tilesY; ty++)
            for (int tx = 0; tx < tilesX; tx++)
            {
                bool lit = ((tx + ty + (int)beat) % 3 == 0) && Drums(bar);
                float hue = (bar * 47 + tx * 20 + ty * 60) % 360;
                if (lit) pic.FillHSB(hue, 70, 55 + 45 * kick);
                else pic.Fill(28, 20, 52);
                float w = PicW / (float)tilesX;
                pic.Rect(tx * w + 1, floorTop + ty * 7.5f + 1, w - 2, 6);
            }

        // The mirror ball, top right: facets that turn.
        float ballX = PicW * 0.8f, ballY = 9;
        pic.Fill(200, 200, 220);
        pic.Circle(ballX, ballY, 15);
        for (int k = 0; k < 6; k++)
        {
            float fx = ballX - 6 + ((k * 2 + (int)(beat * 2)) % 6) * 2.2f;
            pic.Fill(255, 255, 255);
            pic.Rect(fx, ballY - 4 + (k % 3) * 3, 1.6f, 1.6f);
        }
        // A neon sign in the corner.
        pic.TextFont(mono, 15);
        pic.TextAlign(TextAlignH.Left, TextAlignV.Top);
        pic.Fill(255, 80, 180, Drums(bar) && (int)(beat * 2) % 8 == 7 ? 90 : 255);
        pic.Text(""DANA"", 8, 6);

        // The dancer, feet on the floor at (0, 0). One of four moves, each lasting two bars.
        pic.PushMatrix();
        pic.Translate(PicW / 2f, floorTop);
        pic.Scale(1.25f);
        float step = Sin(beat * PI);                    // a side step every beat
        float bounce = Abs(Sin(beat * PI)) * 3;
        float hipX = step * 5, hipY = -26 + bounce;
        float neckX = hipX - step * 2, neckY = hipY - 26;
        int move = (int)(beat / 8) % 4;
        float ph = beat * PI;                           // half a cycle per beat

        // Arms: (shoulder angle, elbow angle) for each side; angles in radians, 0 = pointing right, up is -PI/2.
        (float s, float e) left, right;
        switch (move)
        {
            case 0:  // pump: fists go up one at a time
                left = (-PI * 0.75f + Max(0, Sin(ph)) * -0.9f, -PI * 0.5f);
                right = (-PI * 0.25f + Max(0, -Sin(ph)) * 0.9f * -1, -PI * 0.5f);
                break;
            case 1:  // point: one arm up to the corner, then down across
                bool up = Sin(ph / 2) > 0;
                right = up ? (-PI * 0.3f, -PI * 0.35f) : (PI * 0.25f, PI * 0.35f);
                left = (PI * 0.7f, PI * 0.6f);
                break;
            case 2:  // wave: both arms up, swaying
                left = (-PI * 0.62f + Sin(ph) * 0.25f, -PI * 0.55f + Sin(ph) * 0.3f);
                right = (-PI * 0.38f + Sin(ph) * 0.25f, -PI * 0.45f + Sin(ph) * 0.3f);
                break;
            default: // roll: forearms circling in front of the chest
                left = (PI * 0.85f, -PI * 0.1f + Sin(ph * 2) * 0.8f);
                right = (PI * 0.15f, PI * 1.1f + Cos(ph * 2) * 0.8f);
                break;
        }

        pic.StrokeCap(StrokeCapKind.Round);
        // Legs: knees bend in turn.
        pic.Stroke(90, 200, 255);
        pic.StrokeWeight(5);
        for (int side = -1; side <= 1; side += 2)
        {
            float bend = Max(0, side * step) * 6;
            float kneeX = hipX + side * 6 + side * bend * 0.4f, kneeY = hipY + 12 - bend * 0.3f;
            pic.Line(hipX + side * 3, hipY, kneeX, kneeY);
            pic.Line(kneeX, kneeY, hipX + side * 9, -1);
        }
        // Body.
        pic.Stroke(255, 120, 190);
        pic.StrokeWeight(9);
        pic.Line(hipX, hipY - 2, neckX, neckY + 4);
        // Arms.
        pic.Stroke(255, 200, 150);
        pic.StrokeWeight(4);
        Arm(neckX - 4, neckY + 3, left);
        Arm(neckX + 4, neckY + 3, right);
        // Head, with shades.
        pic.NoStroke();
        pic.Fill(255, 215, 170);
        pic.Circle(neckX, neckY - 6, 13);
        pic.Fill(40, 230, 255);
        pic.Rect(neckX - 6, neckY - 8, 12, 3);
        pic.PopMatrix();

        pic.LoadPixels();
        var px = pic.Pixels;
        for (int r = 0; r < GH; r++)
            for (int c = 0; c < GW; c++)
            {
                int R = 0, G = 0, B = 0;
                for (int dy = 0; dy < 2; dy++)
                    for (int dx = 0; dx < 2; dx++)
                    {
                        var p = px[(r * 2 + dy) * PicW + c * 2 + dx];
                        R += p.R; G += p.G; B += p.B;
                    }
                int i = r * GW + c;
                lum[i] = (0.3f * R + 0.55f * G + 0.15f * B) / (4 * 255f);
                tint[i] = Color(R / 4, G / 4, B / 4);
            }
        pic.EndDraw();
    }

    void Arm(float x, float y, (float s, float e) a)
    {
        const float upper = 11, fore = 10;
        float ex = x + Cos(a.s) * upper, ey = y + Sin(a.s) * upper;
        pic.Line(x, y, ex, ey);
        pic.Line(ex, ey, ex + Cos(a.e) * fore, ey + Sin(a.e) * fore);
    }

    // ===========================================================================
    // The 3D: every character placed in space, seen from the camera.
    // ===========================================================================
    void DrawField(float camBeat, float song)
    {
        var (eye, look) = CameraAt(camBeat);
        // Camera axes: forward, right, up.
        var f = PVector.Sub(look, eye).Normalize();
        // ""Up"" is the sky when we look ahead, and the far edge of the field (+z) when we
        // look down at it -- with the sky as up, looking straight down would spin the picture.
        float down = Constrain(-f.Y * 1.4f - 0.3f, 0, 1);
        var upHint = new PVector(0, 1 - down, down);
        var r = f.Cross(upHint).Normalize();
        var u = r.Cross(f);
        float focal = Height / 2f / Tan(Radians(FovY) / 2);
        float kick = Drums((int)(song / 4) % 32) ? Pulse(1, 4) : 0;

        int n = 0;
        for (int row = 0; row < GH; row++)
            for (int col = 0; col < GW; col++)
            {
                int i = row * GW + col;
                float v = lum[i];
                // Column -> x (mirrored, so from above the picture isn't), row -> z.
                float wx = GW / 2f - col - 0.5f, wz = GH / 2f - row - 0.5f;
                float ring = Sin(Sqrt(wx * wx + wz * wz) * 0.35f - song * PI) * 0.35f;
                float wy = 0.2f + v * 4.5f + ring * (0.3f + kick);
                float dx = wx - eye.X, dy = wy - eye.Y, dz = wz - eye.Z;
                float zc = dx * f.X + dy * f.Y + dz * f.Z;
                if (zc < 0.35f) continue;                         // behind us, or in our face
                float xc = dx * r.X + dy * r.Y + dz * r.Z;
                float yc = dx * u.X + dy * u.Y + dz * u.Z;
                float size = focal * 0.95f / zc;
                float x = Width / 2f + focal * xc / zc, y = Height / 2f - focal * yc / zc;
                if (size < 2.5f || x < -size || x > Width + size || y < -size || y > Height + size) continue;
                sx[i] = x; sy[i] = y; ss[i] = Min(size, 120);
                depth[n] = -zc;                                   // far first
                order[n] = i;
                n++;
            }
        System.Array.Sort(depth, order, 0, n);

        TextFont(mono, 32);
        TextAlign(TextAlignH.Center, TextAlignV.Center);
        for (int k = 0; k < n; k++)
        {
            int i = order[k];
            float v = lum[i];
            int g = (int)Constrain(Sqrt(v) * Glyphs.Length, 1, Glyphs.Length - 1);   // sqrt: the dark room still shows : and -
            float z = -depth[k];
            // Close characters fade in (no wall of glyphs on the lens), far ones dim a little.
            float alpha = 255 * Constrain((z - 0.35f) / 2.5f, 0, 1) * (0.55f + 0.45f * Min(1, v * 3 + 0.2f));
            var c = tint[i];
            float lift = 1 + 0.6f * kick * v;
            Fill(Min(255, c.R * 1.5f * lift + 25), Min(255, c.G * 1.5f * lift + 20), Min(255, c.B * 1.5f * lift + 40), alpha);
            // Whole-pixel sizes (steps of 6 when big): Skia caches each glyph per
            // size, so a few sizes draw ~5x faster than thousands of slightly different ones.
            TextSize(ss[i] < 24 ? MathF.Round(ss[i]) : MathF.Round(ss[i] / 6) * 6);
            Text(Glyphs[g].ToString(), sx[i], sy[i]);
        }
    }

    (PVector Eye, PVector Look) CameraAt(float beat)
    {
        float b = ((beat % SongBeats) + SongBeats) % SongBeats;
        int k = 0;
        while (k < Path.Length - 2 && Path[k + 1].Beat <= b) k++;
        float t = (b - Path[k].Beat) / (Path[k + 1].Beat - Path[k].Beat);
        int k0 = Math.Max(0, k - 1), k3 = Math.Min(Path.Length - 1, k + 2);
        var eye = CatmullRom(Path[k0].Pos, Path[k].Pos, Path[k + 1].Pos, Path[k3].Pos, t);
        var look = CatmullRom(Path[k0].Look, Path[k].Look, Path[k + 1].Look, Path[k3].Look, t);
        // Up top, sway gently with the music so the ""screen"" breathes.
        float high = Constrain((eye.Y - 30) / 20, 0, 1);
        eye.X += Sin(beat * PI / 8) * 3 * high;
        return (eye, look);
    }

    static PVector CatmullRom(PVector p0, PVector p1, PVector p2, PVector p3, float t)
    {
        float t2 = t * t, t3 = t2 * t;
        float F(float a, float b, float c, float d) =>
            0.5f * (2 * b + (-a + c) * t + (2 * a - 5 * b + 4 * c - d) * t2 + (-a + 3 * b - 3 * c + d) * t3);
        return new PVector(F(p0.X, p1.X, p2.X, p3.X), F(p0.Y, p1.Y, p2.Y, p3.Y), F(p0.Z, p1.Z, p2.Z, p3.Z));
    }

    void DrawLabels(double song)
    {
        int bar = (int)(song / 4) % 32;
        string part = bar < 4 ? ""intro"" : bar < 12 ? ""groove"" : bar < 16 ? ""build"" : bar < 28 ? ""chorus"" : ""breakdown"";
        TextFont(mono, 13);
        TextSize(13);
        TextAlign(TextAlignH.Left, TextAlignV.Bottom);
        Fill(255, 255, 255, 110);
        Text($""bar {bar + 1}/32 · {part}{(frozen ? "" · camera frozen"" : """")}"", 16, Height - 30);
        Fill(255, 255, 255, 60);
        Text(""Space: freeze camera · H: hide labels"", 16, Height - 12);
    }

    public override void KeyPressed()
    {
        if (Key == ' ') frozen = !frozen;
        else if (Key == 'h' || Key == 'H') labels = !labels;
    }

    // ===========================================================================
    // The song: 116 BPM, 32 bars, looping. Fmaj7 - Em7 - Dm7 - Cmaj7.
    // ===========================================================================
    [LiveLoop]
    async Task Pad()
    {
        int bar = padBar++ % 32;
        foreach (var note in Chords[bar % 4])
            Play(note, amp: Full(bar) ? 0.06 : 0.09, attack: 0.4, sustain: 2.6, release: 1.0,
                 cutoff: Full(bar) ? 90 : 80, pan: 0, synth: Synth.Saw);
        await Sleep(4);
    }

    [LiveLoop]
    async Task Bass()
    {
        int bar = bassBar++ % 32;
        double root = Notes.Parse(Roots[bar % 4]);
        for (int i = 0; i < 8; i++)
        {
            // Disco octaves: low, high, low, high...
            if (Drums(bar))
                Play(root + (i % 2 == 1 ? 12 : 0), amp: 0.26, release: 0.22, cutoff: 76, res: 0.3, synth: Synth.Saw);
            else if (i == 0)
                Play(root, amp: 0.18, release: 1.6, cutoff: 64, synth: Synth.Triangle);
            await Sleep(0.5);
        }
    }

    [LiveLoop]
    async Task Lead()
    {
        int bar = leadBar++ % 32;
        // A two-bar hook: the first bar climbs, the second one comes home.
        (string Note, double Beats)[] climb = { (""E5"", 0.5), (""G5"", 0.5), (""A5"", 1), (""G5"", 0.5), (""E5"", 0.5), (""D5"", 1) };
        (string Note, double Beats)[] home = { (""C5"", 0.5), (""D5"", 0.5), (""E5"", 1.5), ("""", 0.5), (""G4"", 0.5), (""A4"", 0.5) };
        var phrase = bar % 2 == 0 ? climb : home;
        // Every fourth bar answers an octave lower, for variety.
        int shift = bar % 8 == 7 ? -12 : 0;
        foreach (var (note, beats) in phrase)
        {
            if (Lead(bar) && note != """")
            {
                double n = Notes.Parse(note) + shift;
                Play(n, amp: 0.10, attack: 0.01, release: beats * 0.45, cutoff: 96, pan: -0.15, synth: Synth.Square);
                Play(n + 12, amp: 0.035, release: beats * 0.6, pan: 0.25, synth: Synth.Triangle);   // a soft octave on top
            }
            await Sleep(beats);
        }
    }

    [LiveLoop]
    async Task Kit()
    {
        int bar = drumBar++ % 32;
        for (int beat = 0; beat < 4; beat++)
        {
            if (Drums(bar))
                Sample(""kick"", amp: 0.9);
            if (Full(bar) && beat % 2 == 1)
                Sample(""snare"", amp: 0.55);
            // The build: snare rolls getting faster in the last bar before the chorus.
            if (bar == 15)
            {
                int hits = beat < 2 ? 2 : 4;
                for (int h = 0; h < hits; h++)
                {
                    Sample(""snare"", amp: 0.25 + 0.1 * beat);
                    await Sleep(1.0 / hits);
                }
                continue;
            }
            await Sleep(1);
        }
    }

    [LiveLoop]
    async Task Hats()
    {
        int bar = hatBar++ % 32;
        for (int i = 0; i < 8; i++)
        {
            if (Drums(bar))
                Sample(""hat"", amp: i % 2 == 1 ? (Full(bar) ? 0.32 : 0.22) : 0.1, pan: 0.25);
            await Sleep(0.5);
        }
    }
}
";

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
