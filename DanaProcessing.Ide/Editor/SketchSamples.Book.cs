using DanaProcessing.Ide.Localization;

namespace DanaProcessing.Ide.Editor
{
    /// <summary>A 3D book whose pages are live simulations and whose letters are 3D objects.</summary>
    public static partial class SketchSamples
    {
        private static readonly SketchSample[] Books =
        {
            new SketchSample(
                Loc.Tr("Un libro de los elementos (3D)", "A Book of Elements (3D)"),
                Loc.Tr("Un libro en 3D de verdad (en la GPU) donde cada página está viva y las palabras juegan. Ocho poemas de dominio público, cada uno con su elemento: tocá una palabra del Tigre de Blake y se prende fuego, vuela como brasas y vuelve; las palabras de Coleridge flotan en agua que hace ondas bajo tu dedo; la hierba de Whitman crece a través de la página y levanta las letras en sus tallos; el espejo de Carroll es una bola de cristal que rueda sobre el texto, lo curva y lo da vuelta (hacé clic para romperla); la nieve de Longfellow cae por la habitación y se posa sobre las letras hasta que se la sacuden; el viento del oeste de Shelley se lleva las palabras como hojas de otoño; el hielo de Frost hace crecer cristales desde las letras (tu mouse es tibio); y las palabras de Teasdale suben a la noche y forman constelaciones. Cada letra es su propio objeto 3D, recortado de un atlas de glifos y apoyado en la página aunque la página se esté curvando al girar. Usa lo nuevo del renderer 3D: color por vértice, transparencias, BlendMode(Add) para la luz, Hint(\"DISABLE_DEPTH_MASK\") para partículas y texto 2D encima. Arrastrá el borde de una página para girarla (o Izquierda/Derecha), tocá las páginas, hacé clic en el Índice para saltar, arrastrá la mesa para mirar alrededor y usá la rueda para acercarte.",
                       "A real 3D book (on the GPU) where every page is alive and the words play. Eight public-domain poems, each living with its element: touch a word of Blake's Tyger and it catches fire, flies up as embers and comes back; Coleridge's words float on water that ripples under your finger; Whitman's grass grows up through the page and lifts the letters on its blades; Carroll's looking-glass is a glass ball rolling over the text, bending it and turning it backwards (click it to shatter it); Longfellow's snow falls through the room and settles on the letters until they shake it off; Shelley's west wind blows the words away as autumn leaves; Frost's ice grows crystals out of the letters (your mouse is warm); and Teasdale's words rise into the night as constellations. Every letter is its own 3D object, cut from a glyph atlas and laid on the page even while the page curls as it turns. It uses what's new in the 3D renderer: per-vertex color, transparency, BlendMode(Add) for light, Hint(\"DISABLE_DEPTH_MASK\") for particles, and 2D text on top. Drag a page's edge to turn it (or Left/Right), touch the pages, click the Contents to jump, drag the table to look around and use the wheel to zoom."),
                BookOfElements,
                new[] { SampleTag.ThreeD, SampleTag.Gpu, SampleTag.Text, SampleTag.Interaction, SampleTag.Particles }),
        };

        private const string BookOfElements =
@"// A Book of Elements: a 3D book where every page is alive, and the words themselves play.
//
// Eight poems in the public domain, each living with its element, in real 3D on the GPU:
// Blake's Tyger burns -- touch a word and it catches fire, flies up as embers and comes back;
// Coleridge's words float on water that ripples under your finger; Whitman's grass grows up
// through the page and lifts the letters on its blades; Carroll's looking-glass is a glass
// ball rolling over the text, bending it and turning it backwards (click it to shatter it);
// Longfellow's snow falls through the room and settles on the letters until they shake it
// off; Shelley's west wind blows the words away as autumn leaves; Frost's ice grows crystals
// out of the letters (your mouse is warm); and Teasdale's words rise into the night as stars.
//
// Every letter is its own little 3D object: a glyph cut from an atlas texture, laid on the
// page wherever the page is -- even on a sheet curling as it turns -- and free to leave it.
// The pages, the table and the covers are lit; flames, sparks and stars add light.
//
// Turn pages: drag a page's outer edge across, or Left/Right (Space turns forward). Touch
// the pages with the mouse; click on the Contents to jump. Drag the table to look around,
// wheel to zoom. Left alone for a while, the book leafs through itself.
using System.Numerics;

public class BookOfElements : Sketch
{
    // ---- the book --------------------------------------------------------------------------
    // World: x to the right, y up, z toward you. Dana's 3D is y-down, so everything goes
    // through V()/VT()/N() below. A page's own space (where words and effects live) is
    // x across (0..TW), y down the page (0..TH) and z up out of the paper, in ""page units"".
    const float PW = 200, PH = 266;          // a page, in world units
    const float TW = 420, TH = 560;          // a page, in page units
    const float K = PW / TW;                 // world units per page unit
    const int NU = 26, NV = 10;              // the turning sheet's mesh
    const float SheetT = 0.7f;               // a sheet's thickness
    const float Board = 3.2f;                // the covers' boards

    Page[] pages;
    int Sheets => pages.Length / 2;
    int turned;                               // sheets lying on the left
    int flipping = -1;                        // the sheet being turned, or -1
    float theta, thetaTarget, thetaFrom, flipStart, flipTime = 1.15f;
    bool dragging, autoFlip;
    int dragDir;
    readonly Queue<int> pendingFlips = new();
    float lastInput;

    // ---- the camera ---------------------------------------------------------------------------
    float yaw, pitch = 0.9f, dist = 495, zoom = 1, camX = PW / 2;
    Vector3 eye, target, camFwd, camRight, camUp;
    const float Fov = 0.62f;
    float night;                              // the stars page darkens the room
    Vector3 fireLight; float fireLightPower;  // the Tyger's flames light the room

    // ---- shared pictures ----------------------------------------------------------------------
    PImage atlas, glowTex, leafTex, woodTex, shadowTex, frostTex;
    PFont fRegular, fItalic, fBold, ui;
    readonly Glyph[] glyphs = new Glyph[600];
    readonly Dictionary<(int, char), int> glyphOf = new();
    const int Cell = 80, CellH = 92, AtlasSize = 2048, FontPx = 60, Base = 68, Pad = 10;

    struct Glyph { public float U0, V0, U1, V1, Advance; }

    public override void Setup()
    {
        Size(1280, 800, RendererKind.Renderer3D);
        fRegular = CreateFont(""Georgia"", FontPx);
        fItalic = CreateFont(""Georgia Italic"", FontPx);
        fBold = CreateFont(""Georgia Bold"", FontPx);
        ui = CreateFont(""Segoe UI"", 13);
        BuildAtlas();
        BuildPictures();
        pages = new Page[]
        {
            new CoverPage(this), new ContentsPage(this),
            new FirePage(this), new WaterPage(this),
            new GrassPage(this), new GlassPage(this),
            new SnowPage(this), new WindPage(this),
            new IcePage(this), new StarsPage(this),
            new ColophonPage(this), new BackCoverPage(this),
        };
        for (int i = 0; i < pages.Length; i++) { pages[i].Number = i; pages[i].Build(); }
    }

    float Now => Millis() / 1000f;

    // =====================================================================================
    // Each frame
    // =====================================================================================
    public override void Draw()
    {
        float t = Now, dt = 1 / 60f;
        Turning(t);
        if (flipping < 0 && pendingFlips.Count > 0) Flip(pendingFlips.Dequeue(), fast: true);
        if (t - lastInput > 25 && flipping < 0) { lastInput = t - 16; Flip(turned < Sheets ? +1 : -1); if (turned == Sheets) { for (int i = 1; i < Sheets; i++) pendingFlips.Enqueue(-1); } }

        // Which pages show, and where.
        float lh = LeftHeight(), rh = RightHeight();
        int left = 2 * turned - 1, right = 2 * turned;
        if (flipping >= 0) { left = 2 * Math.Min(turned, flipping) - 1; right = 2 * Math.Max(turned, flipping + 1); }
        foreach (var p in pages) p.Surface = null;
        if (left >= 0) pages[left].Surface = Surface.Flat(-PW, Board + lh + 0.25f);
        if (right < pages.Length) pages[right].Surface = Surface.Flat(0, Board + rh + 0.25f);
        Vector3[] sheet = null;
        if (flipping >= 0)
        {
            sheet = SheetMesh(flipping, Board + Lerp(rh, lh, theta / PI));
            pages[2 * flipping].Surface = Surface.Curled(sheet, back: false);
            pages[2 * flipping + 1].Surface = Surface.Curled(sheet, back: true);
        }

        Camera3D(t);
        Pointer();

        // The pages live: the open ones fully, the turning ones settle down.
        bool starsOpen = false;
        foreach (var p in pages)
        {
            if (p.Surface == null) { if (p.WasOpen) { p.WasOpen = false; p.Close(); } continue; }
            bool open = p.Surface.IsFlat;
            if (open && !p.WasOpen) { p.WasOpen = true; p.Open(t); }
            p.Live = Lerp(p.Live, open ? 1 : 0, open ? 0.06f : 0.2f);
            p.Step(dt, t, open);
            if (p is StarsPage && open) starsOpen = true;
        }
        night = Lerp(night, starsOpen ? 1 : 0, 0.03f);

        // ---- draw ----
        float room = 1 - 0.78f * night;
        Background(30 * room + 4 * night, 22 * room + 6 * night, 20 * room + 14 * night);
        Perspective(Fov, Width / (float)Height, 5, 6000);
        Camera(eye.X, -eye.Y, eye.Z, target.X, -target.Y, target.Z, 0, 1, 0);
        LightFalloff(1, 0, 0);
        AmbientLight(62 * room + 18 * night, 58 * room + 22 * night, 55 * room + 34 * night);
        DirectionalLight(205 * room, 192 * room, 172 * room, 0.35f, 1, -0.45f);
        DirectionalLight(40 * room, 48 * room, 62 * room, -0.6f, 0.4f, 0.5f);
        if (fireLightPower > 0.01f)
        {
            LightFalloff(1, 0.004f, 0.00004f);
            float f = fireLightPower;
            PointLight(255 * f, 140 * f, 50 * f, fireLight.X, -fireLight.Y, fireLight.Z);
            LightFalloff(1, 0, 0);
        }
        fireLightPower = 0;
        Specular(0); Shininess(0);
        NoStroke();

        Table();
        Covers(lh, rh);
        foreach (var p in pages) if (p.Surface != null && p.Surface.IsFlat) FlatPage(p);
        if (sheet != null) TurningSheet(flipping, sheet);
        foreach (var p in pages) if (p.Surface != null && p.Live > 0.02f) p.DrawLit();
        Letters(material: 0);
        Specular(255, 220, 150); Shininess(28);
        Letters(material: 1);
        Specular(0); Shininess(0);

        // See-through things: water, glass, frost, the turning page's shadow.
        foreach (var p in pages) if (p.Surface != null && p.Live > 0.02f) p.DrawTranslucent();
        if (sheet != null) SheetShadow(sheet, lh, rh);

        // Light-adding things, last: no lights, added on top, never hiding each other.
        NoLights();
        Hint(""DISABLE_DEPTH_MASK"");
        foreach (var p in pages) if (p.Surface != null && p.Live > 0.02f) p.DrawSoft();
        DrawSprites(soft);
        DrawStreaks(softStreaks);
        BlendMode(BlendModeKind.Add);
        foreach (var p in pages) if (p.Surface != null && p.Live > 0.02f) p.DrawGlow();
        GlowLetters();
        DrawSprites(glow);
        DrawStreaks(streaks);
        DrawLines();
        BlendMode(BlendModeKind.Blend);
        Hint(""ENABLE_DEPTH_MASK"");
        soft.Clear(); glow.Clear(); lines.Clear(); streaks.Clear(); softStreaks.Clear();

        Hud();
    }

    // =====================================================================================
    // World <-> Dana
    // =====================================================================================
    void V(Vector3 p) => Vertex(p.X, -p.Y, p.Z);
    void VT(Vector3 p, float u, float v) => Vertex(p.X, -p.Y, p.Z, u, v);
    void N(Vector3 n) => Normal(n.X, -n.Y, n.Z);
    void FillV(Vector4 c) => Fill(c.X, c.Y, c.Z, c.W);
    void FillV(Vector4 c, float alpha) => Fill(c.X, c.Y, c.Z, c.W * alpha);

    static float Lerp(float a, float b, float t) => a + (b - a) * t;
    static float Clamp01(float x) => x < 0 ? 0 : x > 1 ? 1 : x;
    static float Smooth(float a, float b, float x) { float k = Clamp01((x - a) / (b - a)); return k * k * (3 - 2 * k); }
    static Vector4 Rgb(float r, float g, float b, float a = 255) => new(r, g, b, a);

    /// <summary>The rotation that takes direction a to direction b.</summary>
    static Quaternion FromTo(Vector3 a, Vector3 b)
    {
        a = Vector3.Normalize(a); b = Vector3.Normalize(b);
        float d = Vector3.Dot(a, b);
        if (d > 0.9999f) return Quaternion.Identity;
        if (d < -0.9999f) return Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI);
        var axis = Vector3.Normalize(Vector3.Cross(a, b));
        return Quaternion.CreateFromAxisAngle(axis, MathF.Acos(d));
    }

    // =====================================================================================
    // Where a page is: flat on a stack, or curled on the turning sheet
    // =====================================================================================
    struct Frame { public Vector3 P, U, V, N; }

    sealed class Surface
    {
        public bool IsFlat;
        float x0, y;
        Vector3[] grid; bool back;

        public static Surface Flat(float x0, float height) => new() { IsFlat = true, x0 = x0, y = height };
        public static Surface Curled(Vector3[] grid, bool back) => new() { grid = grid, back = back };

        /// <summary>The point at (u, v) in 0..1 across and down the page, with the page's own directions there.</summary>
        public Frame At(float u, float v)
        {
            if (IsFlat)
                return new Frame { P = new Vector3(x0 + u * PW, y, -PH / 2 + v * PH), U = Vector3.UnitX, V = Vector3.UnitZ, N = Vector3.UnitY };
            float su = back ? 1 - u : u;
            float fi = Math.Clamp(su, 0, 1) * NU, fj = Math.Clamp(v, 0, 1) * NV;
            int i = Math.Min(NU - 1, (int)fi), j = Math.Min(NV - 1, (int)fj);
            float a = fi - i, b = fj - j;
            Vector3 G(int ii, int jj) => grid[jj * (NU + 1) + ii];
            var top = Vector3.Lerp(G(i, j), G(i + 1, j), a);
            var bottom = Vector3.Lerp(G(i, j + 1), G(i + 1, j + 1), a);
            var p = Vector3.Lerp(top, bottom, b);
            var du = Vector3.Lerp(G(i + 1, j) - G(i, j), G(i + 1, j + 1) - G(i, j + 1), b);
            var dv = bottom - top;
            var U = Vector3.Normalize(du); var V = Vector3.Normalize(dv);
            var n = Vector3.Normalize(Vector3.Cross(V, U));
            if (back) { U = -U; n = -n; p += n * 0.25f; }
            return new Frame { P = p, U = U, V = V, N = n };
        }

        /// <summary>A point in page units (x across, y down, z up out of the paper) in the world.</summary>
        public Vector3 W(float x, float y, float z)
        {
            if (IsFlat) return new Vector3(x0 + x * K, this.y + z * K, -PH / 2 + y * K);
            var f = At(x / TW, y / TH);
            return f.P + f.N * (z * K);
        }

        /// <summary>A direction in page units, in the world (flat pages; on a curl, at the page's middle).</summary>
        public Vector3 Dir(Vector3 d)
        {
            if (IsFlat) return new Vector3(d.X, d.Z, d.Y);
            var f = At(0.5f, 0.5f);
            return f.U * d.X + f.V * d.Y + f.N * d.Z;
        }
    }

    // =====================================================================================
    // The book itself
    // =====================================================================================
    float LeftHeight() => (flipping >= 0 && flipping < turned ? turned - 1 : turned) * SheetT;
    float RightHeight() => (Sheets - (flipping >= 0 && flipping >= turned ? turned + 1 : turned)) * SheetT;

    void Table()
    {
        Fill(255);
        N(Vector3.UnitY);
        BeginShape(ShapeKind.Quads);
        Texture(woodTex);
        VT(new(-900, 0, -560), 0, 0); VT(new(900, 0, -560), 1, 0); VT(new(900, 0, 620), 1, 1); VT(new(-900, 0, 620), 0, 1);
        EndShape();
        // The book's soft shadow on the wood.
        Hint(""DISABLE_DEPTH_MASK"");
        float l = turned > 0 || flipping >= 0 ? -PW - 30 : -30, r = turned < Sheets || flipping >= 0 ? PW + 30 : 30;
        Fill(255, 255, 255, 235);
        BeginShape(ShapeKind.Quads);
        Texture(shadowTex);
        VT(new(l + 14, 0.3f, -PH / 2 - 22), 0, 0); VT(new(r + 14, 0.3f, -PH / 2 - 22), 1, 0); VT(new(r + 14, 0.3f, PH / 2 + 40), 1, 1); VT(new(l + 14, 0.3f, PH / 2 + 40), 0, 1);
        EndShape();
        Hint(""ENABLE_DEPTH_MASK"");
    }

    void Box3(Vector3 center, Vector3 size, Vector4 color)
    {
        PushMatrix();
        Translate(center.X, -center.Y, center.Z);
        FillV(color);
        Box(size.X, size.Y, size.Z);
        PopMatrix();
    }

    void Covers(float lh, float rh)
    {
        var leather = Rgb(112, 28, 34);
        var paper = Rgb(226, 214, 190);
        Specular(70); Shininess(18);
        if (turned > 0 || flipping >= 0) Box3(new(-PW / 2 - 3, Board / 2, 0), new(PW + 8, Board, PH + 12), leather);
        if (turned < Sheets || flipping >= 0) Box3(new(PW / 2 + 3, Board / 2, 0), new(PW + 8, Board, PH + 12), leather);
        Specular(0); Shininess(0);
        if (lh > 0.05f) Box3(new(-PW / 2, Board + lh / 2, 0), new(PW - 0.4f, lh, PH - 0.4f), paper);
        if (rh > 0.05f) Box3(new(PW / 2, Board + rh / 2, 0), new(PW - 0.4f, rh, PH - 0.4f), paper);
        // The spine, a rounded strip along the gutter.
        if (turned > 0 && turned < Sheets || flipping >= 0)
            Box3(new(0, Board / 2, 0), new(10, Board + 0.2f, PH + 12), Rgb(96, 22, 28));
    }

    void FlatPage(Page p)
    {
        var s = p.Surface;
        Fill(255);
        N(Vector3.UnitY);
        BeginShape(ShapeKind.Quads);
        Texture(p.Paper);
        VT(s.W(0, 0, 0), 0, 0); VT(s.W(TW, 0, 0), 1, 0); VT(s.W(TW, TH, 0), 1, 1); VT(s.W(0, TH, 0), 0, 1);
        EndShape();
    }

    /// <summary>
    /// The turning sheet: each column turns a little less than the one before it (more at the
    /// bottom corner, where a hand would hold it), so the page curls as it lifts.
    /// </summary>
    Vector3[] SheetMesh(int sheetIndex, float hingeY)
    {
        var pos = new Vector3[(NU + 1) * (NV + 1)];
        float seg = PW / NU, lift = MathF.Sin(theta);
        bool stiff = sheetIndex == 0 || sheetIndex == Sheets - 1;          // the covers don't curl
        for (int j = 0; j <= NV; j++)
        {
            float v = j / (float)NV, x = 0, y = hingeY;
            pos[j * (NU + 1)] = new Vector3(0, y, -PH / 2 + v * PH);
            for (int i = 1; i <= NU; i++)
            {
                float u = i / (float)NU;
                float lag = stiff ? 0 : lift * (0.45f + 0.55f * v) * MathF.Pow(u, 1.6f) * (dragging ? 0.85f : 0.6f);
                float a = theta - lag * TurnDirection;
                x += MathF.Cos(a) * seg; y += MathF.Sin(a) * seg;
                pos[j * (NU + 1) + i] = new Vector3(x, Math.Max(y, hingeY), -PH / 2 + v * PH);
            }
        }
        return pos;
    }

    void TurningSheet(int sheetIndex, Vector3[] pos)
    {
        var front = pages[2 * sheetIndex].Surface;
        var back = pages[2 * sheetIndex + 1].Surface;
        foreach (var (s, page) in new[] { (front, pages[2 * sheetIndex]), (back, pages[2 * sheetIndex + 1]) })
        {
            Fill(255);
            BeginShape(ShapeKind.Triangles);
            Texture(page.Paper);
            for (int j = 0; j < NV; j++)
                for (int i = 0; i < NU; i++)
                {
                    float u0 = i / (float)NU, u1 = (i + 1) / (float)NU, v0 = j / (float)NV, v1 = (j + 1) / (float)NV;
                    void P(float u, float v) { var f = s.At(u, v); N(f.N); VT(f.P, u, v); }
                    P(u0, v0); P(u1, v0); P(u1, v1);
                    P(u0, v0); P(u1, v1); P(u0, v1);
                }
            EndShape();
        }
    }

    /// <summary>The turning sheet's shadow, cast along the key light onto the page beneath it.</summary>
    void SheetShadow(Vector3[] pos, float lh, float rh)
    {
        bool onRight = theta < HALF_PI;
        float h = Board + (onRight ? rh : lh) + 0.35f;
        float x0 = onRight ? 0 : -PW, x1 = onRight ? PW : 0;
        var light = Vector3.Normalize(new Vector3(0.35f, -1, -0.45f));
        float strength = MathF.Sin(theta) * 0.55f;
        Vector3 Cast(Vector3 p)
        {
            float k = (p.Y - h) / -light.Y;
            var q = p + light * k;
            return new Vector3(Math.Clamp(q.X, x0, x1), h, Math.Clamp(q.Z, -PH / 2, PH / 2));
        }
        Hint(""DISABLE_DEPTH_MASK"");
        Fill(10, 6, 4, 150 * strength);
        N(Vector3.UnitY);
        BeginShape(ShapeKind.Triangles);
        for (int j = 0; j < NV; j++)
            for (int i = 0; i < NU; i++)
            {
                int a = j * (NU + 1) + i, b = a + 1, c = a + NU + 1, d = c + 1;
                V(Cast(pos[a])); V(Cast(pos[b])); V(Cast(pos[d]));
                V(Cast(pos[a])); V(Cast(pos[d])); V(Cast(pos[c]));
            }
        EndShape();
        Hint(""ENABLE_DEPTH_MASK"");
    }

    // =====================================================================================
    // Letters: every one a little textured quad cut from the glyph atlas
    // =====================================================================================
    void BuildAtlas()
    {
        const string chars = "" !\""#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~“”‘’—–·…é"";
        var fonts = new[] { fRegular, fItalic, fBold };
        var g = CreateGraphics(AtlasSize, AtlasSize);
        g.BeginDraw();
        g.Clear();
        g.NoStroke();
        g.Fill(255);
        g.TextAlign(TextAlignH.Left, TextAlignV.Baseline);
        int cols = AtlasSize / Cell, n = 0;
        for (int f = 0; f < fonts.Length; f++)
        {
            g.TextFont(fonts[f], FontPx);
            foreach (char c in chars)
            {
                int x = n % cols * Cell, y = n / cols * CellH;
                g.Text(c.ToString(), x + Pad, y + Base);
                glyphs[n] = new Glyph
                {
                    U0 = x / (float)AtlasSize, V0 = y / (float)AtlasSize,
                    U1 = (x + Cell) / (float)AtlasSize, V1 = (y + CellH) / (float)AtlasSize,
                    Advance = g.TextWidth(c.ToString()),
                };
                glyphOf[(f, c)] = n++;
            }
        }
        g.EndDraw();
        atlas = g.Get();
    }

    int GlyphOf(int font, char c) => glyphOf.TryGetValue((font, c), out int i) ? i : glyphOf[(font, '?')];
    float Advance(int font, char c, float size) => glyphs[GlyphOf(font, c)].Advance * size / FontPx;
    float Measure(string s, float size, int font) { float w = 0; foreach (char c in s) w += Advance(font, c, size); return w; }

    sealed class Letter
    {
        public int Glyph, Font, Word, Line, Material;
        public char Ch;
        public string WordText;
        public float X, Y, W, H;                 // rest: the glyph's middle, and its size, in page units
        public Vector4 Ink;
        // Alive:
        public Vector3 Off, Vel, Spin;           // offset from rest (page units), its velocity, a turn (axis * speed)
        public Quaternion Rot = Quaternion.Identity;
        public float Scale = 1, Alpha = 1, Glow, Mirror;
        public Vector4 GlowColor = Rgb(255, 170, 70);
        public Vector4? Paint;                    // overrides the ink while set
        public int State;
        public float T, A, B, Seed;

        public void Rest() { Off = Vel = Vector3.Zero; Rot = Quaternion.Identity; Scale = 1; Alpha = 1; Glow = 0; Paint = null; State = 0; T = 0; Mirror = 0; }
    }

    /// <summary>A letter's quad, wherever its page is and wherever it has gone.</summary>
    void EmitLetter(Surface s, Letter L, Vector4 color, float sizeMul = 1, float lift = 0.35f)
    {
        var g = glyphs[L.Glyph];
        EmitQuad(s, L, color, L.W * sizeMul, L.H * sizeMul, lift, g.U0, g.V0, g.U1, g.V1);
    }

    /// <summary>A quad that moves with a letter (the letter itself, or the leaf it rides on).</summary>
    void EmitQuad(Surface s, Letter L, Vector4 color, float w, float h, float lift, float u0, float v0, float u1, float v1, Vector3 extra = default)
    {
        var f = s.At(L.X / TW, L.Y / TH);
        var q = L.Rot;
        if (L.Mirror > 0) q = q * Quaternion.CreateFromAxisAngle(Vector3.UnitY, L.Mirror * MathF.PI);
        var r = Vector3.Transform(Vector3.UnitX, q); var d = Vector3.Transform(Vector3.UnitY, q); var n = Vector3.Transform(Vector3.UnitZ, q);
        var R = f.U * r.X + f.V * r.Y + f.N * r.Z;
        var D = f.U * d.X + f.V * d.Y + f.N * d.Z;
        var Nn = f.U * n.X + f.V * n.Y + f.N * n.Z;
        var off = L.Off + extra;
        var c = f.P + (f.U * off.X + f.V * off.Y + f.N * off.Z) * K + Nn * (lift * K);
        float hw = w * 0.5f * K * L.Scale, hh = h * 0.5f * K * L.Scale;
        FillV(color);
        N(Nn);
        VT(c - R * hw - D * hh, u0, v0); VT(c + R * hw - D * hh, u1, v0);
        VT(c + R * hw + D * hh, u1, v1); VT(c - R * hw + D * hh, u0, v1);
    }

    void Letters(int material)
    {
        BeginShape(ShapeKind.Quads);
        Texture(atlas);
        foreach (var p in pages)
        {
            if (p.Surface == null) continue;
            foreach (var L in p.Letters)
            {
                if (L.Material != material || L.Alpha <= 0.01f) continue;
                var ink = L.Paint ?? L.Ink;
                EmitLetter(p.Surface, L, new Vector4(ink.X, ink.Y, ink.Z, ink.W * L.Alpha));
            }
        }
        EndShape();
    }

    /// <summary>Letters that give off light: their glyph again, added on top, and a halo.</summary>
    void GlowLetters()
    {
        BeginShape(ShapeKind.Quads);
        Texture(atlas);
        foreach (var p in pages)
        {
            if (p.Surface == null) continue;
            foreach (var L in p.Letters)
                if (L.Glow > 0.01f) EmitLetter(p.Surface, L, new Vector4(L.GlowColor.X, L.GlowColor.Y, L.GlowColor.Z, 255 * Clamp01(L.Glow) * L.Alpha), 1, 0.5f);
        }
        EndShape();
        foreach (var p in pages)
        {
            if (p.Surface == null) continue;
            foreach (var L in p.Letters)
                if (L.Glow > 0.01f)
                {
                    var at = p.Surface.W(L.X + L.Off.X, L.Y + L.Off.Y, L.Off.Z + 1);
                    AddGlow(at, L.W * 1.3f * K * L.Scale, L.GlowColor, 0.22f * Clamp01(L.Glow) * L.Alpha);
                }
        }
    }

    // =====================================================================================
    // Sprites: soft round dots facing the camera (flames, sparks, snow, stars)
    // =====================================================================================
    struct Sprite { public Vector3 P; public float S; public Vector4 C; }
    readonly List<Sprite> glow = new(), soft = new();
    readonly List<(Vector3 A, Vector3 B, float W, Vector4 C)> lines = new();

    void AddGlow(Vector3 p, float size, Vector4 c, float alpha = 1) => glow.Add(new Sprite { P = p, S = size, C = new Vector4(c.X, c.Y, c.Z, c.W * alpha) });
    void AddSoft(Vector3 p, float size, Vector4 c, float alpha = 1) => soft.Add(new Sprite { P = p, S = size, C = new Vector4(c.X, c.Y, c.Z, c.W * alpha) });
    void AddLine(Vector3 a, Vector3 b, float width, Vector4 c, float alpha = 1) => lines.Add((a, b, width, new Vector4(c.X, c.Y, c.Z, c.W * alpha)));
    readonly List<(Vector3 A, Vector3 B, float W, Vector4 C)> streaks = new(), softStreaks = new();
    /// <summary>A soft dot stretched from a to b: a tongue of flame, a falling drop. Added light,
    /// or (soft) ordinary paint, which shows on light paper too.</summary>
    void AddStreak(Vector3 a, Vector3 b, float width, Vector4 c, float alpha = 1, bool soft = false) =>
        (soft ? softStreaks : streaks).Add((a, b, width, new Vector4(c.X, c.Y, c.Z, c.W * alpha)));

    void DrawSprites(List<Sprite> list)
    {
        if (list.Count == 0) return;
        BeginShape(ShapeKind.Quads);
        Texture(glowTex);
        foreach (var s in list)
        {
            var r = camRight * (s.S * 0.5f); var u = camUp * (s.S * 0.5f);
            FillV(s.C);
            VT(s.P - r + u, 0, 0); VT(s.P + r + u, 1, 0); VT(s.P + r - u, 1, 1); VT(s.P - r - u, 0, 1);
        }
        EndShape();
    }

    void DrawStreaks(List<(Vector3 A, Vector3 B, float W, Vector4 C)> list)
    {
        if (list.Count == 0) return;
        BeginShape(ShapeKind.Quads);
        Texture(glowTex);
        foreach (var (a, b, w, c) in list)
        {
            var along = b - a;
            var side = Vector3.Cross(along, camFwd);
            if (side.LengthSquared() < 1e-8f) side = camRight;
            side = Vector3.Normalize(side) * (w * 0.5f);
            // Pad the ends by half a width so the stretched dot stays round at its tips.
            var dir = along.LengthSquared() > 1e-8f ? Vector3.Normalize(along) * (w * 0.5f) : camUp * (w * 0.5f);
            var a2 = a - dir; var b2 = b + dir;
            FillV(c);
            VT(a2 - side, 0, 0); VT(a2 + side, 1, 0); VT(b2 + side, 1, 1); VT(b2 - side, 0, 1);
        }
        EndShape();
    }

    void DrawLines()
    {
        if (lines.Count == 0) return;
        BeginShape(ShapeKind.Quads);
        Texture(glowTex);
        foreach (var (a, b, w, c) in lines)
        {
            var side = Vector3.Cross(b - a, camFwd);
            if (side.LengthSquared() < 1e-6f) continue;
            side = Vector3.Normalize(side) * (w * 0.5f);
            FillV(c);
            VT(a - side, 0.5f, 0.5f); VT(a + side, 0.5f, 0.5f); VT(b + side, 0.5f, 0.5f); VT(b - side, 0.5f, 0.5f);
        }
        EndShape();
    }

    /// <summary>A soft round spot lying on a page (a caustic, a shadow).</summary>
    void Decal(Surface s, float x, float y, float z, float radius, Vector4 c)
    {
        FillV(c);
        N(s.Dir(Vector3.UnitZ));
        BeginShape(ShapeKind.Quads);
        Texture(glowTex);
        VT(s.W(x - radius, y - radius, z), 0, 0); VT(s.W(x + radius, y - radius, z), 1, 0);
        VT(s.W(x + radius, y + radius, z), 1, 1); VT(s.W(x - radius, y + radius, z), 0, 1);
        EndShape();
    }

    // =====================================================================================
    // Pictures made once: paper, leather, wood, a glow, a leaf, frost
    // =====================================================================================
    void BuildPictures()
    {
        var g = CreateGraphics(64, 64);
        g.BeginDraw(); g.Clear(); g.NoStroke();
        for (int r = 32; r > 0; r--) { float k = 1 - r / 32f; g.Fill(255, 255 * k * k); g.Circle(32, 32, r * 2); }
        g.EndDraw();
        glowTex = g.Get();

        g = CreateGraphics(128, 128);
        g.BeginDraw(); g.Clear(); g.NoStroke(); g.Fill(255);
        g.BeginShape();
        for (int i = 0; i <= 40; i++)
        {
            float a = i / 40f * TWO_PI, rr = 52 * (0.55f + 0.45f * MathF.Abs(MathF.Sin(a))) * (1 + 0.08f * MathF.Sin(a * 9));
            g.Vertex(64 + MathF.Cos(a) * rr * 0.62f, 64 + MathF.Sin(a) * rr);
        }
        g.EndShape(true);
        g.Stroke(0, 0, 0, 60); g.StrokeWeight(2); g.Line(64, 14, 64, 118);
        for (int i = 0; i < 5; i++) { float y = 34 + i * 16; g.Line(64, y, 40, y - 10); g.Line(64, y, 88, y - 10); }
        g.EndDraw();
        leafTex = g.Get();

        g = CreateGraphics(1024, 680);
        g.BeginDraw();
        g.Background(70, 44, 30);
        g.NoFill();
        for (int i = 0; i < 260; i++)
        {
            float y = i * 2.62f + (float)Math.Sin(i * 0.37) * 3;
            g.Stroke(40 + 30 * Noise(i * 0.1f, 0), 24 + 18 * Noise(i * 0.1f, 3), 14 + 10 * Noise(i * 0.1f, 7), 90);
            g.StrokeWeight(1.5f + 2 * Noise(i * 0.2f, 1));
            g.BeginShape();
            for (int x = 0; x <= 1024; x += 32) g.Vertex(x, y + 6 * (Noise(x * 0.004f, i * 0.05f) - 0.5f));
            g.EndShape();
        }
        g.NoStroke();
        for (int k = 0; k < 40; k++) { g.Fill(0, 0, 0, 6); g.Rect(-k * 8, -k * 8, 1024 + k * 16, 680 + k * 16); }
        g.EndDraw();
        woodTex = g.Get();

        g = CreateGraphics(128, 128);
        g.BeginDraw(); g.Clear(); g.NoStroke();
        for (int k = 0; k < 40; k++) { g.Fill(0, 0, 0, 7); g.Rect(14 + k * 0.6f, 14 + k * 0.6f, 100 - k * 1.2f, 100 - k * 1.2f, 18); }
        g.EndDraw();
        shadowTex = g.Get();

        // Frost: feathery crystals grown from a few seeds.
        g = CreateGraphics(420, 560);
        g.BeginDraw(); g.Clear(); g.Stroke(255, 255, 255, 120);
        var rng = new System.Random(4);
        void Branch(float x, float y, float a, float len, int depth)
        {
            if (depth == 0 || len < 2) return;
            float x2 = x + MathF.Cos(a) * len, y2 = y + MathF.Sin(a) * len;
            g.StrokeWeight(Math.Max(0.6f, depth * 0.35f));
            g.Line(x, y, x2, y2);
            for (int k = 1; k <= 3; k++)
            {
                float t = k / 4f, bx = x + (x2 - x) * t, by = y + (y2 - y) * t;
                Branch(bx, by, a + 1.0f, len * 0.38f, depth - 1);
                Branch(bx, by, a - 1.0f, len * 0.38f, depth - 1);
            }
            Branch(x2, y2, a + (float)(rng.NextDouble() - 0.5) * 0.5f, len * 0.7f, depth - 1);
        }
        for (int i = 0; i < 26; i++)
        {
            float x = (float)rng.NextDouble() * 420, y = (float)rng.NextDouble() * 560;
            for (int k = 0; k < 6; k++) Branch(x, y, k * MathF.PI / 3 + (float)rng.NextDouble() * 0.3f, 24 + (float)rng.NextDouble() * 30, 4);
        }
        g.EndDraw();
        frostTex = g.Get();
    }

    /// <summary>A page's paper: grain, a soft vignette and its number -- or leather, or night.</summary>
    PImage MakePaper(int number, string kind)
    {
        var g = CreateGraphics((int)TW, (int)TH);
        var rng = new System.Random(number * 7 + 1);
        g.BeginDraw();
        g.NoStroke();
        if (kind == ""leather"")
        {
            g.Background(104, 26, 32);
            for (int k = 0; k < 5000; k++) { g.Fill(rng.Next(2) == 0 ? 0 : 255, rng.Next(4, 12)); g.Circle((float)rng.NextDouble() * TW, (float)rng.NextDouble() * TH, 1 + (float)rng.NextDouble() * 3); }
            g.NoFill(); g.Stroke(214, 170, 90); g.StrokeWeight(2.5f); g.Rect(26, 26, TW - 52, TH - 52); g.StrokeWeight(1); g.Rect(34, 34, TW - 68, TH - 68);
            g.EndDraw();
            return g.Get();
        }
        if (kind == ""night"")
        {
            g.Background(10, 16, 32);
            for (int k = 0; k < 160; k++) { g.Fill(200, 210, 255, rng.Next(30, 150)); g.Circle((float)rng.NextDouble() * TW, (float)rng.NextDouble() * TH * 0.75f, (float)rng.NextDouble() * 1.8f); }
            // Pines on a dark hill.
            g.Fill(4, 7, 14);
            g.BeginShape(); g.Vertex(0, TH); g.Vertex(0, TH - 70);
            for (int x = 0; x <= TW; x += 10) g.Vertex(x, TH - 70 - 26 * MathF.Sin(x * 0.012f + 0.6f));
            g.Vertex(TW, TH); g.EndShape(true);
            for (int k = 0; k < 9; k++)
            {
                float x = 24 + k * 46 + (float)rng.NextDouble() * 18, base0 = TH - 70 - 26 * MathF.Sin(x * 0.012f + 0.6f), h = 50 + (float)rng.NextDouble() * 60;
                g.Triangle(x - h * 0.22f, base0 + 6, x + h * 0.22f, base0 + 6, x, base0 - h);
                g.Triangle(x - h * 0.17f, base0 - h * 0.3f, x + h * 0.17f, base0 - h * 0.3f, x, base0 - h * 1.15f);
            }
            g.EndDraw();
            return g.Get();
        }
        g.Background(244, 236, 216);
        for (int k = 0; k < 2200; k++) { g.Fill(150, 120, 80, rng.Next(5, 16)); g.Circle((float)rng.NextDouble() * TW, (float)rng.NextDouble() * TH, (float)rng.NextDouble() * 2.2f); }
        g.NoFill();
        for (int k = 0; k < 14; k++) { g.Stroke(120, 90, 50, 5); g.StrokeWeight(10); g.Rect(k * 5, k * 5, TW - k * 10, TH - k * 10); }
        g.NoStroke();
        g.Fill(120, 100, 70);
        g.TextFont(fRegular, 13);
        g.TextAlign(TextAlignH.Center, TextAlignV.Bottom);
        if (number > 1 && number < 11) g.Text(number.ToString(), TW / 2, TH - 18);
        g.EndDraw();
        return g.Get();
    }

    // =====================================================================================
    // Camera and pointer
    // =====================================================================================
    void Camera3D(float t)
    {
        float targetX = turned == 0 && flipping < 0 ? PW / 2 : turned == Sheets && flipping < 0 ? -PW / 2 : 0;
        camX = Lerp(camX, targetX, 0.05f);
        float sway = t - lastInput > 6 ? 0.05f * MathF.Sin(t * 0.13f) : 0;
        float a = yaw + sway, d = dist / zoom;
        target = new Vector3(camX, 0, 18);
        eye = target + new Vector3(MathF.Sin(a) * MathF.Cos(pitch), MathF.Sin(pitch), MathF.Cos(a) * MathF.Cos(pitch)) * d;
        camFwd = Vector3.Normalize(target - eye);
        camRight = Vector3.Normalize(Vector3.Cross(camFwd, Vector3.UnitY));
        camUp = Vector3.Cross(camRight, camFwd);
    }

    /// <summary>The ray from the eye through the mouse.</summary>
    Vector3 MouseRay()
    {
        float tan = MathF.Tan(Fov / 2), aspect = Width / (float)Height;
        float nx = 2 * MouseX / Width - 1, ny = 1 - 2 * MouseY / Height;
        return Vector3.Normalize(camFwd + camRight * (nx * tan * aspect) + camUp * (ny * tan));
    }

    /// <summary>The flat page under the mouse, and where on it (page units).</summary>
    Page PageUnderMouse(out float x, out float y)
    {
        x = y = -1;
        var ray = MouseRay();
        foreach (var p in pages)
        {
            if (p.Surface == null || !p.Surface.IsFlat) continue;
            var o = p.Surface.W(0, 0, 0);
            if (Math.Abs(ray.Y) < 1e-5f) continue;
            float k = (o.Y - eye.Y) / ray.Y;
            if (k <= 0) continue;
            var hit = eye + ray * k;
            float px = (hit.X - o.X) / K, py = (hit.Z - o.Z) / K;
            if (px < 0 || px > TW || py < 0 || py > TH) continue;
            x = px; y = py;
            return p;
        }
        return null;
    }

    Page touched; bool orbiting; float pressX, pressY;

    /// <summary>Every frame: what the mouse is over (pages react to it), and dragging.</summary>
    void Pointer()
    {
        foreach (var p in pages) { p.HoverX = -1; p.HoverY = -1; }
        if (dragging)
        {
            // The sheet follows the mouse: where the ray meets the hinge's height.
            var ray = MouseRay();
            float h = Board + Math.Max(LeftHeight(), RightHeight()) + 8;
            float k = Math.Abs(ray.Y) > 1e-5f ? (h - eye.Y) / ray.Y : 0;
            var hit = eye + ray * k;
            theta = MathF.Acos(Math.Clamp(hit.X / PW, -1, 1));
            return;
        }
        if (orbiting) return;
        var page = PageUnderMouse(out float x, out float y);
        bool ghost = Now - lastInput > 25;
        if (ghost)
        {
            // Left alone, a ghost hand wanders over the open pages.
            float t = Now;
            int side = (int)(t / 7) % 2;
            int idx = side == 0 ? 2 * turned - 1 : 2 * turned;
            if (idx >= 0 && idx < pages.Length && pages[idx].Surface != null && pages[idx].Surface.IsFlat)
            {
                page = pages[idx];
                x = TW * (0.5f + 0.4f * MathF.Sin(t * 0.7f)); y = TH * (0.5f + 0.38f * MathF.Sin(t * 0.43f + 1));
            }
        }
        if (page != null)
        {
            page.HoverX = x; page.HoverY = y;
            if (touched == page && IsMousePressed) page.Drag(x, y);
        }
    }

    public override void MousePressed()
    {
        lastInput = Now;
        pressX = MouseX; pressY = MouseY;
        var hit = PageUnderMouse(out float x, out float y);
        if (hit == null || flipping >= 0)
        {
            orbiting = hit == null && flipping < 0;
            return;
        }
        bool isLeft = hit.Surface.W(0, 0, 0).X < -1;
        bool outer = isLeft ? x < TW * 0.1f : x > TW * 0.9f;
        if (outer)
        {
            dragDir = isLeft ? -1 : +1;
            if (dragDir > 0 && turned >= Sheets || dragDir < 0 && turned <= 0) return;
            flipping = dragDir > 0 ? turned : turned - 1;
            theta = dragDir > 0 ? 0 : PI;
            dragging = true;
            Rustle();
            return;
        }
        touched = hit;
        hit.Press(x, y);
    }

    public override void MouseDragged()
    {
        lastInput = Now;
        if (orbiting)
        {
            yaw -= (MouseX - PMouseX) * 0.005f;
            pitch = Math.Clamp(pitch + (MouseY - PMouseY) * 0.004f, 0.3f, 1.4f);
        }
    }

    public override void MouseReleased()
    {
        if (dragging)
        {
            dragging = false;
            bool through = dragDir > 0 ? theta > HALF_PI * 0.7f : theta < PI - HALF_PI * 0.7f;
            thetaFrom = theta;
            thetaTarget = dragDir > 0 ? (through ? PI : 0) : (through ? 0 : PI);
            flipTime = 1.15f;
            flipStart = Now - (1 - Math.Abs(thetaTarget - theta) / PI) * flipTime * 0.5f;
            autoFlip = true;
        }
        touched = null; orbiting = false;
    }

    public override void MouseWheel(float delta) => zoom = Constrain(zoom * (delta > 0 ? 1 / 1.08f : 1.08f), 0.6f, 2.6f);

    public override void KeyPressed()
    {
        lastInput = Now;
        if (KeyCode == RIGHT || Key == ' ') Flip(+1);
        else if (KeyCode == LEFT) Flip(-1);
    }

    /// <summary>Turns forward or back until the given page is open (from the Contents).</summary>
    public void GoTo(int pageNumber)
    {
        int target = (pageNumber + 1) / 2;
        int steps = target - turned;
        for (int i = 0; i < Math.Abs(steps); i++) pendingFlips.Enqueue(Math.Sign(steps));
    }

    void Hud()
    {
        TextFont(ui, 13);
        TextAlign(TextAlignH.Center, TextAlignV.Bottom);
        Fill(255, 240, 220, 120);
        Text(""drag a page's edge (or Left / Right) to turn · touch the pages · drag the table to look around · wheel: zoom"", Width / 2, Height - 14);
    }

    // =====================================================================================
    // Turning
    // =====================================================================================
    float TurnDirection => dragging ? dragDir : thetaTarget >= thetaFrom ? 1 : -1;

    void Flip(int direction, bool fast = false)
    {
        if (flipping >= 0) return;
        if (direction > 0 && turned < Sheets) { flipping = turned; thetaFrom = 0; thetaTarget = PI; }
        else if (direction < 0 && turned > 0) { flipping = turned - 1; thetaFrom = PI; thetaTarget = 0; }
        else return;
        theta = thetaFrom; flipStart = Now; autoFlip = true;
        flipTime = fast ? 0.55f : 1.15f;
        Rustle();
    }

    void Turning(float t)
    {
        if (flipping < 0 || dragging || !autoFlip) return;
        float k = Clamp01((t - flipStart) / flipTime);
        float e = k < 0.5f ? 4 * k * k * k : 1 - MathF.Pow(-2 * k + 2, 3) / 2;
        theta = Lerp(thetaFrom, thetaTarget, e);
        if (k >= 1)
        {
            if (thetaTarget >= PI - 0.01f && flipping >= turned) turned = flipping + 1;
            else if (thetaTarget <= 0.01f && flipping < turned) turned = flipping;
            flipping = -1; autoFlip = false;
        }
    }

    void Rustle() => Play(70, amp: 0.12, attack: 0.05, release: 0.45, cutoff: 70, synth: Synth.Noise);

    // =====================================================================================
    // Pages
    // =====================================================================================
    abstract class Page
    {
        protected readonly BookOfElements S;
        public int Number;
        public PImage Paper;
        public Surface Surface;
        public readonly List<Letter> Letters = new();
        public float HoverX = -1, HoverY = -1, Live;
        public bool WasOpen;
        protected readonly System.Random Rng;
        protected virtual string PaperKind => ""paper"";
        protected Vector4 InkColor = Rgb(40, 30, 24);

        protected Page(BookOfElements s) { S = s; Rng = new System.Random(GetType().Name.Length * 97 + 3); }

        public void Build()
        {
            Paper = S.MakePaper(Number, PaperKind);
            Layout();
            foreach (var L in Letters) L.Seed = (float)Rng.NextDouble();
            Init();
        }

        protected virtual void Layout() { }
        protected virtual void Init() { }
        public virtual void Open(float t) { }
        public virtual void Close() { foreach (var L in Letters) L.Rest(); }
        public virtual void Step(float dt, float t, bool open) { }
        public virtual void DrawLit() { }
        public virtual void DrawTranslucent() { }
        public virtual void DrawSoft() { }
        public virtual void DrawGlow() { }
        public virtual void Press(float x, float y) { }
        public virtual void Drag(float x, float y) { }

        protected float R(float a, float b) => a + (float)Rng.NextDouble() * (b - a);

        // ---- writing --------------------------------------------------------------------
        int words;
        protected float Write(string text, float x, float baseline, float size, int font, Vector4 ink, int line, int material = 0)
        {
            float sc = size / FontPx;
            bool inWord = false;
            string current = """";
            int start = Letters.Count;
            foreach (char c in text)
            {
                if (c == ' ') { if (inWord) { Close(start, current); start = Letters.Count; current = """"; words++; } inWord = false; x += S.Advance(font, c, size); continue; }
                inWord = true; current += c;
                int gi = S.GlyphOf(font, c);
                Letters.Add(new Letter
                {
                    Glyph = gi, Font = font, Ch = c, Word = words, Line = line, Material = material, Ink = ink,
                    X = x - Pad * sc + Cell * sc / 2, Y = baseline - Base * sc + CellH * sc / 2, W = Cell * sc, H = CellH * sc,
                });
                x += S.glyphs[gi].Advance * sc;
            }
            if (inWord) { Close(start, current); words++; }
            return x;
        }

        void Close(int start, string word)
        {
            string clean = new string(word.Where(char.IsLetter).ToArray()).ToLowerInvariant();
            for (int i = start; i < Letters.Count; i++) Letters[i].WordText = clean;
        }

        protected float Centered(string text, float baseline, float size, int font, Vector4 ink, int line, int material = 0) =>
            Write(text, (TW - S.Measure(text, size, font)) / 2, baseline, size, font, ink, line, material);

        /// <summary>A title, an author, and lines of verse wrapped to the page; returns where it ended.</summary>
        protected float Poem(string title, string author, string[] verse, float top, float size = 21, bool prose = false)
        {
            float ts = Math.Min(27, 27 * (TW - 70) / S.Measure(title, 27, 2));
            Centered(title, top + 26, ts, 2, InkColor, -1);
            var faint = new Vector4(InkColor.X, InkColor.Y, InkColor.Z, 170);
            Centered(author, top + 50, 14, 1, faint, -1);
            float y = top + 96;
            int line = 0;
            foreach (var l in verse)
            {
                if (l == """") { y += size * 0.7f; continue; }
                foreach (var part in Wrap(l, size, TW - 84))
                {
                    bool indent = !prose && part.StartsWith(""  "");
                    Write(part.Trim(), 42 + (indent ? 18 : 0), y, size, 0, InkColor, line++);
                    y += size * 1.42f;
                }
            }
            return y;
        }

        List<string> Wrap(string line, float size, float width)
        {
            var result = new List<string>();
            string current = """";
            foreach (var w in line.Split(' '))
            {
                string next = current == """" ? w : current + "" "" + w;
                if (S.Measure(next.Trim(), size, 0) > width && current != """") { result.Add(current); current = ""  "" + w; }
                else current = next;
            }
            result.Add(current);
            return result;
        }

        protected Letter LetterAt(float x, float y, float reach = 1)
        {
            Letter best = null; float bd = float.MaxValue;
            foreach (var L in Letters)
            {
                float dx = Math.Abs(x - L.X) / (L.W * 0.4f * reach), dy = Math.Abs(y - L.Y) / (L.H * 0.35f * reach);
                float d = dx * dx + dy * dy;
                if (d < 1 && d < bd) { bd = d; best = L; }
            }
            return best;
        }

        protected Vector3 W(float x, float y, float z) => Surface.W(x, y, z);
    }

    // =====================================================================================
    // The cover, the contents, the colophon, the back
    // =====================================================================================
    sealed class CoverPage : Page
    {
        public CoverPage(BookOfElements s) : base(s) { }
        protected override string PaperKind => ""leather"";
        static readonly Vector4 Gold = Rgb(226, 182, 96);

        protected override void Layout()
        {
            Centered(""A Book"", 200, 54, 2, Gold, 0, material: 1);
            Centered(""of Elements"", 262, 54, 2, Gold, 1, material: 1);
            Centered(""fire · water · grass · glass"", 330, 17, 1, Gold, 2, material: 1);
            Centered(""snow · wind · ice · stars"", 356, 17, 1, Gold, 3, material: 1);
        }

        public override void Step(float dt, float t, bool open)
        {
            foreach (var L in Letters)
            {
                // The gold letters breathe, so the light runs over them; the mouse lifts them.
                float wave = 1.2f + 1.2f * MathF.Sin(t * 1.6f - L.X * 0.02f);
                float near = HoverX >= 0 ? Smooth(70, 0, MathF.Sqrt((HoverX - L.X) * (HoverX - L.X) + (HoverY - L.Y) * (HoverY - L.Y))) : 0;
                L.Off.Z = Lerp(L.Off.Z, (wave + 14 * near) * Live, 0.15f);
                L.Rot = Quaternion.CreateFromAxisAngle(Vector3.UnitX, 0.25f * near * MathF.Sin(t * 3 + L.Seed * 6));
            }
        }

        public override void DrawGlow()
        {
            float t = S.Now;
            for (int i = 0; i < 26; i++)
            {
                float x = 50 + (i * 73 % 320), y = 150 + (i * 131 % 260), tw = MathF.Pow(Math.Max(0, MathF.Sin(t * 1.3f + i * 2.1f)), 8);
                if (tw > 0.02f) S.AddGlow(W(x, y, 3), 9 * K, Rgb(255, 220, 140), tw * Live);
            }
        }
    }

    sealed class ContentsPage : Page
    {
        public ContentsPage(BookOfElements s) : base(s) { }
        readonly List<(float Y0, float Y1, int Page)> rows = new();

        protected override void Layout()
        {
            Centered(""Contents"", 96, 30, 2, InkColor, 0);
            var entries = new[] { (""Fire"", ""The Tyger · William Blake""), (""Water"", ""The Rime of the Ancient Mariner · Coleridge""), (""Grass"", ""Song of Myself · Walt Whitman""), (""Glass"", ""Through the Looking-Glass · Lewis Carroll""), (""Snow"", ""Snow-flakes · H. W. Longfellow""), (""Wind"", ""Ode to the West Wind · P. B. Shelley""), (""Ice"", ""Fire and Ice · Robert Frost""), (""Stars"", ""Stars · Sara Teasdale"") };
            float y = 150;
            for (int i = 0; i < entries.Length; i++)
            {
                Write(entries[i].Item1, 52, y, 21, 0, InkColor, 1 + i * 2);
                Write((i + 2).ToString(), TW - 66, y, 18, 0, InkColor, 1 + i * 2);
                Write(entries[i].Item2, 62, y + 18, 11.5f, 1, new Vector4(InkColor.X, InkColor.Y, InkColor.Z, 190), 2 + i * 2);
                rows.Add((y - 22, y + 24, i + 2));
                y += 44;
            }
            Centered(""Every page is alive. Touch it."", 520, 12, 1, new Vector4(InkColor.X, InkColor.Y, InkColor.Z, 160), 40);
        }

        int Row(float y) { for (int i = 0; i < rows.Count; i++) if (y >= rows[i].Y0 && y < rows[i].Y1) return i; return -1; }

        public override void Step(float dt, float t, bool open)
        {
            int row = HoverX >= 0 ? Row(HoverY) : -1;
            foreach (var L in Letters)
            {
                bool lit = row >= 0 && (L.Line == 1 + row * 2 || L.Line == 2 + row * 2);
                float wave = lit ? 6 + 5 * MathF.Sin(t * 7 - L.X * 0.05f) : 0;
                L.Off.Z = Lerp(L.Off.Z, wave * Live, 0.2f);
                L.Paint = lit ? Rgb(150, 50, 30) : null;
            }
        }

        public override void Press(float x, float y)
        {
            int row = Row(y);
            if (row >= 0) S.GoTo(rows[row].Page);
        }
    }

    sealed class ColophonPage : Page
    {
        public ColophonPage(BookOfElements s) : base(s) { }

        protected override void Layout()
        {
            Centered(""All poems are in the public domain."", 220, 15, 1, InkColor, 0);
            Centered(""Set in Georgia; every letter and every page"", 250, 15, 1, InkColor, 1);
            Centered(""is drawn live, in 3D, by Dana --"", 274, 15, 1, InkColor, 2);
            Centered(""creative coding in C#."", 298, 15, 1, InkColor, 3);
            Centered(""Finis"", 380, 30, 2, InkColor, 4);
        }

        public override void Open(float t)
        {
            // The words fly in and settle when the page opens.
            foreach (var L in Letters)
            {
                L.Off = new Vector3(R(-260, 260), R(-260, 260), R(60, 220));
                L.Rot = Quaternion.CreateFromYawPitchRoll(R(-3, 3), R(-3, 3), R(-3, 3));
                L.Vel = Vector3.Zero;
            }
        }

        public override void Step(float dt, float t, bool open)
        {
            foreach (var L in Letters)
            {
                float near = HoverX >= 0 ? Smooth(60, 0, Vector2.Distance(new(HoverX, HoverY), new(L.X, L.Y))) : 0;
                var goal = new Vector3(0, 0, 10 * near * (1 + MathF.Sin(t * 8 + L.Seed * 9)));
                L.Vel += ((goal - L.Off) * 14 - L.Vel * 5.5f) * dt;
                L.Off += L.Vel * dt;
                L.Rot = Quaternion.Slerp(L.Rot, Quaternion.Identity, 0.07f);
            }
        }
    }

    sealed class BackCoverPage : Page
    {
        public BackCoverPage(BookOfElements s) : base(s) { }
        protected override string PaperKind => ""leather"";
        protected override void Layout() => Centered(""Dana"", 470, 20, 1, Rgb(226, 182, 96), 0, material: 1);
    }

    // =====================================================================================
    // Fire: Blake, The Tyger. Touch a word and it catches fire, flies up as embers, comes back.
    // =====================================================================================
    sealed class FirePage : Page
    {
        struct Flame { public Vector3 X, V; public float Age, Life, Size; public bool Spark; }
        readonly List<Flame> flames = new();
        float nextIgnite;
        public FirePage(BookOfElements s) : base(s) { }

        protected override void Layout() => Poem(""The Tyger"", ""William Blake, 1794"", new[]
        {
            ""Tyger Tyger, burning bright,"", ""In the forests of the night;"", ""What immortal hand or eye,"", ""Could frame thy fearful symmetry?"",
            """", ""In what distant deeps or skies."", ""Burnt the fire of thine eyes?"", ""On what wings dare he aspire?"", ""What the hand, dare seize the fire?"",
        }, 40, 20);

        void Ignite(int word)
        {
            bool any = false;
            foreach (var L in Letters) if (L.Word == word && L.State == 0 && L.Line >= 0) { L.State = 1; L.T = 0; any = true; }
            if (any) S.Play(40, amp: 0.05, attack: 0.3, release: 1.2, cutoff: 60, synth: Synth.Noise);
        }

        public override void Open(float t) => nextIgnite = t + 1.0f;
        public override void Close() { base.Close(); flames.Clear(); }

        public override void Step(float dt, float t, bool open)
        {
            if (!open) return;
            if (HoverX >= 0) { var L = LetterAt(HoverX, HoverY, 1.4f); if (L != null && L.Line >= 0) Ignite(L.Word); }
            if (t > nextIgnite)
            {
                var resting = Letters.Where(l => l.State == 0 && l.Line >= 0).ToList();
                if (resting.Count > 0) Ignite(resting[Rng.Next(resting.Count)].Word);
                nextIgnite = t + R(2.0f, 3.6f);
            }
            foreach (var L in Letters)
            {
                L.T += dt;
                switch (L.State)
                {
                    case 1: // catching: the ink glows, flames lick up from it
                    {
                        float k = Math.Min(1, L.T / 1.0f);
                        L.Paint = Vector4.Lerp(InkColor, Rgb(60, 16, 4), k);
                        L.Glow = k; L.GlowColor = Rgb(255, 140 + 70 * k, 50);
                        L.Off = new Vector3(R(-0.5f, 0.5f) * k, R(-0.5f, 0.5f) * k, 0);
                        if (Rng.NextDouble() < 0.7 * k) Emit(L.X + R(-L.W * 0.22f, L.W * 0.22f), L.Y + R(-L.H * 0.12f, L.H * 0.15f), 1, 0.55f, false);
                        if (L.T > 1.0f)
                        {
                            L.State = 2; L.T = 0;
                            L.Vel = new Vector3(R(-25, 25), R(-25, 10), R(45, 85));
                            L.Spin = Vector3.Normalize(new Vector3(R(-1, 1), R(-1, 1), R(-1, 1))) * R(2, 5);
                        }
                        break;
                    }
                    case 2: // an ember, flying up and away
                    {
                        float k = L.T / 2.6f;
                        L.Vel.Z += 30 * dt;
                        L.Vel.X += (S.Noise(L.Off.Z * 0.02f, L.Seed * 9) - 0.5f) * 180 * dt;
                        L.Off += L.Vel * dt;
                        L.Rot = Quaternion.Normalize(L.Rot * Quaternion.CreateFromAxisAngle(Vector3.Normalize(L.Spin), L.Spin.Length() * dt));
                        L.Scale = 1 - 0.55f * k; L.Alpha = Clamp01(1 - k); L.Glow = 1.3f - k;
                        L.GlowColor = Rgb(255, 170 - 90 * Clamp01(k), 60 - 40 * Clamp01(k));
                        L.Paint = Rgb(28, 10, 4);
                        if (Rng.NextDouble() < 0.5) Emit(L.X + L.Off.X, L.Y + L.Off.Y, L.Off.Z, 1, true);
                        if (k >= 1) { L.State = 3; L.T = 0; L.Alpha = 0; L.Glow = 0; }
                        break;
                    }
                    case 3: if (L.T > 1.0f + L.Seed * 1.6f) { L.State = 4; L.T = 0; } break;
                    case 4: // back from the ashes
                    {
                        float e = Clamp01(L.T / 0.9f);
                        L.Off = new Vector3(0, 0, 26 * (1 - e) * (1 - e));
                        L.Rot = Quaternion.Identity; L.Scale = 1; L.Alpha = e; L.Paint = null;
                        L.Glow = 0.8f * (1 - e); L.GlowColor = Rgb(255, 210, 130);
                        if (e >= 1) L.Rest();
                        break;
                    }
                }
            }
            // A bed of flames along the foot of the page, standing up out of it.
            for (int i = 0; i < 14; i++)
            {
                // Tongues: the flames gather where a slow noise says, so they lick up in clumps.
                float x = R(6, TW - 6);
                float clump = S.Noise(x * 0.018f, t * 1.3f);
                if (clump < 0.38f) continue;
                Emit(x, R(CharLine(x, t) + 10, TH - 6), 0, 0.6f + clump, false);
            }
            for (int i = flames.Count - 1; i >= 0; i--)
            {
                var f = flames[i];
                f.Age += dt;
                if (f.Age > f.Life) { flames.RemoveAt(i); continue; }
                f.V.X += (S.Noise(f.X.X * 0.02f, f.X.Z * 0.02f - t * 1.5f) - 0.5f) * 240 * dt;
                f.V.Y += (S.Noise(f.X.Y * 0.02f + 5, t) - 0.5f) * 100 * dt;
                f.V.Z += (f.Spark ? 8 : 60) * dt;
                f.V *= 1 - 0.8f * dt;
                f.X += f.V * dt;
                flames[i] = f;
            }
            // The flames light the room.
            S.fireLight = W(TW / 2, 470, 60);
            S.fireLightPower = Math.Max(S.fireLightPower, Live * (0.25f + 0.18f * S.Noise(t * 7, 0)));
        }

        void Emit(float x, float y, float z, float scale, bool spark)
        {
            if (flames.Count > 1800) return;
            flames.Add(new Flame
            {
                X = new Vector3(x, y, z),
                V = spark ? new Vector3(R(-35, 35), R(-60, 10), R(40, 110)) : new Vector3(R(-6, 6), R(-70, -35) * scale, R(45, 90) * scale),   // climbing the page
                Life = spark ? R(0.6f, 1.4f) : R(0.5f, 0.9f),
                Size = (spark ? R(2.5f, 5) : R(20, 34)) * scale,
                Spark = spark,
            });
        }

        static Vector4 FlameColor(float k) =>
            k < 0.15f ? Vector4.Lerp(Rgb(255, 245, 200), Rgb(255, 170, 50), k / 0.15f) :
            k < 0.45f ? Vector4.Lerp(Rgb(255, 170, 50), Rgb(240, 70, 15), (k - 0.15f) / 0.3f) :
                       Vector4.Lerp(Rgb(240, 70, 15), Rgb(110, 20, 8), (k - 0.45f) / 0.55f);

        public override void DrawGlow()
        {
            foreach (var f in flames)
            {
                float k = f.Age / f.Life;
                var c = f.Spark ? Rgb(255, 200, 120) : FlameColor(k);
                float a = (f.Spark ? 0.9f : 0.34f) * MathF.Pow(1 - k, 1.6f) * Live;
                float size = f.Size * (f.Spark ? 1 : 1.15f - 0.75f * k) * K;
                // A tongue: stretched back along its own motion, so the fire licks upward.
                var head = W(f.X.X, f.X.Y, f.X.Z);
                var tail = W(f.X.X - f.V.X * 0.09f, f.X.Y - f.V.Y * (f.Spark ? 0.05f : 0.22f), f.X.Z - f.V.Z * (f.Spark ? 0.05f : 0.16f));
                if (f.Spark) { S.AddStreak(tail, head, size * 0.6f, c, a); continue; }
                S.AddStreak(tail, head, size * 0.8f, c, a * 1.5f);
                if (k < 0.45f) S.AddStreak(tail, head, size * 0.4f, Rgb(255, 235, 170), a * (1 - k / 0.45f));
            }
            // The glowing edge where the paper is burning.
            float t = S.Now;
            for (int i = 0; i < Edge; i++)
            {
                float x0 = i * TW / Edge, x1 = (i + 1) * TW / Edge;
                float pulse = 0.6f + 0.4f * S.Noise(i * 0.4f, t * 3);
                S.AddLine(W(x0, CharLine(x0, t) + 8, 0.6f), W(x1, CharLine(x1, t) + 8, 0.6f), 2.2f, Rgb(255, 120 + 60 * pulse, 30), 0.55f * pulse * Live);
            }
        }

        const int Edge = 48;
        float CharLine(float x, float t) => 448 + 30 * S.Noise(x * 0.025f, t * 0.15f) + 8 * S.Noise(x * 0.11f, 7);

        public override void DrawTranslucent()
        {
            // The foot of the page is burning: charred, ragged, dark -- so the flames glow against it.
            float t = S.Now;
            S.N(Surface.Dir(Vector3.UnitZ));
            S.BeginShape(ShapeKind.Triangles);
            for (int i = 0; i < Edge; i++)
            {
                float x0 = i * TW / Edge, x1 = (i + 1) * TW / Edge;
                float y0 = CharLine(x0, t), y1 = CharLine(x1, t);
                var fade = Rgb(30, 14, 6, 0); var dark = Rgb(22, 10, 4, 235 * Live); var deep = Rgb(12, 6, 2, 250 * Live);
                void P(float x, float y, Vector4 c) { S.FillV(c); S.V(W(x, y, 0.3f)); }
                P(x0, y0, fade); P(x1, y1, fade); P(x1, y1 + 14, dark);
                P(x0, y0, fade); P(x1, y1 + 14, dark); P(x0, y0 + 14, dark);
                P(x0, y0 + 14, dark); P(x1, y1 + 14, dark); P(x1, TH, deep);
                P(x0, y0 + 14, dark); P(x1, TH, deep); P(x0, TH, deep);
            }
            S.EndShape();
        }
    }

    // =====================================================================================
    // Water: Coleridge. The words float on water that ripples under your finger.
    // =====================================================================================
    sealed class WaterPage : Page
    {
        const int GX = 44, GY = 58;
        const float X0 = 14, Y0 = 14, X1 = TW - 14, Y1 = TH - 14, Level = 9;
        float[] h = new float[GX * GY], prev = new float[GX * GY];
        readonly float[] surf = new float[GX * GY];
        readonly List<Vector3> drops = new();
        readonly List<(Vector3 P, Vector3 V, float Age)> splashes = new();
        float time, nextDrop, lastHX = -1, lastHY;
        public WaterPage(BookOfElements s) : base(s) { }

        protected override void Layout() => Poem(""The Rime of the Ancient Mariner"", ""Samuel Taylor Coleridge, 1798"", new[]
        {
            ""Day after day, day after day,"", ""We stuck, nor breath nor motion;"", ""As idle as a painted ship"", ""Upon a painted ocean."",
            """", ""Water, water, every where,"", ""And all the boards did shrink;"", ""Water, water, every where,"", ""Nor any drop to drink."",
        }, 40, 20);

        float Gx(int i) => X0 + i * (X1 - X0) / (GX - 1);
        float Gy(int j) => Y0 + j * (Y1 - Y0) / (GY - 1);
        float Swell(float x, float y) => 2.2f * MathF.Sin(x * 0.03f + time * 1.3f) + 1.5f * MathF.Sin(y * 0.045f - time * 0.9f + x * 0.01f);

        public float Height(float x, float y)
        {
            float fx = Math.Clamp((x - X0) / (X1 - X0) * (GX - 1), 0, GX - 1.001f), fy = Math.Clamp((y - Y0) / (Y1 - Y0) * (GY - 1), 0, GY - 1.001f);
            int i = (int)fx, j = (int)fy; float a = fx - i, b = fy - j;
            float r = Lerp(Lerp(h[j * GX + i], h[j * GX + i + 1], a), Lerp(h[(j + 1) * GX + i], h[(j + 1) * GX + i + 1], a), b);
            return Level + r + Swell(x, y);
        }

        void Disturb(float x, float y, float radius, float amount)
        {
            for (int j = 1; j < GY - 1; j++)
                for (int i = 1; i < GX - 1; i++)
                {
                    float d = MathF.Sqrt((Gx(i) - x) * (Gx(i) - x) + (Gy(j) - y) * (Gy(j) - y));
                    if (d < radius) { float k = 1 - d / radius; h[j * GX + i] += amount * k * k; }
                }
        }

        public override void Open(float t) { Array.Clear(h); Array.Clear(prev); nextDrop = t + 1; }

        public override void Step(float dt, float t, bool open)
        {
            time = t;
            if (!open) return;
            // Ripples: the classic two-buffer wave.
            for (int sub = 0; sub < 2; sub++)
            {
                for (int j = 1; j < GY - 1; j++)
                    for (int i = 1; i < GX - 1; i++)
                    {
                        int k = j * GX + i;
                        prev[k] = ((h[k - 1] + h[k + 1] + h[k - GX] + h[k + GX]) * 0.5f - prev[k]) * 0.985f;
                    }
                (h, prev) = (prev, h);
            }
            if (HoverX >= 0)
            {
                if (lastHX >= 0)
                {
                    float moved = MathF.Sqrt((HoverX - lastHX) * (HoverX - lastHX) + (HoverY - lastHY) * (HoverY - lastHY));
                    if (moved > 0.5f) Disturb(HoverX, HoverY, 18, -Math.Min(2.5f, moved * 0.12f));
                }
                lastHX = HoverX; lastHY = HoverY;
            }
            else lastHX = -1;
            // Now and then a drop falls from the sky.
            if (t > nextDrop) { drops.Add(new Vector3(R(50, TW - 50), R(70, TH - 70), 300)); nextDrop = t + R(0.7f, 2.0f); }
            for (int i = drops.Count - 1; i >= 0; i--)
            {
                var d = drops[i];
                d.Z -= 380 * dt;
                if (d.Z <= Height(d.X, d.Y)) { Splash(d.X, d.Y, 7); drops.RemoveAt(i); continue; }
                drops[i] = d;
            }
            for (int i = splashes.Count - 1; i >= 0; i--)
            {
                var (p, v, age) = splashes[i];
                v.Z -= 320 * dt; p += v * dt; age += dt;
                if (age > 0.9f || p.Z < Level - 3) { splashes.RemoveAt(i); continue; }
                splashes[i] = (p, v, age);
            }
            // The words float: up with the water, tilted by it, pushed down its slopes and back home.
            foreach (var L in Letters)
            {
                float x = L.X + L.Off.X, y = L.Y + L.Off.Y;
                float gx = (Height(x + 3, y) - Height(x - 3, y)) / 6, gy = (Height(x, y + 3) - Height(x, y - 3)) / 6;
                var acc = new Vector2(-gx, -gy) * 340 - new Vector2(L.Off.X, L.Off.Y) * 4.5f - new Vector2(L.Vel.X, L.Vel.Y) * 2.4f;
                L.Vel.X += acc.X * dt; L.Vel.Y += acc.Y * dt;
                L.Off.X += L.Vel.X * dt; L.Off.Y += L.Vel.Y * dt;
                L.Off.Z = Lerp(L.Off.Z, (Height(x, y) + 0.5f) * Live, 0.3f);
                L.Rot = Quaternion.Slerp(L.Rot, FromTo(Vector3.UnitZ, new Vector3(-gx * 1.6f, -gy * 1.6f, 1)), 0.3f);
            }
        }

        void Splash(float x, float y, float strength)
        {
            Disturb(x, y, 14, -strength);
            for (int k = 0; k < 14; k++)
            {
                float a = R(0, TWO_PI);
                splashes.Add((new Vector3(x, y, Height(x, y)), new Vector3(MathF.Cos(a) * R(20, 60), MathF.Sin(a) * R(20, 60), R(60, 140)), 0));
            }
            S.Play(84 + R(-5, 5), amp: 0.025, attack: 0.002, release: 0.25, synth: Synth.Sine);
        }

        public override void Press(float x, float y) => Splash(x, y, 16);
        public override void Drag(float x, float y) => Disturb(x, y, 22, -1.5f);

        public override void DrawTranslucent()
        {
            for (int j = 0; j < GY; j++) for (int i = 0; i < GX; i++) surf[j * GX + i] = Height(Gx(i), Gy(j));
            S.Specular(255); S.Shininess(90);
            S.BeginShape(ShapeKind.Triangles);
            void Pt(int i, int j)
            {
                int k = j * GX + i;
                float gx = (surf[j * GX + Math.Min(GX - 1, i + 1)] - surf[j * GX + Math.Max(0, i - 1)]) / (2 * (X1 - X0) / (GX - 1));
                float gy = (surf[Math.Min(GY - 1, j + 1) * GX + i] - surf[Math.Max(0, j - 1) * GX + i]) / (2 * (Y1 - Y0) / (GY - 1));
                float crest = Clamp01((surf[k] - Level) / 6);
                S.FillV(Vector4.Lerp(Rgb(30, 110, 150, 78), Rgb(140, 205, 230, 120), crest), Live);
                S.N(Vector3.Normalize(Surface.Dir(new Vector3(-gx, -gy, 1))));
                S.V(W(Gx(i), Gy(j), surf[k]));
            }
            for (int j = 0; j < GY - 1; j++)
                for (int i = 0; i < GX - 1; i++)
                {
                    Pt(i, j); Pt(i + 1, j); Pt(i + 1, j + 1);
                    Pt(i, j); Pt(i + 1, j + 1); Pt(i, j + 1);
                }
            S.EndShape();
            S.Specular(0); S.Shininess(0);
        }

        public override void DrawSoft()
        {
            foreach (var d in drops) S.AddSoft(W(d.X, d.Y, d.Z), 5 * K, Rgb(210, 235, 255), 0.9f * Live);
            foreach (var (p, _, age) in splashes) S.AddSoft(W(p.X, p.Y, p.Z), 3.5f * K, Rgb(220, 240, 255), (1 - age / 0.9f) * 0.8f * Live);
        }
    }

    // =====================================================================================
    // Grass: Whitman. Grass grows up through the page and lifts the words on its blades.
    // =====================================================================================
    sealed class GrassPage : Page
    {
        sealed class Blade { public float X, Y, H, Wd, Stiff, Hue, Face; public Vector2 Bend, BendV; public Letter Carries; }
        readonly List<Blade> blades = new();
        readonly List<(Vector3 P, Vector3 V, float Age)> fluff = new();
        float grow, lastHX = -1, lastHY;
        public GrassPage(BookOfElements s) : base(s) { }

        protected override void Layout() => Poem(""Song of Myself"", ""Walt Whitman, 1855"", new[]
        {
            ""A child said What is the grass? fetching it to me with full hands;"",
            ""How could I answer the child? I do not know what it is any more than he."",
            """", ""I guess it must be the flag of my disposition, out of hopeful green stuff woven."",
        }, 34, 20);

        protected override void Init()
        {
            foreach (var L in Letters)
                if (L.Line >= 0) blades.Add(new Blade { X = L.X, Y = L.Y + L.H * 0.25f, H = R(15, 20), Wd = 1.3f, Stiff = 1.4f, Hue = R(0, 1), Face = R(0, 6.28f), Carries = L });
            for (int i = 0; i < 1150; i++)
            {
                bool meadow = i < 750;
                float y = meadow ? R(330, TH - 10) : R(16, TH - 10);
                blades.Add(new Blade { X = R(8, TW - 8), Y = y, H = y > 320 ? R(36, 95) : R(8, 20), Wd = R(2.2f, 3.6f), Stiff = R(0.7f, 1.3f), Hue = R(0, 1), Face = R(0, 6.28f) });
            }
        }

        public override void Open(float t) { grow = 0; foreach (var b in blades) { b.Bend = b.BendV = Vector2.Zero; } }
        public override void Close() { base.Close(); fluff.Clear(); }

        Vector3 At(Blade b, float s)
        {
            float hgt = b.H * grow;
            float z = hgt * s * (1 - 0.2f * Math.Min(1.5f, b.Bend.LengthSquared()) * s);
            return new Vector3(b.X + b.Bend.X * hgt * 0.7f * s * s, b.Y + b.Bend.Y * hgt * 0.7f * s * s, Math.Max(0.2f, z));
        }

        public override void Step(float dt, float t, bool open)
        {
            if (!open) return;
            grow = Math.Min(1, grow + dt / 2.8f);
            bool hover = HoverX >= 0;
            float speed = hover && lastHX >= 0 ? MathF.Sqrt((HoverX - lastHX) * (HoverX - lastHX) + (HoverY - lastHY) * (HoverY - lastHY)) / dt : 0;
            foreach (var b in blades)
            {
                float gust = S.Noise(b.X * 0.004f - t * 0.35f, b.Y * 0.004f);
                float ang = (S.Noise(b.X * 0.002f + 9, t * 0.1f) - 0.5f) * 1.4f;
                var target = new Vector2(MathF.Cos(ang), MathF.Sin(ang)) * (0.05f + 0.5f * gust * gust) / b.Stiff;
                if (hover)
                {
                    var d = new Vector2(b.X - HoverX, b.Y - HoverY);
                    float len = d.Length();
                    if (len < 90 && len > 0.01f) target += d / len * 1.3f * MathF.Pow(1 - len / 90, 1.5f);
                }
                b.BendV += ((target - b.Bend) * 26 - b.BendV * 4.2f) * dt;
                b.Bend += b.BendV * dt;
                if (b.Carries != null)
                {
                    var tip = At(b, 1);
                    var L = b.Carries;
                    L.Off = new Vector3(tip.X - L.X, tip.Y - L.Y, tip.Z);
                    float bl = b.Bend.Length();
                    L.Rot = bl > 0.001f ? Quaternion.CreateFromAxisAngle(Vector3.Normalize(new Vector3(b.Bend.Y, -b.Bend.X, 0)), -bl * 0.45f) : Quaternion.Identity;
                }
            }
            // Brushing the meadow fast sends seeds up.
            if (hover && speed > 260 && HoverY > 300 && Rng.NextDouble() < 0.6)
                fluff.Add((new Vector3(HoverX + R(-20, 20), HoverY + R(-20, 20), R(20, 60)), new Vector3(R(-20, 20), R(-20, 20), R(15, 40)), 0));
            lastHX = hover ? HoverX : -1; lastHY = HoverY;
            for (int i = fluff.Count - 1; i >= 0; i--)
            {
                var (p, v, age) = fluff[i];
                v.X += (S.Noise(p.Y * 0.01f, t * 0.5f) - 0.4f) * 40 * dt; v.Z -= 3 * dt;
                p += v * dt; age += dt;
                if (age > 7) { fluff.RemoveAt(i); continue; }
                fluff[i] = (p, v, age);
            }
        }

        public override void DrawLit()
        {
            if (grow <= 0.01f) return;
            S.BeginShape(ShapeKind.Triangles);
            foreach (var b in blades)
            {
                float bl = b.Bend.Length();
                var side2 = bl > 0.05f ? new Vector2(-b.Bend.Y, b.Bend.X) / bl : new Vector2(MathF.Cos(b.Face), MathF.Sin(b.Face));
                var side = new Vector3(side2.X, side2.Y, 0);
                var root = b.Carries != null ? Rgb(40, 80, 30) : Rgb(26 + 14 * b.Hue, 60 + 24 * b.Hue, 20);
                var tipC = b.Carries != null ? Rgb(90, 140, 60) : Rgb(108 + 50 * b.Hue, 168 + 34 * b.Hue, 58);
                Vector3 prevL = default, prevR = default; Vector4 prevC = default;
                for (int s = 0; s <= 4; s++)
                {
                    float k = s / 4f;
                    var p = At(b, k);
                    float w = b.Wd * (1 - k * 0.92f);
                    var l = p - side * w; var r = p + side * w;
                    var c = Vector4.Lerp(root, tipC, k);
                    if (s > 0)
                    {
                        var tangent = p - At(b, k - 0.25f);
                        var n = Vector3.Cross(side, tangent);
                        if (n.LengthSquared() < 1e-8f) n = Vector3.UnitZ;
                        n = Vector3.Normalize(n); if (n.Z < 0) n = -n;
                        var nw = Surface.Dir(Vector3.Normalize(n + Vector3.UnitZ * 0.6f));
                        S.N(nw);
                        S.FillV(prevC, Live); S.V(W(prevL.X, prevL.Y, prevL.Z)); S.V(W(prevR.X, prevR.Y, prevR.Z));
                        S.FillV(c, Live); S.V(W(r.X, r.Y, r.Z));
                        S.FillV(prevC, Live); S.V(W(prevL.X, prevL.Y, prevL.Z));
                        S.FillV(c, Live); S.V(W(r.X, r.Y, r.Z)); S.V(W(l.X, l.Y, l.Z));
                    }
                    prevL = l; prevR = r; prevC = c;
                }
            }
            S.EndShape();
        }

        public override void DrawSoft()
        {
            foreach (var (p, _, age) in fluff) S.AddSoft(W(p.X, p.Y, p.Z), 6 * K, Rgb(255, 255, 245), Smooth(7, 5, age) * 0.85f * Live);
        }
    }

    // =====================================================================================
    // Glass: Carroll. A glass ball rolls over the words, bends them, turns them backwards.
    // =====================================================================================
    sealed class GlassPage : Page
    {
        const float Rb = 64;
        float bx = TW / 2, by = TH * 0.55f, vx, vy, ballScale = 1, shatterT = -10;
        sealed class Shard { public Vector3 P, V, Spin, A, B, C; public Quaternion Q = Quaternion.Identity; public float Age; }
        readonly List<Shard> shards = new();
        public GlassPage(BookOfElements s) : base(s) { }

        protected override void Layout() => Poem(""Through the Looking-Glass"", ""Lewis Carroll, 1871"", new[]
        {
            ""“Let’s pretend the glass has got all soft like gauze, so that we can get through. Why, it’s turning into a sort of mist now, I declare! It’ll be easy enough to get through—”"",
            """",
            ""She was up on the chimney-piece while she said this, though she hardly knew how she had got there. And certainly the glass was beginning to melt away, just like a bright silvery mist."",
        }, 34, 18, prose: true);

        public override void Close() { base.Close(); shards.Clear(); ballScale = 1; }

        public override void Step(float dt, float t, bool open)
        {
            if (!open) return;
            float tx = HoverX >= 0 ? HoverX : TW / 2 + MathF.Sin(t * 0.37f) * 120, ty = HoverX >= 0 ? HoverY : TH * 0.55f + MathF.Sin(t * 0.23f) * 170;
            vx += ((tx - bx) * 7 - vx * 3.6f) * dt; vy += ((ty - by) * 7 - vy * 3.6f) * dt;
            bx += vx * dt; by += vy * dt;
            if (t - shatterT > 1.8f) ballScale = Math.Min(1, ballScale + dt / 0.9f);
            float rad = Rb * ballScale;
            foreach (var L in Letters)
            {
                float dx = L.X - bx, dy = L.Y - by, d = MathF.Sqrt(dx * dx + dy * dy);
                var off = Vector3.Zero; var rot = Quaternion.Identity; float mirror = 0, scale = 1;
                float hold = rad > 2 ? Smooth(rad * 1.25f, rad * 0.7f, d) : 0;
                if (hold > 0)
                {
                    // Lifted onto the ball, magnified like under a lens, tilted with its surface;
                    // near the middle it turns over -- through the looking-glass, backwards.
                    float nd = Math.Min(rad * 0.97f, d * (1 + 0.4f * (1 - d / (rad * 1.25f))));
                    var dir = d > 0.01f ? new Vector2(dx, dy) / d : Vector2.Zero;
                    float top = MathF.Sqrt(Math.Max(0, rad * rad - nd * nd));
                    var on = new Vector3(bx + dir.X * nd - L.X, by + dir.Y * nd - L.Y, rad + top - 3);
                    off = on * hold;
                    rot = Quaternion.Slerp(Quaternion.Identity, FromTo(Vector3.UnitZ, new Vector3(dir.X * nd, dir.Y * nd, top + 0.01f)), hold);
                    mirror = Smooth(rad * 0.55f, rad * 0.25f, nd) * hold;
                    scale = 1 + 0.4f * hold * (1 - nd / rad);
                }
                L.Off = Vector3.Lerp(L.Off, off, 0.25f);
                L.Rot = Quaternion.Slerp(L.Rot, rot, 0.25f);
                L.Mirror = Lerp(L.Mirror, mirror, 0.18f);
                L.Scale = Lerp(L.Scale, scale, 0.2f);
            }
            for (int i = shards.Count - 1; i >= 0; i--)
            {
                var s = shards[i];
                s.Age += dt;
                if (s.Age > 3.4f) { shards.RemoveAt(i); continue; }
                s.V.Z -= 330 * dt;
                s.P += s.V * dt;
                if (s.P.Z < 1.5f) { s.P.Z = 1.5f; s.V.Z = -s.V.Z * 0.35f; s.V.X *= 0.7f; s.V.Y *= 0.7f; s.Spin *= 0.55f; }
                if (s.Spin.LengthSquared() > 1e-4f) s.Q = Quaternion.Normalize(s.Q * Quaternion.CreateFromAxisAngle(Vector3.Normalize(s.Spin), s.Spin.Length() * dt));
            }
        }

        public override void Press(float x, float y)
        {
            float rad = Rb * ballScale;
            if (rad < 10 || (x - bx) * (x - bx) + (y - by) * (y - by) > rad * rad) return;
            for (int i = 0; i < 120; i++)
            {
                var n = Vector3.Normalize(new Vector3(R(-1, 1), R(-1, 1), R(-0.3f, 1)));
                float sz = R(5, 13);
                Vector3 Corner() => Vector3.Normalize(new Vector3(R(-1, 1), R(-1, 1), R(-1, 1))) * sz;
                shards.Add(new Shard
                {
                    P = new Vector3(bx, by, Rb) + n * rad, V = n * R(90, 220) + new Vector3(0, 0, R(40, 130)),
                    Spin = Vector3.Normalize(new Vector3(R(-1, 1), R(-1, 1), R(-1, 1))) * R(4, 14),
                    A = Corner(), B = Corner(), C = Corner(),
                });
            }
            ballScale = 0; shatterT = S.Now;
            for (int k = 0; k < 6; k++) S.Play(96 + k * 2 + R(-1, 1), amp: 0.05, attack: 0.001, release: 0.9, synth: Synth.Sine);
        }

        public override void DrawTranslucent()
        {
            float rad = Rb * ballScale;
            if (rad > 1)
            {
                S.Decal(Surface, bx + 20, by + 26, 0.3f, rad * 1.05f, Rgb(40, 28, 18, 80 * Live));
                S.Hint(""DISABLE_DEPTH_MASK"");
                S.Specular(255); S.Shininess(120);
                S.PushMatrix();
                var c = W(bx, by, rad);
                S.Translate(c.X, -c.Y, c.Z);
                S.Fill(150, 185, 220, 34 * Live);
                S.SphereDetail(48);
                S.Sphere(rad * K);
                S.PopMatrix();
                S.Hint(""ENABLE_DEPTH_MASK"");
            }
            if (shards.Count > 0)
            {
                S.Specular(255); S.Shininess(140);
                S.BeginShape(ShapeKind.Triangles);
                foreach (var s in shards)
                {
                    float fade = Smooth(3.4f, 2.4f, s.Age) * Live;
                    var a = s.P + Vector3.Transform(s.A, s.Q); var b = s.P + Vector3.Transform(s.B, s.Q); var cc = s.P + Vector3.Transform(s.C, s.Q);
                    var n = Vector3.Cross(b - a, cc - a);
                    if (n.LengthSquared() < 1e-6f) continue;
                    n = Vector3.Normalize(n); if (n.Z < 0) n = -n;
                    S.N(Surface.Dir(n));
                    S.Fill(215, 235, 255, 150 * fade);
                    S.V(W(a.X, a.Y, a.Z)); S.V(W(b.X, b.Y, b.Z)); S.V(W(cc.X, cc.Y, cc.Z));
                }
                S.EndShape();
            }
            S.Specular(0); S.Shininess(0);
        }

        public override void DrawGlow()
        {
            float rad = Rb * ballScale;
            if (rad < 1) return;
            // The light gathered under the ball, and a gleam on it.
            S.Decal(Surface, bx - 12, by - 14, 0.5f, rad * 0.5f, Rgb(255, 248, 225, 120 * Live));
            S.AddGlow(W(bx - rad * 0.35f, by - rad * 0.4f, rad * 1.75f), rad * 0.5f * K, Rgb(255, 255, 255), 0.4f * Live);
        }
    }

    // =====================================================================================
    // Snow: Longfellow. Snow falls through the room and settles on the words until they shake it off.
    // =====================================================================================
    sealed class SnowPage : Page
    {
        const int GX = 42, GY = 56;
        readonly float[] depth = new float[GX * GY], tmp = new float[GX * GY];
        struct Flake { public Vector3 P; public float Size, Speed, Phase; }
        Flake[] flakes;
        readonly List<(Vector3 P, Vector3 V, float Age)> puffs = new();
        public SnowPage(BookOfElements s) : base(s) { }

        protected override void Layout() => Poem(""Snow-flakes"", ""Henry Wadsworth Longfellow, 1863"", new[]
        {
            ""Out of the bosom of the Air,"", ""  Out of the cloud-folds of her garments shaken,"", ""Over the woodlands brown and bare,"", ""  Over the harvest-fields forsaken,"",
            ""    Silent, and soft, and slow"", ""    Descends the snow."",
        }, 50, 20);

        protected override void Init()
        {
            flakes = new Flake[1300];
            for (int i = 0; i < flakes.Length; i++) flakes[i] = NewFlake(R(0, 340));
        }

        Flake NewFlake(float z) => new() { P = new Vector3(R(-30, TW + 30), R(-30, TH + 30), z), Size = R(2.5f, 5.5f), Speed = R(16, 34), Phase = R(0, 6.28f) };

        int Cell(float x, float y) => Math.Clamp((int)(y / TH * (GY - 1) + 0.5f), 0, GY - 1) * GX + Math.Clamp((int)(x / TW * (GX - 1) + 0.5f), 0, GX - 1);

        float DepthAt(float x, float y)
        {
            float fx = Math.Clamp(x / TW * (GX - 1), 0, GX - 1.001f), fy = Math.Clamp(y / TH * (GY - 1), 0, GY - 1.001f);
            int i = (int)fx, j = (int)fy; float a = fx - i, b = fy - j;
            return Lerp(Lerp(depth[j * GX + i], depth[j * GX + i + 1], a), Lerp(depth[(j + 1) * GX + i], depth[(j + 1) * GX + i + 1], a), b);
        }

        void Clear(float x, float y, float radius, float keep, int puff)
        {
            for (int j = 0; j < GY; j++)
                for (int i = 0; i < GX; i++)
                {
                    float px = i * TW / (GX - 1), py = j * TH / (GY - 1), d = MathF.Sqrt((px - x) * (px - x) + (py - y) * (py - y));
                    if (d < radius) depth[j * GX + i] *= Lerp(keep, 1, d / radius);
                }
            for (int k = 0; k < puff && puffs.Count < 600; k++)
                puffs.Add((new Vector3(x + R(-radius, radius) * 0.6f, y + R(-radius, radius) * 0.6f, DepthAt(x, y) + 2), new Vector3(R(-40, 40), R(-40, 40), R(40, 110)), 0));
        }

        public override void Open(float t) => Array.Clear(depth);
        public override void Close() { base.Close(); puffs.Clear(); }

        public override void Step(float dt, float t, bool open)
        {
            if (!open) return;
            float windX = 10 * MathF.Sin(t * 0.21f);
            for (int i = 0; i < flakes.Length; i++)
            {
                ref var f = ref flakes[i];
                f.P.Z -= f.Speed * dt;
                f.P.X += (MathF.Sin(t * 0.7f + f.Phase) * 9 + windX) * dt;
                f.P.Y += MathF.Cos(t * 0.5f + f.Phase * 1.3f) * 6 * dt;
                bool inside = f.P.X > 0 && f.P.X < TW && f.P.Y > 0 && f.P.Y < TH;
                float ground = inside ? DepthAt(f.P.X, f.P.Y) : -45;
                if (f.P.Z <= ground)
                {
                    if (inside) depth[Cell(f.P.X, f.P.Y)] += 1.6f * f.Size / 4;
                    f = NewFlake(R(300, 340));
                }
            }
            // The snowfall settles everywhere, a little more in drifts.
            for (int j = 0; j < GY; j++)
                for (int i = 0; i < GX; i++)
                    depth[j * GX + i] += dt * (0.08f + 0.9f * Smooth(0.55f, 0.8f, S.Noise(i * 0.13f + 3, j * 0.13f)) * 0.5f + 0.9f * Smooth(0.7f, 1, j / (float)(GY - 1)));
            // Settle (spread a little) and melt a little.
            for (int j = 1; j < GY - 1; j++)
                for (int i = 1; i < GX - 1; i++)
                {
                    int k = j * GX + i;
                    tmp[k] = depth[k] * 0.9f + (depth[k - 1] + depth[k + 1] + depth[k - GX] + depth[k + GX]) * 0.025f;
                }
            for (int k = 0; k < depth.Length; k++) depth[k] = Math.Min(16, tmp[k] * (1 - 0.006f * dt));
            // The words shake the snow off when it gets heavy.
            foreach (var L in Letters)
            {
                L.T += dt;
                // Snow settles on each letter as a white cap.
                if (L.State != 1) L.A = Math.Min(1.2f, L.A + dt * (0.08f + 0.1f * L.Seed));
                if (L.State == 0 && (L.A >= 1.2f || DepthAt(L.X, L.Y) > 6f)) { L.State = 1; L.T = 0; }
                if (L.State == 1)
                {
                    L.Off = new Vector3(MathF.Sin(L.T * 70) * 1.6f, 0, 1.5f + MathF.Abs(MathF.Sin(L.T * 35)) * 2);
                    if (L.T > 0.6f) { Clear(L.X, L.Y, 14, 0.1f, 8); L.A = 0; L.State = 2; L.T = 0; L.Off = Vector3.Zero; }
                }
                else if (L.State == 2 && L.T > 1.2f) L.State = 0;
            }
            for (int i = puffs.Count - 1; i >= 0; i--)
            {
                var (p, v, age) = puffs[i];
                v.Z -= 70 * dt; v *= 1 - 1.5f * dt; p += v * dt; age += dt;
                if (age > 2.5f || p.Z < 0) { puffs.RemoveAt(i); continue; }
                puffs[i] = (p, v, age);
            }
        }

        public override void Drag(float x, float y) { Clear(x, y, 28, 0.15f, 3); Brush(x, y, 30); }
        public override void Press(float x, float y) { Clear(x, y, 34, 0.05f, 40); Brush(x, y, 36); }
        void Brush(float x, float y, float r) { foreach (var L in Letters) if ((L.X - x) * (L.X - x) + (L.Y - y) * (L.Y - y) < r * r) L.A = 0; }

        public override void DrawTranslucent()
        {
            S.Specular(140); S.Shininess(30);
            S.BeginShape(ShapeKind.Triangles);
            void Pt(int i, int j)
            {
                int k = j * GX + i;
                float d = depth[k];
                float gx = (depth[j * GX + Math.Min(GX - 1, i + 1)] - depth[j * GX + Math.Max(0, i - 1)]) / (2 * TW / (GX - 1));
                float gy = (depth[Math.Min(GY - 1, j + 1) * GX + i] - depth[Math.Max(0, j - 1) * GX + i]) / (2 * TH / (GY - 1));
                S.Fill(246, 249, 255, 252 * Smooth(0.9f, 1.7f, d) * Live);
                S.N(Vector3.Normalize(Surface.Dir(new Vector3(-gx * 5, -gy * 5, 1))));
                S.V(W(i * TW / (GX - 1), j * TH / (GY - 1), 0.5f + d * 0.9f));
            }
            for (int j = 0; j < GY - 1; j++)
                for (int i = 0; i < GX - 1; i++)
                {
                    int k = j * GX + i;
                    if (depth[k] < 0.6f && depth[k + 1] < 0.6f && depth[k + GX] < 0.6f && depth[k + GX + 1] < 0.6f) continue;
                    Pt(i, j); Pt(i + 1, j); Pt(i + 1, j + 1);
                    Pt(i, j); Pt(i + 1, j + 1); Pt(i, j + 1);
                }
            S.EndShape();
            // The caps: each letter again, in snow, sitting on its top edge.
            S.BeginShape(ShapeKind.Quads);
            S.Texture(S.atlas);
            foreach (var L in Letters)
            {
                float cap = Math.Min(1, L.A);
                if (cap < 0.03f) continue;
                var g = S.glyphs[L.Glyph];
                // Drawn just under the ink and nudged up: only what pokes out above the letter shows.
                S.EmitQuad(Surface, L, Rgb(250, 252, 255, 250 * Math.Min(1, cap * 1.5f) * Live), L.W * 1.1f, L.H * 1.08f, 0.2f, g.U0, g.V0, g.U1, g.V1, new Vector3(0, -2.6f * cap, 0));
            }
            S.EndShape();
            S.Specular(0); S.Shininess(0);
        }

        public override void DrawSoft()
        {
            foreach (var f in flakes) S.AddSoft(W(f.P.X, f.P.Y, f.P.Z), f.Size * K, Rgb(255, 255, 255), 0.9f * Live);
            foreach (var (p, _, age) in puffs) S.AddSoft(W(p.X, p.Y, p.Z), 5 * K, Rgb(255, 255, 255), (1 - age / 2.5f) * 0.9f * Live);
        }
    }

    // =====================================================================================
    // Wind: Shelley. The west wind blows the words away as autumn leaves; they flutter home.
    // =====================================================================================
    sealed class WindPage : Page
    {
        // ""Yellow, and black, and pale, and hectic red""
        static readonly Vector4[] Autumn = { Rgb(232, 186, 60), Rgb(48, 38, 32), Rgb(228, 216, 188), Rgb(196, 46, 30), Rgb(212, 116, 36) };
        float gustX = -9999, gustStrength, nextGust; int gustId;
        Vector2 gustDir = new(1, 0);
        float lastDX = -1, lastDY;
        sealed class Leaf { public Vector3 P, V, Spin; public Quaternion Q = Quaternion.Identity; public Vector4 C; public float Size; }
        readonly List<Leaf> loose = new();
        public WindPage(BookOfElements s) : base(s) { }

        protected override void Layout() => Poem(""Ode to the West Wind"", ""Percy Bysshe Shelley, 1820"", new[]
        {
            ""O wild West Wind, thou breath of Autumn’s being,"", ""Thou, from whose unseen presence the leaves dead"", ""Are driven, like ghosts from an enchanter fleeing,"",
            """", ""Yellow, and black, and pale, and hectic red,"", ""Pestilence-stricken multitudes: O thou,"", ""Who chariotest to their dark wintry bed"", ""The winged seeds, where they lie cold and low,"",
        }, 40, 19);

        protected override void Init()
        {
            foreach (var L in Letters) L.A = Rng.Next(Autumn.Length);
            for (int i = 0; i < 18; i++)
                loose.Add(new Leaf { P = new Vector3(R(-300, 700), R(-100, 650), R(30, 200)), C = Autumn[new[] { 0, 3, 4 }[Rng.Next(3)]], Size = R(30, 44), Spin = Vector3.Normalize(new Vector3(R(-1, 1), R(-1, 1), R(-1, 1))) * R(2, 6) });
        }

        public override void Open(float t) => nextGust = t + 1.5f;

        void StartGust(Vector2 dir, float strength)
        {
            gustDir = Vector2.Normalize(dir);
            float min = float.MaxValue;
            foreach (var c in new[] { new Vector2(0, 0), new Vector2(TW, 0), new Vector2(0, TH), new Vector2(TW, TH) }) min = Math.Min(min, Vector2.Dot(c, gustDir));
            gustX = min - 40; gustStrength = strength; gustId++;
            S.Play(50, amp: 0.09, attack: 0.4, release: 1.6, cutoff: 75, synth: Synth.Noise);
        }

        Vector3 Flow(Vector3 p, float t)
        {
            float a = S.Noise(p.X * 0.004f + t * 0.1f, p.Y * 0.004f) * TWO_PI * 2;
            return new Vector3(gustDir.X * 80 + MathF.Cos(a) * 60, gustDir.Y * 80 + MathF.Sin(a) * 60, 30 * MathF.Sin(a * 1.3f + t) + 8);
        }

        public override void Step(float dt, float t, bool open)
        {
            if (!open) return;
            if (t > nextGust) { StartGust(new Vector2(1, R(-0.35f, 0.35f)), R(0.5f, 0.9f)); nextGust = t + R(6.5f, 9.5f); }
            gustX += 330 * dt;
            foreach (var L in Letters)
            {
                L.T += dt;
                switch (L.State)
                {
                    case 0:
                        if ((int)L.B != gustId && Vector2.Dot(new Vector2(L.X, L.Y), gustDir) < gustX)
                        {
                            L.B = gustId;
                            if (Rng.NextDouble() < gustStrength)
                            {
                                L.State = 1; L.T = 0;
                                L.Vel = new Vector3(gustDir.X * R(80, 170), gustDir.Y * R(80, 170) + R(-30, 30), R(70, 150));
                                L.Spin = Vector3.Normalize(new Vector3(R(-1, 1), R(-1, 1), R(-1, 1))) * R(3, 8);
                            }
                        }
                        break;
                    case 1: // flying with the wind
                    {
                        var p = new Vector3(L.X, L.Y, 0) + L.Off;
                        L.Vel += (Flow(p, t) - L.Vel) * 1.4f * dt;
                        L.Vel.Z -= 16 * dt;
                        L.Off += L.Vel * dt;
                        if (L.Off.Z < 3) { L.Off.Z = 3; L.Vel.Z = Math.Abs(L.Vel.Z) * 0.5f; }
                        L.Rot = Quaternion.Normalize(L.Rot * Quaternion.CreateFromAxisAngle(Vector3.Normalize(L.Spin), L.Spin.Length() * dt));
                        if (L.T > 2.8f + L.Seed * 3) { L.State = 2; L.T = 0; }
                        break;
                    }
                    case 2: // finding the way home
                    {
                        var desired = new Vector3(0, 0, 26) - L.Off;
                        L.Vel += (desired * 2.4f - L.Vel * 1.8f) * dt;
                        L.Off += L.Vel * dt;
                        L.Rot = Quaternion.Slerp(L.Rot, Quaternion.Identity, 2 * dt);
                        if (new Vector2(L.Off.X, L.Off.Y).Length() < 14 && Math.Abs(L.Off.Z - 26) < 14) { L.State = 3; L.T = 0; L.Vel = L.Off; }
                        break;
                    }
                    case 3: // fluttering down, rocking like a leaf
                    {
                        float e = Clamp01(L.T / 1.4f), sway = 1 - e;
                        L.Off = new Vector3(L.Vel.X * sway + MathF.Sin(L.T * 6) * 7 * sway, L.Vel.Y * sway, L.Vel.Z * sway * sway);
                        L.Rot = Quaternion.CreateFromAxisAngle(Vector3.UnitY, 0.55f * MathF.Sin(L.T * 6) * sway) * Quaternion.CreateFromAxisAngle(Vector3.UnitX, 0.3f * MathF.Cos(L.T * 5) * sway);
                        if (e >= 1) { float b = L.B; L.Rest(); L.B = b; }
                        break;
                    }
                }
                bool dark = (int)L.A == 1 || (int)L.A == 3;
                L.Paint = L.State > 0 && dark && L.Off.Z > 4 ? Rgb(244, 236, 216) : null;
            }
            foreach (var leaf in loose)
            {
                leaf.V += (Flow(leaf.P, t) * 1.2f - leaf.V) * 1.2f * dt;
                leaf.V.Z -= 10 * dt;
                leaf.P += leaf.V * dt;
                if (leaf.P.Z < 20) leaf.V.Z += 60 * dt;
                if (leaf.P.Z > 260) leaf.V.Z -= 40 * dt;
                if (leaf.P.X > 900) leaf.P.X = -500; if (leaf.P.X < -520) leaf.P.X = 880;
                if (leaf.P.Y > 800) leaf.P.Y = -200; if (leaf.P.Y < -220) leaf.P.Y = 780;
                leaf.Q = Quaternion.Normalize(leaf.Q * Quaternion.CreateFromAxisAngle(Vector3.Normalize(leaf.Spin), leaf.Spin.Length() * dt));
            }
        }

        public override void Drag(float x, float y)
        {
            if (lastDX >= 0)
            {
                var d = new Vector2(x - lastDX, y - lastDY);
                if (d.Length() > 6 && S.Now - lastGustByHand > 0.8f)
                {
                    StartGust(d, 1);
                    gustX = Vector2.Dot(new Vector2(x, y), gustDir) - 30;
                    lastGustByHand = S.Now;
                }
            }
            lastDX = x; lastDY = y;
        }
        float lastGustByHand;
        public override void Press(float x, float y) { lastDX = x; lastDY = y; }

        public override void DrawLit()
        {
            S.BeginShape(ShapeKind.Quads);
            S.Texture(S.leafTex);
            foreach (var L in Letters)
            {
                float vis = Smooth(1, 10, L.Off.Z) * (L.State > 0 ? 1 : 0);
                if (vis < 0.02f) continue;
                var c = Autumn[(int)L.A];
                S.EmitQuad(Surface, L, new Vector4(c.X, c.Y, c.Z, 255 * vis * Live), L.W * 1.9f, L.H * 1.25f, 0.1f, 0, 0, 1, 1);
            }
            foreach (var leaf in loose)
            {
                var r = Vector3.Transform(Vector3.UnitX, leaf.Q) * leaf.Size * 0.5f; var d = Vector3.Transform(Vector3.UnitY, leaf.Q) * leaf.Size * 0.7f;
                var n = Vector3.Transform(Vector3.UnitZ, leaf.Q);
                S.FillV(leaf.C, Live);
                S.N(Surface.Dir(n));
                Vector3 P(Vector3 q) => W(q.X, q.Y, q.Z);
                S.VT(P(leaf.P - r - d), 0, 0); S.VT(P(leaf.P + r - d), 1, 0); S.VT(P(leaf.P + r + d), 1, 1); S.VT(P(leaf.P - r + d), 0, 1);
            }
            S.EndShape();
        }
    }

    // =====================================================================================
    // Ice: Frost, Fire and Ice. Crystals grow out of the letters; the mouse is warm.
    // =====================================================================================
    sealed class IcePage : Page
    {
        sealed class Crystal { public float X, Y, Len, Max, Rad, Rate, Delay; public Vector3 Axis; public Letter Of; }
        readonly List<Crystal> crystals = new();
        const int GX = 30, GY = 40;
        readonly float[] frost = new float[GX * GY];
        readonly List<(Vector3 P, float Age, float Size)> drips = new();
        readonly List<(Vector3 P, Vector3 V, float Age)> flames = new();
        float opened;
        static readonly Vector4 Icy = Rgb(96, 150, 210);
        public IcePage(BookOfElements s) : base(s) { }

        protected override void Layout() => Poem(""Fire and Ice"", ""Robert Frost, 1920"", new[]
        {
            ""Some say the world will end in fire,"", ""Some say in ice."", ""From what I’ve tasted of desire"", ""I hold with those who favor fire."",
            ""But if it had to perish twice,"", ""I think I know enough of hate"", ""To say that for destruction ice"", ""Is also great"", ""And would suffice."",
        }, 50, 21);

        protected override void Init()
        {
            foreach (var L in Letters)
            {
                if (L.Line < 0 || L.WordText == ""fire"") continue;
                bool ice = L.WordText == ""ice"";
                int n = ice ? 4 : Rng.NextDouble() < 0.85 ? 1 + (Rng.NextDouble() < 0.3 ? 1 : 0) : 0;
                for (int k = 0; k < n; k++)
                {
                    float tilt = R(0.25f, ice ? 1.1f : 0.95f), az = R(0, TWO_PI);
                    float max = ice ? R(40, 72) : R(14, 34);
                    crystals.Add(new Crystal
                    {
                        X = L.X + R(-L.W * 0.25f, L.W * 0.25f), Y = L.Y + R(-L.H * 0.2f, L.H * 0.2f),
                        Axis = Vector3.Normalize(new Vector3(MathF.Sin(tilt) * MathF.Cos(az), MathF.Sin(tilt) * MathF.Sin(az), MathF.Cos(tilt))),
                        Max = max, Rad = max * R(0.1f, 0.15f), Rate = R(5, 11), Delay = R(0, 3), Of = L,
                    });
                }
            }
        }

        public override void Open(float t)
        {
            opened = t;
            foreach (var c in crystals) c.Len = 0;
            Array.Clear(frost);
        }
        public override void Close() { base.Close(); drips.Clear(); flames.Clear(); }

        float Heat(float x, float y) => HoverX < 0 ? 0 : Smooth(80, 0, MathF.Sqrt((x - HoverX) * (x - HoverX) + (y - HoverY) * (y - HoverY)));

        public override void Step(float dt, float t, bool open)
        {
            if (!open) return;
            float since = t - opened;
            foreach (var c in crystals)
            {
                float heat = Heat(c.X, c.Y);
                if (heat > 0.05f)
                {
                    float before = c.Len;
                    c.Len = Math.Max(0, c.Len - 40 * heat * dt);
                    if (before > 2 && Rng.NextDouble() < 0.12 * heat)
                    {
                        var tip = new Vector3(c.X, c.Y, 0) + c.Axis * c.Len;
                        drips.Add((new Vector3(tip.X, tip.Y, 0.8f), 0, R(3, 5)));
                    }
                }
                else if (since > c.Delay) c.Len = Math.Min(c.Max, c.Len + c.Rate * dt);
            }
            foreach (var L in Letters)
            {
                if (L.Line < 0) continue;
                if (L.WordText == ""fire"")
                {
                    L.Glow = 0.55f + 0.25f * S.Noise(t * 5, L.Seed * 20); L.GlowColor = Rgb(255, 150, 60);
                    L.Paint = Rgb(150, 40, 10);
                    if (Rng.NextDouble() < 0.25) flames.Add((new Vector3(L.X + R(-4, 4), L.Y + R(-6, 4), 1), new Vector3(R(-4, 4), R(-4, 4), R(25, 45)), 0));
                    continue;
                }
                L.A = Clamp01(L.A + dt * 0.12f - Heat(L.X, L.Y) * dt * 1.5f);
                L.Paint = Vector4.Lerp(InkColor, Icy, L.A);
                float shiver = L.A > 0.6f ? 0.25f * MathF.Sin(t * 40 + L.Seed * 30) : 0;
                L.Off = new Vector3(shiver, 0, 0);
            }
            for (int j = 0; j < GY; j++)
                for (int i = 0; i < GX; i++)
                {
                    float x = i * TW / (GX - 1), y = j * TH / (GY - 1);
                    int k = j * GX + i;
                    float pattern = 0.35f + 0.65f * S.Noise(x * 0.012f, y * 0.012f);
                    frost[k] = Clamp01(frost[k] + dt * 0.035f * pattern - Heat(x, y) * dt * 2.5f);
                }
            for (int i = drips.Count - 1; i >= 0; i--)
            {
                var (p, age, size) = drips[i];
                p.Y += (30 + age * 30) * dt; age += dt;
                if (age > 2.5f || p.Y > TH - 10) { drips.RemoveAt(i); continue; }
                drips[i] = (p, age, size);
            }
            for (int i = flames.Count - 1; i >= 0; i--)
            {
                var (p, v, age) = flames[i];
                p += v * dt; age += dt;
                if (age > 0.8f) { flames.RemoveAt(i); continue; }
                flames[i] = (p, v, age);
            }
        }

        public override void DrawTranslucent()
        {
            // Frost on the paper.
            S.BeginShape(ShapeKind.Triangles);
            S.Texture(S.frostTex);
            S.N(Surface.Dir(Vector3.UnitZ));
            void Fp(int i, int j)
            {
                S.Fill(255, 255, 255, 230 * frost[j * GX + i] * Live);
                float u = i / (float)(GX - 1), v = j / (float)(GY - 1);
                S.VT(W(u * TW, v * TH, 0.7f), u, v);
            }
            for (int j = 0; j < GY - 1; j++)
                for (int i = 0; i < GX - 1; i++) { Fp(i, j); Fp(i + 1, j); Fp(i + 1, j + 1); Fp(i, j); Fp(i + 1, j + 1); Fp(i, j + 1); }
            S.EndShape();

            // The crystals: tapering hexagonal prisms.
            S.Specular(255); S.Shininess(90);
            S.BeginShape(ShapeKind.Triangles);
            foreach (var c in crystals)
            {
                if (c.Len < 0.5f) continue;
                var a = c.Axis;
                var e1 = Vector3.Normalize(Vector3.Cross(a, Math.Abs(a.Z) < 0.9f ? Vector3.UnitZ : Vector3.UnitX));
                var e2 = Vector3.Cross(a, e1);
                float r = c.Rad * MathF.Sqrt(c.Len / c.Max);
                var b0 = new Vector3(c.X, c.Y, 0);
                var mid = b0 + a * c.Len * 0.72f; var tip = b0 + a * c.Len;
                S.Fill(215, 238, 255, 190 * Live);
                for (int k = 0; k < 6; k++)
                {
                    float a0 = k * MathF.PI / 3, a1 = (k + 1) * MathF.PI / 3;
                    var d0 = e1 * MathF.Cos(a0) + e2 * MathF.Sin(a0); var d1 = e1 * MathF.Cos(a1) + e2 * MathF.Sin(a1);
                    var p0 = b0 + d0 * r; var p1 = b0 + d1 * r; var q0 = mid + d0 * r * 0.85f; var q1 = mid + d1 * r * 0.85f;
                    var n = Vector3.Normalize(d0 + d1);
                    S.N(Surface.Dir(n));
                    S.V(W(p0.X, p0.Y, p0.Z)); S.V(W(p1.X, p1.Y, p1.Z)); S.V(W(q1.X, q1.Y, q1.Z));
                    S.V(W(p0.X, p0.Y, p0.Z)); S.V(W(q1.X, q1.Y, q1.Z)); S.V(W(q0.X, q0.Y, q0.Z));
                    var nt = Vector3.Normalize(Vector3.Cross(q1 - q0, tip - q0));
                    if (Vector3.Dot(nt, n) < 0) nt = -nt;
                    S.N(Surface.Dir(nt));
                    S.V(W(q0.X, q0.Y, q0.Z)); S.V(W(q1.X, q1.Y, q1.Z)); S.V(W(tip.X, tip.Y, tip.Z));
                }
            }
            S.EndShape();
            S.Specular(0); S.Shininess(0);
        }

        public override void DrawGlow()
        {
            foreach (var (p, v, age) in flames) S.AddGlow(W(p.X, p.Y, p.Z), (6 + 8 * MathF.Sin(MathF.PI * age / 0.8f)) * K, FirePage_Color(age / 0.8f), 0.5f * (1 - age / 0.8f) * Live);
            foreach (var (p, age, size) in drips) S.AddGlow(W(p.X, p.Y, p.Z), size * K, Rgb(150, 200, 255), 0.5f * (1 - age / 2.5f) * Live);
            // Glints at the crystals' tips.
            float t = S.Now;
            for (int i = 0; i < crystals.Count; i++)
            {
                var c = crystals[i];
                if (c.Len < 3) continue;
                float tw = MathF.Pow(Math.Max(0, MathF.Sin(t * 2.3f + i * 1.7f)), 12);
                if (tw < 0.02f) continue;
                var tip = new Vector3(c.X, c.Y, 0) + c.Axis * c.Len;
                S.AddGlow(W(tip.X, tip.Y, tip.Z), 9 * K, Rgb(220, 240, 255), tw * Live);
            }
        }

        static Vector4 FirePage_Color(float k) => Vector4.Lerp(Rgb(255, 220, 120), Rgb(230, 70, 20), k);
    }

    // =====================================================================================
    // Stars: Teasdale. At night the words rise off the page and become constellations.
    // =====================================================================================
    sealed class StarsPage : Page
    {
        protected override string PaperKind => ""night"";
        int phase; float phaseT, skyAngle;
        Vector3[] sky; Vector4[] colors;
        readonly List<(Vector3 P, Vector3 V, float Age)> shooting = new();
        static Vector3[] dome; static Vector4[] domeColor;
        public StarsPage(BookOfElements s) : base(s) { InkColor = Rgb(214, 222, 246); }

        protected override void Layout() => Poem(""Stars"", ""Sara Teasdale, 1920"", new[]
        {
            ""Alone in the night"", ""On a dark hill"", ""With pines around me"", ""Spicy and still,"",
            """", ""And a heaven full of stars"", ""Over my head,"", ""White and topaz"", ""And misty red;"",
        }, 34, 21);

        protected override void Init()
        {
            var white = Rgb(255, 255, 255); var topaz = Rgb(255, 204, 120); var red = Rgb(255, 128, 128);
            sky = new Vector3[Letters.Count]; colors = new Vector4[Letters.Count];
            var centers = new Dictionary<int, (Vector3 C, Vector2 Dir)>();
            for (int i = 0; i < Letters.Count; i++)
            {
                var L = Letters[i];
                if (!centers.TryGetValue(L.Word, out var w))
                {
                    float a = R(-0.6f, 0.6f);
                    w = (new Vector3(R(-120, 700), R(60, 540), R(70, 150)), new Vector2(MathF.Cos(a), MathF.Sin(a)));
                    centers[L.Word] = w;
                    centers[L.Word + 100000] = (Vector3.Zero, Vector2.Zero);    // a counter of letters placed
                }
                int n = (int)centers[L.Word + 100000].C.X;
                centers[L.Word + 100000] = (new Vector3(n + 1, 0, 0), Vector2.Zero);
                sky[i] = w.C + new Vector3(w.Dir.X * n * 30 + R(-8, 8), w.Dir.Y * n * 30 + R(-8, 8), R(-14, 14));
                float pick = R(0, 1);
                colors[i] = pick < 0.6f ? white : pick < 0.85f ? topaz : red;
                L.GlowColor = colors[i];
            }
            if (dome == null)
            {
                dome = new Vector3[700]; domeColor = new Vector4[700];
                var rng = new System.Random(11);
                for (int i = 0; i < dome.Length; i++)
                {
                    float a = (float)rng.NextDouble() * MathF.PI * 2, el = 0.12f + (float)rng.NextDouble() * 1.35f;
                    dome[i] = new Vector3(MathF.Cos(a) * MathF.Cos(el), MathF.Sin(el), MathF.Sin(a) * MathF.Cos(el)) * 1500;
                    double k = rng.NextDouble();
                    domeColor[i] = k < 0.7 ? white : k < 0.88 ? topaz : red;
                }
            }
        }

        public override void Open(float t) { phase = 0; phaseT = 0; }
        public override void Close() { base.Close(); shooting.Clear(); }

        Vector3 SkyOffset(int i)
        {
            // The sky turns slowly around the book's middle (the gutter, at x = TW on this left page).
            var p = sky[i];
            float c = MathF.Cos(skyAngle), s = MathF.Sin(skyAngle);
            float x = p.X - TW, y = p.Y - TH / 2;
            var q = new Vector3(TW + x * c - y * s, TH / 2 + x * s + y * c, p.Z);
            return q - new Vector3(Letters[i].X, Letters[i].Y, 0);
        }

        public override void Step(float dt, float t, bool open)
        {
            if (!open) return;
            phaseT += dt; skyAngle += dt * 0.05f;
            float[] lengths = { 5, 3.6f, 11, 2.8f };
            if (phaseT > lengths[phase])
            {
                phase = (phase + 1) % 4; phaseT = 0;
                if (phase == 1) S.Play(new[] { 72, 76, 79, 83 }, amp: 0.05, attack: 0.6, release: 2.5, synth: Synth.Triangle);
            }
            for (int i = 0; i < Letters.Count; i++)
            {
                var L = Letters[i];
                var up = SkyOffset(i);
                float twinkle = 0.5f + 0.5f * MathF.Sin(t * (2 + L.Seed * 3) + L.Seed * 40);
                switch (phase)
                {
                    case 0:
                        L.Off = Vector3.Zero; L.Glow = 0.12f + 0.12f * twinkle; L.Rot = Quaternion.Identity; L.Scale = 1;
                        break;
                    case 1:
                    {
                        float e = Smooth(0, 1, (phaseT - L.Seed * 1.0f) / 2.4f);
                        var swirl = new Vector3(MathF.Cos(L.Seed * 6.3f), MathF.Sin(L.Seed * 6.3f), 0) * 70 * MathF.Sin(e * MathF.PI);
                        L.Off = up * e + swirl; L.Glow = 0.3f + 0.5f * e; L.Scale = 1 + 0.5f * e;
                        L.Rot = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, e * (L.Seed - 0.5f) * 2);
                        break;
                    }
                    case 2:
                        L.Off = up + new Vector3(0, 0, 4 * MathF.Sin(t * 0.8f + L.Seed * 9)); L.Glow = 0.6f + 0.35f * twinkle; L.Scale = 1.5f;
                        break;
                    case 3:
                    {
                        float e = Smooth(0, 1, (phaseT - L.Seed * 0.8f) / 1.6f);
                        L.Off = up * (1 - e); L.Glow = 0.9f - 0.7f * e; L.Scale = 1.5f - 0.5f * e;
                        L.Rot = Quaternion.Slerp(L.Rot, Quaternion.Identity, 0.1f);
                        break;
                    }
                }
            }
            if (phase == 2 && Rng.NextDouble() < dt * 0.4) Shoot();
            for (int i = shooting.Count - 1; i >= 0; i--)
            {
                var (p, v, age) = shooting[i];
                p += v * dt; age += dt;
                if (age > 1.3f) { shooting.RemoveAt(i); continue; }
                shooting[i] = (p, v, age);
            }
        }

        void Shoot()
        {
            float a = R(0, TWO_PI);
            shooting.Add((new Vector3(TW + MathF.Cos(a) * R(200, 500), TH / 2 + MathF.Sin(a) * R(200, 400), R(260, 380)), new Vector3(-MathF.Cos(a) * 520, -MathF.Sin(a) * 300, -120), 0));
        }

        public override void Press(float x, float y)
        {
            if (phase == 0) { phase = 1; phaseT = 0; S.Play(new[] { 72, 76, 79, 83 }, amp: 0.05, attack: 0.6, release: 2.5, synth: Synth.Triangle); }
            else Shoot();
        }

        public override void DrawGlow()
        {
            float night = S.night * Live, t = S.Now;
            for (int i = 0; i < dome.Length; i++)
            {
                float tw = 0.55f + 0.45f * MathF.Sin(t * (1.5f + i % 7 * 0.3f) + i);
                S.AddGlow(dome[i] + new Vector3(0, -120, 0), (i % 5 == 0 ? 16 : 9), domeColor[i], 0.75f * tw * night);
            }
            float skyAmount = phase == 2 ? 1 : phase == 1 ? Clamp01(phaseT / 3) : phase == 3 ? Clamp01(1 - phaseT / 1.5f) : 0;
            for (int i = 0; i < Letters.Count; i++)
            {
                var L = Letters[i];
                var at = W(L.X + L.Off.X, L.Y + L.Off.Y, L.Off.Z);
                if (L.Off.Z > 5) S.AddGlow(at, 14 * K * L.Scale, colors[i], 0.45f * Clamp01(L.Glow) * Live);
                if (i > 0 && Letters[i - 1].Word == L.Word && skyAmount > 0.01f)
                {
                    var P = Letters[i - 1];
                    S.AddLine(W(P.X + P.Off.X, P.Y + P.Off.Y, P.Off.Z), at, 0.6f, Rgb(170, 190, 255, 110 * skyAmount * Live));
                }
            }
            foreach (var (p, v, age) in shooting)
            {
                float a = Smooth(1.3f, 0.8f, age) * Live;
                S.AddLine(W(p.X, p.Y, p.Z), W(p.X - v.X * 0.25f, p.Y - v.Y * 0.25f, p.Z - v.Z * 0.25f), 1.4f, Rgb(255, 250, 230, 180 * a));
                S.AddGlow(W(p.X, p.Y, p.Z), 14 * K, Rgb(255, 255, 240), a);
            }
        }
    }
}
";
    }
}
