namespace DanaProcessing.Ide.Editor
{
    public static partial class SketchSamples
    {
        // DanaProcessing's own 60-second promo, picture and music, as one sketch:
        // Sketch > Create reel exports it as a 1920x1080 MP4 with sound.
        private const string DanaPromo =
@"using DanaProcessing.Reels;
// @reel hide   (everything below is the sketch, not typed on screen)

// DanaProcessing's 60-second promo -- picture AND music -- in one sketch.
//
//   Sketch > Create reel > Export   ->  a 1920x1080 MP4 with sound, ready
//   for YouTube, X or Reddit. (The preview in that window is slow on the
//   shader parts; the export renders every frame, so the video is smooth.)
//
// How it's built: one bar of 120 BPM = 2 seconds, and every shot starts on a
// bar, so the cuts land on the music. The music is the [LiveLoop]s at the end;
// Seconds() reads the same clock the music plays on, so picture and sound stay
// together. Change a caption, a color or a riff and export again.
//
// @reel format: landscape
// @reel result: 61 full
// @reel outro: off
// @reel mouse: off
// @reel audio: on
public class DanaPromo : Sketch
{
    // ------------------------------------------------------------ palette & type
    static readonly Color Ink = new(0x13, 0x20, 0x2A), Ink2 = new(0x1C, 0x2E, 0x3B), OnInk = new(0xF4, 0xEB, 0xDD),
                          Muted = new(0xB9, 0xC7, 0xD2), Gold = new(0xE7, 0xB6, 0x6B), Coral = new(0xFF, 0x8A, 0x6B),
                          Mint = new(0x8F, 0xD3, 0xB6), Sky = new(0x9D, 0xB8, 0xF2);

    PFont heavy = null!, display = null!, body = null!, bodyBold = null!, mono = null!;

    /// <summary>The first font file that exists: Windows, then macOS, then Linux; the system font otherwise.</summary>
    PFont FontFrom(params string[] files)
    {
        foreach (var f in files)
            if (File.Exists(f))
                return LoadFont(f, 48);
        return CreateFont(""sans-serif"", 48);
    }

    // Video time in seconds, on the music's clock (2 beats = 1 second at 120 BPM).
    // When live loops start from silence, their first downbeat lands on the next
    // bar line, so Beat() is 4 (not 0) when the music starts: the first note we
    // hear marks that bar, and time counts from there. With no sound (the
    // preview in the Create reel window) no note arrives and Beat() starts at 0.
    double musicStart = -1;
    bool heardMusic;

    public override void OnNote(NoteEvent e) => heardMusic = true;

    float Seconds()
    {
        double b = Beat();
        if (heardMusic && musicStart < 0)
            musicStart = Math.Floor(b / 4 + 1e-6) * 4;
        return (float)Math.Max(0, b - Math.Max(0, musicStart)) / 2f;
    }

    float T;                                   // Seconds() for this frame

    public override void Setup()
    {
        Size(1920, 1080);
        Bpm = 120;
        heavy = FontFrom(@""C:\Windows\Fonts\seguibl.ttf"", ""/System/Library/Fonts/Supplemental/Arial Black.ttf"",
                         ""/usr/share/fonts/opentype/inter/InterDisplay-Black.otf"");
        display = FontFrom(@""C:\Windows\Fonts\segoeuib.ttf"", ""/System/Library/Fonts/Supplemental/Arial Bold.ttf"",
                           ""/usr/share/fonts/opentype/inter/InterDisplay-ExtraBold.otf"");
        body = FontFrom(@""C:\Windows\Fonts\segoeui.ttf"", ""/System/Library/Fonts/Supplemental/Arial.ttf"",
                        ""/usr/share/fonts/opentype/inter/Inter-Regular.otf"");
        bodyBold = FontFrom(@""C:\Windows\Fonts\seguisb.ttf"", ""/System/Library/Fonts/Supplemental/Arial Bold.ttf"",
                            ""/usr/share/fonts/opentype/inter/Inter-SemiBold.otf"");
        mono = FontFrom(@""C:\Windows\Fonts\CascadiaMono.ttf"", @""C:\Windows\Fonts\consola.ttf"", ""/System/Library/Fonts/Menlo.ttc"",
                        ""/usr/share/fonts/truetype/dejavu/DejaVuSansMono.ttf"");
        plasma = CreateShader2D(PlasmaSource);
        rings = CreateShader2D(RingsSource);
        PrepareLife();
    }

    public override void Draw()
    {
        T = Seconds();
        if (T < 6) Hook(T);
        else if (T < 10) TypedCode(T - 6, ParticleCode, ""Particles.cs"", ""Write a sketch in C#"", markLine: 11);
        else if (T < 18) Particles(T - 10);
        else if (T < 22) TypedCode(T - 18, MusicCode, ""Music.cs"", ""The music in this video is this code"", markLine: 14);
        else if (T < 30) BeatScene(T - 22);
        else if (T < 42) Stage(T - 30);
        else if (T < 52) Montage(T - 42);
        else Outro(T - 52);
    }

    // ------------------------------------------------------------ helpers
    static float Smooth(float a, float b, float t)
    {
        float x = Math.Clamp((t - a) / (b - a), 0f, 1f);
        return x * x * (3 - 2 * x);
    }

    void FillC(Color c, float alpha = 255) => Fill(c.R, c.G, c.B, alpha);

    /// <summary>A rounded rectangle as one shape (a see-through fill stays even).</summary>
    void RoundRect(float x, float y, float w, float h, float r)
    {
        r = Math.Min(r, Math.Min(w, h) / 2);
        (float cx, float cy, float from)[] corners =
            { (x + w - r, y + r, -HALF_PI), (x + w - r, y + h - r, 0), (x + r, y + h - r, HALF_PI), (x + r, y + r, PI) };
        BeginShape();
        foreach (var (cx, cy, from) in corners)
            for (int i = 0; i <= 8; i++)
                Vertex(cx + Cos(from + HALF_PI * i / 8) * r, cy + Sin(from + HALF_PI * i / 8) * r);
        EndShape(true);
    }

    /// <summary>The caption card, bottom-left; <paramref name=""t""/> is the shot's own time.</summary>
    void Caption(string title, string sub, float t, float start, float end)
    {
        float a = Smooth(start, start + 0.35f, t) * (1 - Smooth(end - 0.35f, end, t));
        if (a <= 0) return;
        float rise = (1 - Smooth(start, start + 0.5f, t)) * 24;
        PushStyle();
        TextFont(display, 54);
        float tw = TextWidth(title);
        TextFont(body, 30);
        float sw = TextWidth(sub);
        float w = Max(tw, sw) + 120, h = 150, x = 72, y = Height - 72 - h + rise;
        NoStroke();
        FillC(Ink, 225 * a);
        RoundRect(x, y, w, h, 28);
        FillC(Gold, 255 * a);
        Circle(x + 44, y + 52, 18);
        TextAlign(TextAlignH.Left, TextAlignV.Baseline);
        FillC(OnInk, 255 * a);
        TextFont(display, 54);
        Text(title, x + 76, y + 70);
        FillC(Muted, 255 * a);
        TextFont(body, 30);
        Text(sub, x + 76, y + 114);
        PopStyle();
    }

    /// <summary>The DanaProcessing mark, top-left (dark ink on light pictures).</summary>
    void Mark(bool onLight = false)
    {
        var line = onLight ? Ink : OnInk;
        PushStyle();
        NoFill();
        Stroke(line.R, line.G, line.B, 230);
        StrokeWeight(3);
        Circle(96, 84, 40);
        NoStroke();
        FillC(Gold);
        Circle(96, 84, 18);
        FillC(Coral);
        Circle(109, 70, 7);
        FillC(line, 235);
        TextFont(bodyBold, 30);
        TextAlign(TextAlignH.Left, TextAlignV.Center);
        Text(""DanaProcessing"", 132, 82);
        PopStyle();
    }

    void FadeTo(Color c, float t, float from, float to)
    {
        float a = Smooth(from, to, t);
        if (a <= 0) return;
        NoStroke();
        FillC(c, 255 * a);
        Rect(0, 0, Width, Height);
    }

    static readonly HashSet<string> Keywords = new()
        { ""public"", ""class"", ""override"", ""void"", ""float"", ""var"", ""new"", ""foreach"", ""in"", ""readonly"", ""async"", ""await"", ""int"", ""string"" };

    /// <summary>A tiny C# highlighter: keywords gold, numbers coral, strings mint, calls sky, types mint.</summary>
    void DrawCode(string line, float x, float y, float alpha = 245)
    {
        int i = 0;
        while (i < line.Length)
        {
            int j = i;
            Color c = OnInk;
            if (line[i] == '""') { j = line.IndexOf('""', i + 1); j = j < 0 ? line.Length : j + 1; c = Mint; }
            else if (line[i] == '/' && i + 1 < line.Length && line[i + 1] == '/') { j = line.Length; c = Muted; }
            else if (char.IsDigit(line[i])) { while (j < line.Length && (char.IsDigit(line[j]) || line[j] == '.' || line[j] == 'f')) j++; c = Coral; }
            else if (char.IsLetter(line[i]) || line[i] == '_')
            {
                while (j < line.Length && (char.IsLetterOrDigit(line[j]) || line[j] == '_')) j++;
                string word = line[i..j];
                if (Keywords.Contains(word)) c = Gold;
                else if (j < line.Length && line[j] == '(') c = Sky;
                else if (char.IsUpper(word[0]) && word is ""Sketch"" or ""List"" or ""Particle"" or ""PVector"" or ""Task"" or ""Synth"" or ""LiveLoop"") c = Mint;
            }
            else j = i + 1;
            string token = line[i..j];
            FillC(c, alpha);
            Text(token, x, y);
            x += TextWidth(token);
            i = j;
        }
    }

    // ------------------------------------------------------------ 0-6 s: the hook
    const string PlasmaSource = @""
uniform float iTime;
uniform float2 iResolution;
uniform float uPulse;
uniform float uLight;
half4 main(float2 fragCoord) {
    float2 uv = (fragCoord - 0.5 * iResolution) / iResolution.y;
    float d = length(uv);
    float v = (sin(10.0 * d - iTime * 2.2 + uPulse * 1.6) + sin(5.0 * uv.x + iTime * 0.7) + sin(4.0 * (uv.x + uv.y) - iTime * 0.9)) / 3.0;
    float3 ink = float3(0.075, 0.125, 0.165), gold = float3(0.906, 0.714, 0.420);
    float3 coral = float3(1.0, 0.541, 0.420), sky = float3(0.616, 0.722, 0.949);
    float3 col = mix(ink, sky, smoothstep(-0.6, 0.9, v) * 0.55);
    col = mix(col, coral, smoothstep(0.35, 1.0, v) * 0.65);
    col = mix(col, gold, smoothstep(0.75, 1.0, v));
    col += uPulse * 0.10 * exp(-d * 2.5);
    col *= uLight * (1.0 - 0.55 * smoothstep(0.2, 1.1, d));
    return half4(half3(col), 1.0);
}"";
    PShader2D plasma = null!;

    void Hook(float t)
    {
        float pulse = t >= 4 ? Pulse(1, sharpness: 4) : 0;        // the kick comes in on bar 2
        plasma.Set(""uPulse"", pulse);
        plasma.Set(""uLight"", 0.35f + 0.65f * Smooth(0, 1.2f, t));
        Shade(plasma);

        NoStroke();
        float band = 130 * Smooth(0, 0.6f, t);
        for (int y = 250; y < 830; y += 4)
        {
            FillC(Ink, band * Math.Min(Smooth(250, 330, y), 1 - Smooth(750, 830, y)));
            Rect(0, y, Width, 4);
        }

        string[] words = { ""Draw."", ""Play."", ""Perform."" };
        float[] at = { 0.25f, 1.25f, 2.25f };
        TextFont(heavy, 150);
        float gap = 48, total = gap * 2;
        foreach (var w in words) total += TextWidth(w);
        float x = (Width - total) / 2;
        TextAlign(TextAlignH.Left, TextAlignV.Baseline);
        for (int i = 0; i < words.Length; i++)
        {
            float k = Smooth(at[i], at[i] + 0.45f, t);
            FillC(i == 2 ? Gold : OnInk, 255 * k);
            Text(words[i], x, 560 + (1 - k) * 40 - pulse * 6);
            x += TextWidth(words[i]) + gap;
        }
        float s = Smooth(3.2f, 3.8f, t);
        TextFont(body, 44);
        TextAlign(TextAlignH.Center, TextAlignV.Baseline);
        FillC(OnInk, 235 * s);
        Text(""Creative coding in C#: visuals, music and live code."", Width / 2f, 660 + (1 - s) * 20);
        if (t > 0.5f) Mark();
        FadeTo(Ink, t, 5.7f, 6f);
    }

    // ------------------------------------------------------------ typed code (6-10 s, 18-22 s)
    static readonly string[] ParticleCode =
    {
        ""public class Particles : Sketch"",
        ""{"",
        ""    readonly List<Particle> swarm = new();"",
        """",
        ""    public override void Draw()"",
        ""    {"",
        ""        Fill(18, 14, 12, 64);   // see-through: trails"",
        ""        Rect(0, 0, Width, Height);"",
        ""        foreach (var p in swarm)"",
        ""        {"",
        ""            var pull = new PVector(MouseX - p.Pos.X, MouseY - p.Pos.Y);"",
        ""            p.Vel.Add(pull.SetMag(0.55f));"",
        ""            p.Pos.Add(p.Vel.Limit(13));"",
        ""            Circle(p.Pos.X, p.Pos.Y, 10);"",
        ""        }"",
        ""    }"",
        ""}"",
    };

    static readonly string[] MusicCode =
    {
        ""public class Music : Sketch"",
        ""{"",
        ""    [LiveLoop]"",
        ""    async Task Drums()"",
        ""    {"",
        ""        Sample(\""kick\"");"",
        ""        await Sleep(1);"",
        ""    }"",
        """",
        ""    [LiveLoop]"",
        ""    async Task Bass()"",
        ""    {"",
        ""        foreach (var root in new[] { \""E2\"", \""C2\"", \""G2\"", \""D2\"" })"",
        ""        {"",
        ""            Play(root, synth: Synth.Saw, cutoff: 72);"",
        ""            await Sleep(4);"",
        ""        }"",
        ""    }"",
        ""}"",
    };

    /// <summary>An editor card typing <paramref name=""code""/> in 3.2 s, then the marked line lights up.</summary>
    void TypedCode(float t, string[] code, string file, string caption, int markLine)
    {
        Background(Ink.R, Ink.G, Ink.B);
        NoStroke();
        for (int i = 0; i < 6; i++)                 // a soft glow behind the card
        {
            FillC(Ink2, 60);
            Circle(Width / 2f, Height / 2f - 40, 1300 - i * 160);
        }
        float cw = 1500, lh = 40, ch = code.Length * lh + 110;
        float cx = (Width - cw) / 2, cy = (Height - ch) / 2 - 50;
        float appear = Smooth(0, 0.35f, t);
        FillC(Ink2, 250 * appear);
        RoundRect(cx, cy, cw, ch, 22);
        float[] dots = { 0, 1, 2 };
        Color[] dotColors = { Coral, Gold, Mint };
        foreach (int d in dots) { FillC(dotColors[d], 255 * appear); Circle(cx + 36 + d * 26, cy + 30, 14); }
        TextFont(mono, 22);
        FillC(Muted, 220 * appear);
        TextAlign(TextAlignH.Left, TextAlignV.Baseline);
        Text(file, cx + 120, cy + 38);

        int totalChars = code.Sum(l => l.Length + 1);
        int shown = (int)(totalChars * Smooth(0.2f, 3.2f, t));
        float mark = Smooth(3.3f, 3.6f, t);
        TextFont(mono, 28);
        float y = cy + 100;
        for (int i = 0; i < code.Length && shown > 0; i++)
        {
            string line = code[i][..Math.Min(code[i].Length, shown)];
            shown -= code[i].Length + 1;
            if (i == markLine && mark > 0)
            {
                FillC(Gold, 50 * mark);
                Rect(cx, y - 30, cw, lh);
                FillC(Gold, 255 * mark);
                Rect(cx, y - 30, 6, lh);
            }
            FillC(Muted, 140);
            TextAlign(TextAlignH.Right, TextAlignV.Baseline);
            Text((i + 1).ToString(), cx + 80, y);
            TextAlign(TextAlignH.Left, TextAlignV.Baseline);
            DrawCode(line, cx + 110, y);
            if (shown <= 0 && (int)(t * 4) % 2 == 0)       // the text cursor
            {
                FillC(Gold);
                Rect(cx + 110 + TextWidth(line) + 2, y - 26, 3, 34);
            }
            y += lh;
        }

        TextFont(bodyBold, 32);
        TextAlign(TextAlignH.Center, TextAlignV.Center);
        float pw = TextWidth(caption) + 70;
        FillC(OnInk, 245 * appear);
        RoundRect((Width - pw) / 2, cy + ch + 40, pw, 66, 33);
        FillC(Ink, 255 * appear);
        Text(caption, Width / 2f, cy + ch + 71);
        Mark();
    }

    // ------------------------------------------------------------ 10-18 s: particles
    sealed class Particle { public PVector Pos, Vel; public float Hue; }
    readonly List<Particle> swarm = new();
    float lastParticleT = -1;

    void Particles(float t)
    {
        if (lastParticleT < 0)
        {
            Background(18, 14, 12);
            Burst(Width / 2f, Height / 2f, 500);
        }
        // The ""mouse"": a looping path; a click (a burst) on every downbeat.
        float mx = Width / 2f + Cos(t * 1.3f) * 620, my = Height / 2f + Sin(t * 2.1f) * 300;
        foreach (float beat in new[] { 2f, 4f, 6f })
            if (lastParticleT < beat && t >= beat)
                Burst(mx, my, 220);
        lastParticleT = t;

        NoStroke();
        Fill(18, 14, 12, 64);
        Rect(0, 0, Width, Height);
        foreach (var p in swarm)
        {
            var pull = new PVector(mx - p.Pos.X, my - p.Pos.Y);
            p.Vel.Add(pull.SetMag(0.55f));
            p.Pos.Add(p.Vel.Limit(13));
            FillHSB(p.Hue, 70, 95);
            Circle(p.Pos.X, p.Pos.Y, 10);
        }
        if (swarm.Count > 1400)
            swarm.RemoveRange(0, swarm.Count - 1400);

        Stroke(18, 14, 12);                          // an arrow cursor
        StrokeWeight(3);
        Fill(255);
        BeginShape();
        Vertex(mx, my); Vertex(mx, my + 42); Vertex(mx + 11, my + 31); Vertex(mx + 19, my + 49);
        Vertex(mx + 27, my + 45); Vertex(mx + 19, my + 28); Vertex(mx + 33, my + 28);
        EndShape(true);
        NoStroke();

        Mark();
        Caption(""Sketch like Processing, in C#"", ""Setup(), Draw(), Fill(), Circle()... plus hot reload and NuGet."", t, 0.5f, 7.7f);
        FadeTo(Ink, t, 7.7f, 8f);
    }

    void Burst(float x, float y, int n)
    {
        for (int i = 0; i < n; i++)
            swarm.Add(new Particle { Pos = new PVector(x, y), Vel = PVector.FromAngle(Random(TWO_PI)).Mult(Random(3, 16)), Hue = Random(10, 60) });
    }

    // ------------------------------------------------------------ 22-30 s: the beat
    static readonly string[] ChordNames = { ""Em7"", ""Cmaj7"", ""Gmaj7"", ""D"" };
    static Color ChordColor(int bar) => (bar % 4) switch { 0 => Sky, 1 => Coral, 2 => Gold, _ => Mint };

    void BeatScene(float t)
    {
        int bar = (int)(T / 2);                      // the soundtrack's own bar number (one bar = 2 s)
        var c = ChordColor(bar);
        float kick = Pulse(1, sharpness: 5);
        float snare = (int)Beat() % 2 == 1 ? Pulse(1, sharpness: 7) : 0;
        float hat = Pulse(0.5f, sharpness: 9);
        Background(Ink.R, Ink.G, Ink.B);
        float cx = Width / 2f, cy = 560;
        NoStroke();
        for (int i = 6; i >= 1; i--)
        {
            FillC(c, 10 + kick * 14);
            Circle(cx, cy, 300 + i * 55 + kick * 70);
        }
        StrokeWeight(10);
        for (int i = 0; i < 48; i++)
        {
            float a = TWO_PI * i / 48 - HALF_PI + (float)Beat() * 0.06f;
            float drive = Max(i < 12 || i > 36 ? kick : 0, i % 2 == 0 ? hat : snare);
            float len = 20 + 110 * Noise(i * 0.2f, t * 0.8f) * (0.4f + 0.6f * drive);
            float r0 = 185 + kick * 22, k = i / 48f;
            Stroke(Lerp(c.R, OnInk.R, k), Lerp(c.G, OnInk.G, k), Lerp(c.B, OnInk.B, k), 230);
            Line(cx + Cos(a) * r0, cy + Sin(a) * r0, cx + Cos(a) * (r0 + len), cy + Sin(a) * (r0 + len));
        }
        NoFill();
        Stroke(OnInk.R, OnInk.G, OnInk.B);
        StrokeWeight(8 + kick * 10);
        Circle(cx, cy, 260 + kick * 90);
        if (snare > 0.05f)
        {
            Stroke(255, 255, 255, snare * 160);
            StrokeWeight(3);
            Circle(cx, cy, 310 + kick * 90 + (1 - snare) * 100);
        }
        NoStroke();
        FillC(OnInk);
        TextFont(display, 60);
        TextAlign(TextAlignH.Center, TextAlignV.Center);
        Text(ChordNames[bar % 4], cx, cy - 6);

        int step = (int)(Beat() * 4) % 16;           // a 16-step row: Beat() in sixteenths
        float rx = cx - 16 * 46 / 2f;
        for (int i = 0; i < 16; i++)
        {
            bool on = i == step;
            if (on) FillC(c); else FillC(OnInk, i % 4 == 0 ? 70 : 32);
            RoundRect(rx + i * 46, 170 - (on ? 10 : 0), 36, on ? 46 : 26, 8);
        }
        FillC(Muted);
        TextFont(mono, 26);
        TextAlign(TextAlignH.Center, TextAlignV.Baseline);
        Text($""bar {bar + 1}   beat {(int)Beat() % 4 + 1}   120 BPM"", cx, 136);
        Mark();
        Caption(""Its own sound engine"", ""[LiveLoop]s, synths and drums. Beat() and Pulse() keep the visuals in time."", t, 0.4f, 7.7f);
        FadeTo(Ink, t, 7.7f, 8f);
    }

    // ------------------------------------------------------------ 30-42 s: on stage
    const string RingsSource = @""
uniform float2 iResolution;
uniform float uPulse;
uniform float uRings;
uniform float uPhase;
half4 main(float2 fragCoord) {
    float2 uv = (fragCoord - float2(0.62, 0.5) * iResolution) / iResolution.y;
    float d = length(uv);
    float a = atan(uv.y, uv.x);
    float v = sin(uRings * d * 6.0 - uPhase * 2.5 + uPulse * 2.0) * 0.6 + sin(a * 5.0 + uPhase * 0.8 + d * 4.0) * 0.4;
    float3 ink = float3(0.075, 0.125, 0.165), gold = float3(0.906, 0.714, 0.420);
    float3 coral = float3(1.0, 0.541, 0.420), mint = float3(0.561, 0.827, 0.714);
    float3 col = mix(ink, mint, smoothstep(-0.2, 0.9, v) * 0.45);
    col = mix(col, coral, smoothstep(0.3, 1.0, v) * 0.7);
    col = mix(col, gold, smoothstep(0.8, 1.0, v));
    col += uPulse * 0.12 * exp(-d * 3.0);
    col *= 1.0 - 0.5 * smoothstep(0.3, 1.2, d);
    return half4(half3(col), 1.0);
}"";
    PShader2D rings = null!;
    float phase, lastStageT;

    /// <summary>Backspace the old value, then type the new one, from <paramref name=""at""/>.</summary>
    static string Typed(string from, string to, float t, float at)
    {
        if (t < at) return from;
        float k = (t - at) / 0.14f;
        if (k < from.Length) return from[..(from.Length - (int)k - 1)];
        return to[..Math.Min(to.Length, (int)(k - from.Length) + 1)];
    }

    void Stage(float t)
    {
        float ringCount = t >= 6 ? 14 : 6, speed = t >= 10 ? 3 : 1;   // the two edits, hot-reloaded on the downbeat
        phase += speed * Math.Max(0, t - lastStageT) * 2;
        lastStageT = t;
        rings.Set(""uPulse"", Pulse(1, sharpness: 4));
        rings.Set(""uRings"", ringCount);
        rings.Set(""uPhase"", phase);
        Shade(rings);

        string[] lines =
        {
            ""public override void Draw()"",
            ""{"",
            $""    float rings = {Typed(""6"", ""14"", t, 5.0f)};"",
            $""    float speed = {Typed(""1"", ""3"", t, 8.9f)};"",
            """",
            ""    shader.Set(\""rings\"", rings);"",
            ""    shader.Set(\""pulse\"", Pulse(1));"",
            ""    phase += speed * DeltaTime;"",
            ""    Shade(shader);"",
            ""}"",
        };
        float lh = 52, x0 = 120, y0 = 190;
        NoStroke();
        FillC(Ink, 150);
        RoundRect(x0 - 40, y0 - 70, 1060, lines.Length * lh + 90, 26);
        TextFont(mono, 38);
        TextAlign(TextAlignH.Left, TextAlignV.Baseline);
        for (int i = 0; i < lines.Length; i++)
            DrawCode(lines[i], x0, y0 + i * lh);
        int editLine = t < 7.5f ? 2 : 3;
        if (((t > 4.6f && t < 6) || (t > 8.5f && t < 10)) && (int)(t * 4) % 2 == 0)
        {
            FillC(Gold);
            Rect(x0 + TextWidth(lines[editLine].TrimEnd(';')) + 2, y0 + editLine * lh - 38, 4, 46);
        }
        foreach (float at in new[] { 6f, 10f })     // the Hot Reload flash and chip
        {
            float k = 1 - Smooth(at, at + 1.4f, t);
            if (t < at || k <= 0) continue;
            Fill(255, 255, 255, 70 * (1 - Smooth(at, at + 0.25f, t)));
            Rect(0, 0, Width, Height);
            float by = y0 + lines.Length * lh + 40;
            FillC(Gold, 255 * k);
            RoundRect(x0 - 40, by, 300, 64, 32);
            FillC(Ink, 255 * k);
            BeginShape();                            // a lightning bolt
            Vertex(x0 - 2, by + 12); Vertex(x0 - 16, by + 35); Vertex(x0 - 6, by + 35);
            Vertex(x0 - 12, by + 54); Vertex(x0 + 8, by + 28); Vertex(x0 - 2, by + 28); Vertex(x0 + 4, by + 12);
            EndShape(true);
            TextFont(bodyBold, 30);
            Text(""Hot Reload"", x0 + 22, by + 43);
            TextFont(mono, 38);
        }
        Mark();
        Caption(""Built for the stage"", ""Performance mode: code over the visuals, edits that land on the beat."", t, 0.4f, 11.7f);
        FadeTo(Ink, t, 11.7f, 12f);
    }

    // ------------------------------------------------------------ 42-52 s: five things, 2 s each
    void Montage(float t)
    {
        int clip = Math.Min(4, (int)(t / 2));
        float u = t - clip * 2;
        switch (clip)
        {
            case 0: Tree(u); break;
            case 1: Landscape(u); break;
            case 2: Plotter(u); break;
            case 3: Life(u); break;
            default: Harmonograph(u); break;
        }
        Mark(onLight: clip is 0 or 2);
        string[,] text =
        {
            { ""Generative art"", ""Recursion, noise and randomness in a few lines."" },
            { ""Seeds and variations"", ""Every seed is a new landscape. Lock the one you like."" },
            { ""Print and plotter ready"", ""Big PNG, SVG, and plotter SVG with one pen per color."" },
            { ""Real rewind"", ""Pause, scrub back, and continue from any frame."" },
            { ""MIDI, microphone, sliders"", ""Map a knob to any [Tweak] slider with MIDI learn."" },
        };
        Caption(text[clip, 0], text[clip, 1], u, 0.1f, clip == 4 ? 1.8f : 1.95f);
        if (clip > 0 && u < 0.18f) { Fill(255, 255, 255, 90 * (1 - Smooth(0, 0.18f, u))); Rect(0, 0, Width, Height); }
        FadeTo(Ink, t, 9.7f, 10f);
    }

    void Tree(float t)
    {
        Background(0xF1, 0xE5, 0xA1);
        float angle = Lerp(12, 62, Smooth(0, 2, t)) + Pulse(1, sharpness: 5) * 4;
        PushMatrix();
        Translate(Width / 2f, Height);
        StrokeWeight(14);
        Stroke(0x8B, 0x26, 0x26);
        Line(0, 0, 0, -330);
        Translate(0, -330);
        Branch(330, angle, 0);
        PopMatrix();
    }

    void Branch(float len, float angle, int depth)
    {
        if (depth > 9) return;
        len *= 0.68f;
        float k = depth / 9f;
        Stroke(Lerp(0xEF, 0x48, k), Lerp(0x69, 0x6C, k), Lerp(0x05, 0x2F, k));
        StrokeWeight(Math.Max(1.5f, 10 - depth));
        foreach (float a in new[] { angle, -angle })
        {
            PushMatrix();
            Rotate(Radians(a));
            Line(0, 0, 0, -len);
            Translate(0, -len);
            Branch(len, angle, depth + 1);
            PopMatrix();
        }
    }

    void Landscape(float t)
    {
        int seed = 9168 + (int)(t * 2) * 731;        // a new seed on every beat
        RandomSeed(seed);
        NoiseSeed(seed);
        float hue = Random(180, 290);
        NoStroke();
        for (int y = 0; y < Height; y += 6)
        {
            float k = y / (float)Height;
            FillHSB(hue, 50 - k * 20, 18 + k * 30);
            Rect(0, y, Width, 6);
        }
        for (int i = 0; i < 220; i++)
        {
            Fill(255, Random(80, 230));
            Circle(Random(Width), Random(Height * 0.6f), Random(1.5f, 4));
        }
        FillHSB((hue + 180) % 360, 40, 100, 230);
        Circle(Random(300, Width - 300), Random(140, 320), Random(120, 220));
        for (int layer = 0; layer < 5; layer++)
        {
            float depth = layer / 4f, baseY = Height * (0.45f + depth * 0.4f), amp = 110 + depth * 160, offset = Random(1000);
            FillHSB(hue, 35 + depth * 30, 70 - depth * 55);
            BeginShape();
            Vertex(0, Height);
            for (int x = 0; x <= Width; x += 8)
                Vertex(x, baseY - Noise(offset + x * 0.0055f, layer * 10) * amp * 2 + amp);
            Vertex(Width, Height);
            EndShape(true);
        }
        Fill(255, 220);
        TextFont(mono, 28);
        TextAlign(TextAlignH.Right, TextAlignV.Baseline);
        Text($""seed {seed}"", Width - 72, 92);
    }

    void Plotter(float t)
    {
        RandomSeed(42);
        NoiseSeed(42);
        Background(250, 248, 240);
        NoFill();
        StrokeWeight(2);
        int shown = (int)(900 * Smooth(0, 1.6f, t));
        for (int i = 0; i < shown; i++)
        {
            float x = Random(-80, Width + 80), y = Random(-80, Height + 80);
            if (Random(1) < 0.25f) Stroke(200, 40, 40); else Stroke(20, 20, 30);
            BeginShape();
            for (int s = 0; s < 80; s++)
            {
                Vertex(x, y);
                float a = Noise(x / Width * 2.2f, y / Height * 2.2f) * TWO_PI * 2;
                x += Cos(a) * 7;
                y += Sin(a) * 7;
            }
            EndShape();
        }
    }

    readonly List<bool[,]> lifeHistory = new();
    const int Cell = 24;

    void PrepareLife()
    {
        int cols = 1920 / Cell, rows = 1080 / Cell;
        var b = new bool[cols, rows];
        var rng = new System.Random(7);
        for (int x = 0; x < cols; x++)
            for (int y = 0; y < rows; y++)
                b[x, y] = rng.NextDouble() < 0.3;
        lifeHistory.Add(b);
        for (int g = 0; g < 40; g++)
        {
            var next = new bool[cols, rows];
            for (int x = 0; x < cols; x++)
                for (int y = 0; y < rows; y++)
                {
                    int n = 0;
                    for (int dx = -1; dx <= 1; dx++)
                        for (int dy = -1; dy <= 1; dy++)
                            if ((dx != 0 || dy != 0) && b[(x + dx + cols) % cols, (y + dy + rows) % rows]) n++;
                    next[x, y] = b[x, y] ? n is 2 or 3 : n == 3;
                }
            lifeHistory.Add(b = next);
        }
    }

    void Life(float t)
    {
        // 0-1.2 s forward through 40 generations, then scrub back to generation 12.
        int gen = t < 1.2f ? (int)(t / 1.2f * 40) : (int)Lerp(40, 12, Smooth(1.2f, 1.9f, t));
        bool back = t >= 1.2f;
        var b = lifeHistory[Math.Clamp(gen, 0, lifeHistory.Count - 1)];
        Background(Ink.R, Ink.G, Ink.B);
        NoStroke();
        FillC(back ? Sky : Mint);
        for (int x = 0; x < b.GetLength(0); x++)
            for (int y = 0; y < b.GetLength(1); y++)
                if (b[x, y]) RoundRect(x * Cell + 2, y * Cell + 2, Cell - 4, Cell - 4, 5);

        float bx = 620, bw = 1180, byy = 84;           // the rewind bar, like the IDE's
        FillC(Ink2, 245);
        Stroke(Muted.R, Muted.G, Muted.B, 90);
        StrokeWeight(2);
        RoundRect(bx - 30, byy - 38, bw + 60, 76, 38);
        Stroke(Muted.R, Muted.G, Muted.B, 120);
        StrokeWeight(6);
        Line(bx + 60, byy, bx + bw - 200, byy);
        NoStroke();
        FillC(Sky);
        Circle(Lerp(bx + 60, bx + bw - 200, gen / 40f), byy, 30);
        FillC(OnInk);
        if (back) { Triangle(bx + 20, byy - 14, bx + 20, byy + 14, bx + 2, byy); Triangle(bx + 40, byy - 14, bx + 40, byy + 14, bx + 22, byy); }
        else Triangle(bx + 8, byy - 15, bx + 8, byy + 15, bx + 34, byy);
        TextFont(mono, 26);
        TextAlign(TextAlignH.Right, TextAlignV.Center);
        Text(back ? $""frame {gen + 1} · {40 - gen} back"" : $""frame {gen + 1}"", bx + bw + 10, byy);
    }

    void Harmonograph(float t)
    {
        Background(14, 14, 20);
        float fx = Lerp(2, 3, Smooth(0.1f, 1.2f, t)), fy = 2, ph = Lerp(0.4f, 2.2f, Smooth(0, 2, t));
        NoFill();
        Stroke(255, 170, 90);
        StrokeWeight(2.2f + Pulse(1) * 1.5f);
        BeginShape();
        for (int i = 0; i < 3000; i++)
        {
            float tt = i * 0.02f, decay = Exp(-0.0025f * i);
            Vertex(760 + Sin(fx * tt + ph) * decay * 400, 520 + Sin(fy * tt) * decay * 400);
        }
        EndShape();
        string[] names = { ""freqX"", ""freqY"", ""phase"" };
        float[] values = { (fx - 1) / 5, (fy - 1) / 5, ph / TWO_PI };
        NoStroke();
        FillC(Ink2);
        RoundRect(1360, 250, 420, 540, 30);
        FillC(Coral);
        TextFont(bodyBold, 28);
        TextAlign(TextAlignH.Left, TextAlignV.Center);
        Text(""MIDI"", 1400, 300);
        for (int i = 0; i < 3; i++)
        {
            float ky = 400 + i * 135;
            FillC(OnInk);
            TextFont(body, 30);
            Text(names[i], 1400, ky);
            FillC(Ink);
            Circle(1690, ky, 92);
            float a = Lerp(-PI * 0.75f, PI * 0.75f, values[i]) - HALF_PI;
            Stroke(Gold.R, Gold.G, Gold.B);
            StrokeWeight(7);
            Line(1690, ky, 1690 + Cos(a) * 36, ky + Sin(a) * 36);
            NoStroke();
        }
    }

    // ------------------------------------------------------------ 52-60 s: the outro
    void Outro(float t)
    {
        Background(Ink.R, Ink.G, Ink.B);
        NoStroke();
        RandomSeed(3);
        for (int i = 0; i < 140; i++)                // slow drifting dots
        {
            float x = Random(Width), y0 = Random(Height), r = Random(2, 7), sp = Random(8, 30);
            var c = (int)Random(4) switch { 0 => Gold, 1 => Coral, 2 => Mint, _ => Sky };
            FillC(c, 70);
            Circle(x + Sin(t * 0.4f + y0) * 20, (y0 - t * sp + Height) % Height, r);
        }
        float cx = Width / 2f, a1 = Smooth(0, 0.8f, t), sc = 1 + (1 - a1) * 0.2f;
        NoFill();
        Stroke(OnInk.R, OnInk.G, OnInk.B, 255 * a1);
        StrokeWeight(8);
        Circle(cx, 300, 190 * sc);
        NoStroke();
        FillC(Gold, 255 * a1);
        Circle(cx, 300, 86 * sc);
        float orbit = (float)Phase(4) * TWO_PI - HALF_PI;   // the coral moon goes round once a bar
        FillC(Coral, 255 * a1);
        Circle(cx + Cos(orbit) * 70 * sc, 300 + Sin(orbit) * 70 * sc, 30 * sc);

        TextAlign(TextAlignH.Center, TextAlignV.Baseline);
        float a2 = Smooth(0.5f, 1.2f, t);
        FillC(OnInk, 255 * a2);
        TextFont(heavy, 120);
        Text(""DanaProcessing"", cx, 540 + (1 - a2) * 30);
        float a3 = Smooth(1, 1.7f, t);
        FillC(OnInk, 235 * a3);
        TextFont(body, 46);
        Text(""Free and open-source creative coding in C#"", cx, 620 + (1 - a3) * 20);
        FillC(Muted, 255 * Smooth(1.6f, 2.3f, t));
        TextFont(body, 34);
        Text(""Windows  ·  macOS  ·  Linux  ·  MIT license"", cx, 690);
        float a5 = Smooth(2.2f, 2.9f, t);
        TextFont(mono, 44);
        const string url = ""buddhacodes.github.io/DanaProcessing"";
        float uw = TextWidth(url) + 80;
        FillC(Gold, 255 * a5);
        RoundRect(cx - uw / 2, 760, uw, 84, 42);
        FillC(Ink, 255 * a5);
        Text(url, cx, 817);
        FillC(Muted, 220 * Smooth(3.6f, 4.4f, t));
        TextFont(body, 30);
        Text(""Everything in this video, code, visuals and music, was made with DanaProcessing."", cx, 960);
        FadeTo(new Color(0, 0, 0), t, 7.2f, 7.8f);
    }

    // ------------------------------------------------------------ the soundtrack
    // bars 0-2: pad + arpeggio (kick from bar 2) · 3-25: full · 21-25: 16th hats
    // · 26-29: drums out, one last chord.
    static readonly string[][] Chords =
    {
        new[] { ""E3"", ""G3"", ""B3"", ""D4"" }, new[] { ""C3"", ""E3"", ""G3"", ""B3"" },
        new[] { ""G3"", ""B3"", ""D4"", ""F#4"" }, new[] { ""D3"", ""F#3"", ""A3"", ""C#4"" },
    };
    static readonly string[] Roots = { ""E2"", ""C2"", ""G2"", ""D2"" };
    int padBar, arpBar, drumBar, hatBar, bassBar;
    static bool Full(int bar) => bar >= 3 && bar < 26;

    [LiveLoop]
    async Task Pad()
    {
        int bar = padBar++;
        if (bar < 29)
            foreach (var n in Chords[bar % 4])
                Play(n, amp: Full(bar) ? 0.07 : 0.10, attack: 0.5, sustain: 1.0, release: 1.2, cutoff: Full(bar) ? 92 : 84, synth: Synth.Saw);
        else if (bar == 29)
        {
            foreach (var n in Chords[0])
                Play(n, amp: 0.08, attack: 0.2, sustain: 0.8, release: 3.0, cutoff: 84, synth: Synth.Saw);
            Play(""E5"", amp: 0.12, release: 3, synth: Synth.Triangle);
        }
        await Sleep(4);
    }

    [LiveLoop]
    async Task Arp()
    {
        int bar = arpBar++;
        for (int i = 0; i < 8; i++)
        {
            if (bar < 29)
                Play(Notes.Parse(Chords[bar % 4][i % 4]) + (i >= 4 ? 24 : 12), amp: Full(bar) ? 0.11 : 0.19,
                     pan: i % 2 == 0 ? -0.35 : 0.35, release: 0.35, synth: Synth.Pluck);
            await Sleep(0.5);
        }
    }

    [LiveLoop]
    async Task Drums()
    {
        int bar = drumBar++;
        for (int beat = 0; beat < 4; beat++)
        {
            if (bar >= 2 && bar < 26) Sample(""kick"", amp: bar == 2 ? 0.7 : 0.9);
            if (Full(bar) && beat % 2 == 1) Sample(""snare"", amp: 0.5);
            await Sleep(1);
        }
    }

    [LiveLoop]
    async Task Hats()
    {
        int bar = hatBar++;
        int steps = bar >= 21 && bar < 26 ? 16 : 8;
        for (int i = 0; i < steps; i++)
        {
            if (Full(bar)) Sample(""hat"", amp: (i % 2 == 1 ? 0.28 : 0.16) * (steps == 16 ? 0.8 : 1), pan: 0.2);
            await Sleep(4.0 / steps);
        }
    }

    [LiveLoop]
    async Task Bass()
    {
        int bar = bassBar++;
        double root = Notes.Parse(Roots[bar % 4]);
        double[] pattern = { 0, 0, 12, 0, 0, 12, 0, 7 };
        for (int i = 0; i < 8; i++)
        {
            if (Full(bar)) Play(root + pattern[i], amp: 0.28, release: 0.28, cutoff: 72, res: 0.25, synth: Synth.Saw);
            await Sleep(0.5);
        }
    }
}
// @reel show
";
    }
}
