using System;
using System.Collections.Generic;
using System.IO;
using SkiaSharp;

namespace DanaProcessing
{
    // === Enums compartidos por Sketch y PGraphics ===

    /// <summary>How Fill/Stroke/Background interpret their numeric arguments.</summary>
    public enum ColorSpaceMode { RGB, HSB }

    /// <summary>How the four numeric arguments to Rect/Ellipse/Shape are interpreted — https://processing.org/reference/rectMode_.html</summary>
    public enum ShapeAlignMode { Corner, Corners, Center, Radius }

    /// <summary>How Arc fills the space between its two radii and the chord — https://processing.org/reference/arc_.html</summary>
    public enum ArcMode { Open, Chord, Pie }

    /// <summary>Line ending style — https://processing.org/reference/strokeCap_.html. Note Processing's SQUARE == Skia's Butt (flush, no extension) and Processing's PROJECT == Skia's Square (extends past the endpoint) — the names collide across the two APIs, so don't map them by name.</summary>
    public enum StrokeCapKind { Round, Square, Project }

    /// <summary>Corner style where two segments meet — https://processing.org/reference/strokeJoin_.html</summary>
    public enum StrokeJoinKind { Miter, Bevel, Round }

    /// <summary>How EndShape() interprets the recorded vertices — https://processing.org/reference/beginShape_.html</summary>
    public enum ShapeKind { Polygon, Points, Lines, Triangles, TriangleFan, TriangleStrip, Quads, QuadStrip }

    /// <summary>Horizontal text alignment — https://processing.org/reference/textAlign_.html</summary>
    public enum TextAlignH { Left, Center, Right }

    /// <summary>Vertical text alignment — https://processing.org/reference/textAlign_.html</summary>
    public enum TextAlignV { Baseline, Top, Center, Bottom }

    /// <summary>Compositing mode for subsequent drawing (and for Blend()) — https://processing.org/reference/blendMode_.html. Processing's SUBTRACT is deliberately omitted: it has no direct SKBlendMode equivalent (it isn't a true channel subtraction in Skia's set), so it isn't offered here rather than being approximated poorly.</summary>
    public enum BlendModeKind { Blend, Add, Darkest, Lightest, Difference, Exclusion, Multiply, Screen, Overlay, HardLight, SoftLight, Dodge, Burn }

    /// <summary>Per-pixel filters usable via Filter() — https://processing.org/reference/filter_.html. Erode/Dilate need neighbor-pixel sampling (not just a per-pixel transform) and aren't implemented yet — Filter() throws NotSupportedException for them.</summary>
    public enum FilterKind { Gray, Invert, Threshold, Posterize, Opaque, Blur, Erode, Dilate }

    /// <summary>
    /// Everything about drawing state and drawing operations that Sketch and
    /// PGraphics have in common: fill/stroke/color, shape modes, the 2D
    /// primitives, custom shapes, text, gradients, transformations,
    /// compositing other images/buffers/vector shapes onto this one, and
    /// saving output to disk.
    ///
    /// Subclasses provide the actual SKCanvas (via the protected Canvas
    /// property) and Width/Height, and can override EnsureReady() to add
    /// their own precondition checks — PGraphics uses this to require
    /// BeginDraw()/EndDraw() and to guard against use after Dispose(); Sketch
    /// leaves it as a no-op since its host guarantees a valid Canvas before
    /// Draw() runs.
    ///
    /// This class deliberately knows nothing about sketch lifecycle
    /// (Setup/Draw), input (mouse/keyboard), time, or randomness/noise —
    /// those stay on Sketch, since PGraphics doesn't have or need them.
    /// </summary>
    public abstract class GraphicsContext : IDisposable
    {
        public int Width { get; protected set; }
        public int Height { get; protected set; }

        /// <summary>The canvas this context draws into. Sketch sets this each frame via SetCanvas(); PGraphics sets it once, in its constructor.</summary>
        protected SKCanvas Canvas { get; set; } = null!;

        /// <summary>
        /// The surface backing Canvas, if any — needed only for Save(), which
        /// reads pixels back via Surface.PeekPixels(). PGraphics always has
        /// one (set in its constructor). Sketch only has one if its host
        /// passes it to SetCanvas(); a host that draws into some other kind
        /// of target (a shared/externally-owned canvas) can leave this null,
        /// in which case Save() throws rather than silently failing.
        /// </summary>
        protected SKSurface? Surface { get; set; }

        /// <summary>Called at the top of every method that touches Canvas. Base implementation is a no-op.</summary>
        protected virtual void EnsureReady() { }

        private bool _disposed;

        // =====================================================================
        // Drawing state (fill/stroke/text)
        // =====================================================================

        private readonly SKPaint _fillPaint = new SKPaint { Style = SKPaintStyle.Fill, IsAntialias = true, Color = SKColors.White };
        private readonly SKPaint _strokePaint = new SKPaint { Style = SKPaintStyle.Stroke, IsAntialias = true, Color = SKColors.Black, StrokeWidth = 1 };
        private readonly SKPaint _textPaint = new SKPaint { Color = SKColors.White, IsAntialias = true, TextSize = 16 };
        private bool _fillEnabled = true;
        private bool _strokeEnabled = true;
        private ColorSpaceMode _colorMode = ColorSpaceMode.RGB;
        private ShapeAlignMode _rectMode = ShapeAlignMode.Corner;
        private ShapeAlignMode _ellipseMode = ShapeAlignMode.Center;
        private ShapeAlignMode _shapeDrawMode = ShapeAlignMode.Corner;
        private ShapeAlignMode _imageMode = ShapeAlignMode.Corner;
        private SKColor? _tintColor; // null = sin tinte, Image() dibuja el bitmap sin modificar
        private float _curveTightness = 0f;
        private bool _smooth = true;
        private SKBlendMode _blendMode = SKBlendMode.SrcOver;

        // Rangos de cada canal para ColorMode(mode, max...) — por defecto 0-255
        // en ambos modos, igual que Processing (colorMode(HSB) por sí solo NO
        // cambia el rango a 360/100/100; hace falta pedirlo explícitamente:
        // colorMode(HSB, 360, 100, 100)).
        private float _colorMax1 = 255f, _colorMax2 = 255f, _colorMax3 = 255f, _colorMaxA = 255f;

        /// <summary>
        /// Sets how Fill/Stroke/Background interpret their arguments from here
        /// on: RGB (each 0-255) or HSB (hue 0-360, saturation/brightness 0-100)
        /// — matches Processing's own colorMode(). Switching modes doesn't
        /// recolor anything already drawn or already set via a previous
        /// Fill()/Stroke() call — only calls made after this one are affected.
        /// </summary>
        public void ColorMode(ColorSpaceMode mode) => ColorMode(mode, 255f, 255f, 255f, 255f);

        /// <summary>Sets the mode and a single max value shared by all three color channels plus alpha, like Processing's colorMode(mode, max).</summary>
        public void ColorMode(ColorSpaceMode mode, float max) => ColorMode(mode, max, max, max, max);

        /// <summary>Sets the mode and a max value per channel (alpha keeps its previous max unless given), like Processing's colorMode(mode, max1, max2, max3, maxA). In HSB mode max1/max2/max3 scale hue/saturation/brightness respectively — e.g. ColorMode(HSB, 360, 100, 100) gives the classic HSB ranges.</summary>
        public void ColorMode(ColorSpaceMode mode, float max1, float max2, float max3, float? maxA = null)
        {
            _colorMode = mode;
            _colorMax1 = max1;
            _colorMax2 = max2;
            _colorMax3 = max3;
            _colorMaxA = maxA ?? _colorMaxA; // como en Processing: colorMode(mode, max1, max2, max3) no toca el rango de alpha
        }

        public void RectMode(ShapeAlignMode mode) => _rectMode = mode;
        public void EllipseMode(ShapeAlignMode mode) => _ellipseMode = mode;

        /// <summary>Sets how Shape()'s x/y/w/h are interpreted — CORNER (default, x,y is top-left), CORNERS (x,y and w,h are two opposite corners, not a size), or CENTER. Matches Processing's shapeMode(). RADIUS isn't meaningful for shapeMode in Processing either, so it isn't supported here.</summary>
        public void ShapeMode(ShapeAlignMode mode) => _shapeDrawMode = mode;

        /// <summary>Sets how Image()'s x/y/w/h are interpreted, like Processing's imageMode(). Default CORNER (x,y is top-left, w,h is size); CORNERS treats w,h as the opposite corner instead of a size; CENTER positions on the image's center.</summary>
        public void ImageMode(ShapeAlignMode mode) => _imageMode = mode;

        /// <summary>Sets a color/alpha multiplier applied to images drawn via Image() from here on, like Processing's tint(). Interpreted per the current ColorMode, same as Fill/Stroke.</summary>
        public void Tint(float a1, float a2, float a3, byte alpha = 255) => _tintColor = ResolveColor(a1, a2, a3, alpha);

        /// <summary>Sets just the opacity of subsequently drawn images, keeping their own colors (a white multiplier), like Processing's one-argument tint(alpha).</summary>
        public void Tint(byte alpha) => _tintColor = new SKColor(255, 255, 255, alpha);

        /// <summary>Clears any tint set by Tint() — images drawn after this show their original colors, like Processing's noTint().</summary>
        public void NoTint() => _tintColor = null;

        /// <summary>Builds a paint that multiplies a drawn bitmap's colors by the current tint, or null if no tint is set (callers should pass null straight to DrawBitmap/DrawImage in that case).</summary>
        private SKPaint? BuildTintPaint()
        {
            if (_tintColor == null)
                return null;
            return new SKPaint { ColorFilter = SKColorFilter.CreateBlendMode(_tintColor.Value, SKBlendMode.Modulate) };
        }

        /// <summary>
        /// Resolves three numeric arguments into a color according to the
        /// current ColorMode: RGB (each 0-255, clamped) or HSB (hue 0-360,
        /// saturation/brightness 0-100). This is the single runtime switch
        /// point Fill/Stroke/Background all go through — mode selection has to
        /// happen here, at call time, not via method overloads, since C#
        /// picks an overload at compile time and ColorMode is a runtime setting.
        /// </summary>
        private SKColor ResolveColor(float a1, float a2, float a3, float alpha)
        {
            byte a = ScaleToByte(alpha, _colorMaxA);
            if (_colorMode == ColorSpaceMode.HSB)
            {
                float h = a1 / _colorMax1 * 360f;
                float s = a2 / _colorMax2 * 100f;
                float v = a3 / _colorMax3 * 100f;
                return SKColor.FromHsv(h, s, v, a);
            }
            return new SKColor(ScaleToByte(a1, _colorMax1), ScaleToByte(a2, _colorMax2), ScaleToByte(a3, _colorMax3), a);
        }

        /// <summary>Rescales a value from [0, max] into a clamped byte [0, 255] — the normalization every Fill/Stroke/Background argument goes through per the current ColorMode ranges.</summary>
        private static byte ScaleToByte(float v, float max) => (byte)Math.Clamp(max == 0 ? 0 : v / max * 255f, 0, 255);

        // =====================================================================
        // Color construction — https://processing.org/reference/color_.html.
        // This is Processing's color(...) *function* — not to be confused
        // with `new Color(r, g, b)` on the Color struct itself (see the
        // remark atop Color.cs on why that one's a constructor, not a
        // same-named method). The two do different things on purpose:
        // `new Color(r, g, b)` always means plain 0-255 RGB, full stop, no
        // matter what ColorMode() is currently set to — the same way
        // Color.FromHsb(...) always means HSB, full stop. Color(...) here is
        // the one that's ColorMode-aware, exactly like Fill()/Stroke()/
        // Background() are, since that's what Processing's color() does too.
        // C# resolves the two without any ambiguity: `new Color(...)` is
        // always the constructor (the `new` keyword forces a type there),
        // while bare `Color(...)` — anywhere Sketch/PGraphics code can see
        // this method, i.e. everywhere a sketch actually calls it — is
        // always this method.
        // =====================================================================

        /// <summary>Builds a Color from three components interpreted per the current ColorMode (RGB by default), like Processing's color(v1, v2, v3, alpha). For a ColorMode-independent RGB color, use `new Color(r, g, b)` instead.</summary>
        public Color Color(float a1, float a2, float a3, float alpha = 255) => new Color(ResolveColor(a1, a2, a3, alpha));

        /// <summary>Builds a gray Color (same value on all three channels), like Processing's single-argument color(gray).</summary>
        public Color Color(float gray, float alpha = 255) => Color(gray, gray, gray, alpha);

        /// <summary>Turns the four raw arguments Rect/Ellipse/Arc/Shape receive into an SKRect, per the given mode. Shared since Processing defines the same four modes identically across rect(), ellipse() (via ellipseMode, which arc() also reuses), and shape().</summary>
        private static SKRect ResolveRectMode(ShapeAlignMode mode, float a, float b, float c, float d) => mode switch
        {
            ShapeAlignMode.Corner => new SKRect(a, b, a + c, b + d),
            ShapeAlignMode.Corners => new SKRect(a, b, c, d),
            ShapeAlignMode.Center => new SKRect(a - c / 2, b - d / 2, a + c / 2, b + d / 2),
            ShapeAlignMode.Radius => new SKRect(a - c, b - d, a + c, b + d),
            _ => throw new ArgumentOutOfRangeException(nameof(mode))
        };

        /// <summary>Clears to a solid color, interpreted per the current ColorMode (RGB by default).</summary>
        public void Background(float a1, float a2, float a3, float alpha = 255) => BackgroundColor(ResolveColor(a1, a2, a3, alpha));

        /// <summary>Clears to a shade of gray (same value on all three channels), like Processing's single-argument background(gray).</summary>
        public void Background(float gray, float alpha = 255) => Background(gray, gray, gray, alpha);

        /// <summary>Clears to an already-built Color, like Processing's background(color).</summary>
        public void Background(Color c) => BackgroundColor(c.Skia);

        private void BackgroundColor(SKColor color)
        {
            EnsureReady();
            using var paint = new SKPaint { Color = color, Style = SKPaintStyle.Fill };
            Canvas.DrawRect(new SKRect(0, 0, Width, Height), paint);
        }

        /// <summary>Clears this surface to fully transparent. Most useful on a PGraphics you intend to composite over other content via Image().</summary>
        public void Clear()
        {
            EnsureReady();
            Canvas.Clear(SKColors.Transparent);
        }

        /// <summary>Sets the fill color, interpreted per the current ColorMode (RGB by default).</summary>
        public void Fill(float a1, float a2, float a3, float alpha = 255) => FillColor(ResolveColor(a1, a2, a3, alpha));

        /// <summary>Sets the fill to a shade of gray (same value on all three channels), like Processing's single-argument fill(gray).</summary>
        public void Fill(float gray, float alpha = 255) => Fill(gray, gray, gray, alpha);

        /// <summary>Sets the fill to an already-built Color, like Processing's fill(color).</summary>
        public void Fill(Color c) => FillColor(c.Skia);

        private void FillColor(SKColor color)
        {
            _fillEnabled = true;
            _fillPaint.Shader = null; // plain fill overrides any gradient set earlier
            _fillPaint.Color = color;
            _textPaint.Color = color;
        }

        /// <summary>Fill using Hue (0-360), Saturation (0-100), Brightness (0-100) — regardless of the current ColorMode.</summary>
        public void FillHSB(float h, float s, float br, byte a = 255) => FillColor(SKColor.FromHsv(h, s, br, a));

        public void NoFill() => _fillEnabled = false;

        /// <summary>Sets the stroke color, interpreted per the current ColorMode (RGB by default).</summary>
        public void Stroke(float a1, float a2, float a3, float alpha = 255) => StrokeColorSet(ResolveColor(a1, a2, a3, alpha));

        /// <summary>Sets the stroke to a shade of gray (same value on all three channels), like Processing's single-argument stroke(gray).</summary>
        public void Stroke(float gray, float alpha = 255) => Stroke(gray, gray, gray, alpha);

        /// <summary>Sets the stroke to an already-built Color, like Processing's stroke(color).</summary>
        public void Stroke(Color c) => StrokeColorSet(c.Skia);

        private void StrokeColorSet(SKColor color)
        {
            _strokeEnabled = true;
            _strokePaint.Color = color;
        }

        /// <summary>Stroke using Hue (0-360), Saturation (0-100), Brightness (0-100) — regardless of the current ColorMode.</summary>
        public void StrokeHSB(float h, float s, float br, byte a = 255) => StrokeColorSet(SKColor.FromHsv(h, s, br, a));

        public void NoStroke() => _strokeEnabled = false;
        public void StrokeWeight(float w) => _strokePaint.StrokeWidth = w;

        public void StrokeCap(StrokeCapKind cap) => _strokePaint.StrokeCap = cap switch
        {
            StrokeCapKind.Round => SKStrokeCap.Round,
            StrokeCapKind.Square => SKStrokeCap.Butt,
            StrokeCapKind.Project => SKStrokeCap.Square,
            _ => throw new ArgumentOutOfRangeException(nameof(cap))
        };

        public void StrokeJoin(StrokeJoinKind join) => _strokePaint.StrokeJoin = join switch
        {
            StrokeJoinKind.Miter => SKStrokeJoin.Miter,
            StrokeJoinKind.Bevel => SKStrokeJoin.Bevel,
            StrokeJoinKind.Round => SKStrokeJoin.Round,
            _ => throw new ArgumentOutOfRangeException(nameof(join))
        };

        // =====================================================================
        // Antialiasing — https://processing.org/reference/smooth_.html
        // =====================================================================

        /// <summary>Turns antialiasing on for shapes/lines/text drawn from here on, like Processing's smooth(). On by default.</summary>
        public void Smooth() => ApplySmooth(true);

        /// <summary>Turns antialiasing off, like Processing's noSmooth() — gives hard, pixelated edges, occasionally desired for pixel-art-style sketches.</summary>
        public void NoSmooth() => ApplySmooth(false);

        private void ApplySmooth(bool on)
        {
            _smooth = on;
            _fillPaint.IsAntialias = on;
            _strokePaint.IsAntialias = on;
            _textPaint.IsAntialias = on;
        }

        // =====================================================================
        // Blend mode — https://processing.org/reference/blendMode_.html. Applies
        // to every subsequent drawing operation (shapes, images, text), not just
        // Blend()/Copy() below, matching Processing's own global blendMode().
        // =====================================================================
        public void BlendMode(BlendModeKind mode) => SetBlendModeInternal(ResolveBlendMode(mode));

        private static SKBlendMode ResolveBlendMode(BlendModeKind mode) => mode switch
        {
            BlendModeKind.Blend => SKBlendMode.SrcOver,
            BlendModeKind.Add => SKBlendMode.Plus,
            BlendModeKind.Darkest => SKBlendMode.Darken,
            BlendModeKind.Lightest => SKBlendMode.Lighten,
            BlendModeKind.Difference => SKBlendMode.Difference,
            BlendModeKind.Exclusion => SKBlendMode.Exclusion,
            BlendModeKind.Multiply => SKBlendMode.Multiply,
            BlendModeKind.Screen => SKBlendMode.Screen,
            BlendModeKind.Overlay => SKBlendMode.Overlay,
            BlendModeKind.HardLight => SKBlendMode.HardLight,
            BlendModeKind.SoftLight => SKBlendMode.SoftLight,
            BlendModeKind.Dodge => SKBlendMode.ColorDodge,
            BlendModeKind.Burn => SKBlendMode.ColorBurn,
            // Nota: Processing's SUBTRACT no tiene un SKBlendMode equivalente
            // directo en Skia (no es una resta real de canales), por lo que
            // deliberadamente no se ofrece aquí en vez de aproximarlo mal.
            _ => throw new ArgumentOutOfRangeException(nameof(mode))
        };

        private void SetBlendModeInternal(SKBlendMode mode)
        {
            _blendMode = mode;
            _fillPaint.BlendMode = mode;
            _strokePaint.BlendMode = mode;
        }

        // =====================================================================
        // PushStyle/PopStyle — https://processing.org/reference/pushStyle_.html.
        // Snapshots every piece of drawing *state* this class tracks (colors,
        // color/rect/ellipse/shape/image modes, tint, stroke weight/cap/join,
        // text size/align/leading, curve tightness, smooth, blend mode) —
        // deliberately everything PushMatrix/PopMatrix does NOT cover, since
        // those two already handle the transformation matrix on their own
        // stack via Canvas.Save()/Restore(). Gradient shaders set via
        // LinearGradientFill/RadialGradientFill are not restored by PopStyle —
        // call Fill() with a plain color afterward if you need to clear one.
        // =====================================================================

        private struct StyleSnapshot
        {
            public bool FillEnabled, StrokeEnabled;
            public SKColor FillColor, StrokeColor;
            public ColorSpaceMode ColorMode;
            public float ColorMax1, ColorMax2, ColorMax3, ColorMaxA;
            public ShapeAlignMode RectMode, EllipseMode, ShapeDrawMode, ImageMode;
            public SKColor? TintColor;
            public float StrokeWeight;
            public SKStrokeCap StrokeCap;
            public SKStrokeJoin StrokeJoin;
            public float TextSize;
            public TextAlignH TextAlignH;
            public TextAlignV TextAlignV;
            public float? TextLeading;
            public float CurveTightness;
            public bool Smooth;
            public SKBlendMode BlendMode;
        }

        private readonly Stack<StyleSnapshot> _styleStack = new Stack<StyleSnapshot>();

        /// <summary>Saves every current style setting onto a stack, like Processing's pushStyle(). Pair with PopStyle() to restore it later — handy for temporarily changing a color/mode/weight/etc. inside a block of drawing code without affecting anything after it.</summary>
        public void PushStyle()
        {
            _styleStack.Push(new StyleSnapshot
            {
                FillEnabled = _fillEnabled,
                StrokeEnabled = _strokeEnabled,
                FillColor = _fillPaint.Color,
                StrokeColor = _strokePaint.Color,
                ColorMode = _colorMode,
                ColorMax1 = _colorMax1,
                ColorMax2 = _colorMax2,
                ColorMax3 = _colorMax3,
                ColorMaxA = _colorMaxA,
                RectMode = _rectMode,
                EllipseMode = _ellipseMode,
                ShapeDrawMode = _shapeDrawMode,
                ImageMode = _imageMode,
                TintColor = _tintColor,
                StrokeWeight = _strokePaint.StrokeWidth,
                StrokeCap = _strokePaint.StrokeCap,
                StrokeJoin = _strokePaint.StrokeJoin,
                TextSize = _textPaint.TextSize,
                TextAlignH = _textAlignH,
                TextAlignV = _textAlignV,
                TextLeading = _textLeading,
                CurveTightness = _curveTightness,
                Smooth = _smooth,
                BlendMode = _blendMode,
            });
        }

        /// <summary>Restores the style settings saved by the most recent unmatched PushStyle(), like Processing's popStyle().</summary>
        public void PopStyle()
        {
            if (_styleStack.Count == 0)
                throw new InvalidOperationException("PopStyle() llamado sin un PushStyle() correspondiente.");
            var s = _styleStack.Pop();
            _fillEnabled = s.FillEnabled;
            _strokeEnabled = s.StrokeEnabled;
            _fillPaint.Shader = null;
            _fillPaint.Color = s.FillColor;
            _textPaint.Color = s.FillColor;
            _strokePaint.Color = s.StrokeColor;
            _colorMode = s.ColorMode;
            _colorMax1 = s.ColorMax1;
            _colorMax2 = s.ColorMax2;
            _colorMax3 = s.ColorMax3;
            _colorMaxA = s.ColorMaxA;
            _rectMode = s.RectMode;
            _ellipseMode = s.EllipseMode;
            _shapeDrawMode = s.ShapeDrawMode;
            _imageMode = s.ImageMode;
            _tintColor = s.TintColor;
            _strokePaint.StrokeWidth = s.StrokeWeight;
            _strokePaint.StrokeCap = s.StrokeCap;
            _strokePaint.StrokeJoin = s.StrokeJoin;
            _textPaint.TextSize = s.TextSize;
            _textAlignH = s.TextAlignH;
            _textAlignV = s.TextAlignV;
            _textLeading = s.TextLeading;
            _curveTightness = s.CurveTightness;
            ApplySmooth(s.Smooth);
            SetBlendModeInternal(s.BlendMode);
        }

        // =====================================================================
        // Color extraction — https://processing.org/reference/red_.html and
        // siblings, plus lerpColor_.html.
        // =====================================================================

        public float Red(Color c) => c.R;
        public float Green(Color c) => c.G;
        public float Blue(Color c) => c.B;
        public float Alpha(Color c) => c.A;

        public float Hue(Color c) { c.Skia.ToHsv(out float h, out _, out _); return h; }
        public float Saturation(Color c) { c.Skia.ToHsv(out _, out float s, out _); return s; }
        public float Brightness(Color c) { c.Skia.ToHsv(out _, out _, out float v); return v; }

        /// <summary>Interpolates between two colors by amt (0-1) in RGB space, like Processing's lerpColor().</summary>
        public Color LerpColor(Color c1, Color c2, float amt)
        {
            amt = Math.Clamp(amt, 0f, 1f);
            byte r = (byte)(c1.R + (c2.R - c1.R) * amt);
            byte g = (byte)(c1.G + (c2.G - c1.G) * amt);
            byte b = (byte)(c1.B + (c2.B - c1.B) * amt);
            byte a = (byte)(c1.A + (c2.A - c1.A) * amt);
            return new Color(r, g, b, a);
        }

        // =====================================================================
        // Primitives
        // =====================================================================

        public void Rect(float a, float b, float c, float d)
        {
            EnsureReady();
            var rect = ResolveRectMode(_rectMode, a, b, c, d);
            if (_fillEnabled)
                Canvas.DrawRect(rect, _fillPaint);
            if (_strokeEnabled)
                Canvas.DrawRect(rect, _strokePaint);
        }

        public void Ellipse(float a, float b, float c, float d)
        {
            EnsureReady();
            var rect = ResolveRectMode(_ellipseMode, a, b, c, d);
            if (_fillEnabled)
                Canvas.DrawOval(rect, _fillPaint);
            if (_strokeEnabled)
                Canvas.DrawOval(rect, _strokePaint);
        }

        public void Line(float x1, float y1, float x2, float y2)
        {
            EnsureReady();
            Canvas.DrawLine(x1, y1, x2, y2, _strokePaint);
        }

        /// <summary>Draws a single point using the current stroke color/weight.</summary>
        public void Point(float x, float y)
        {
            EnsureReady();
            Canvas.DrawPoint(x, y, _strokePaint);
        }

        public void Triangle(float x1, float y1, float x2, float y2, float x3, float y3)
        {
            EnsureReady();
            using var path = new SKPath();
            path.MoveTo(x1, y1);
            path.LineTo(x2, y2);
            path.LineTo(x3, y3);
            path.Close();
            if (_fillEnabled)
                Canvas.DrawPath(path, _fillPaint);
            if (_strokeEnabled)
                Canvas.DrawPath(path, _strokePaint);
        }

        /// <summary>Draws a closed polygon through the given points, e.g. Polygon((0,0), (50,0), (25,50)).</summary>
        public void Polygon(params (float x, float y)[] points)
        {
            EnsureReady();
            if (points.Length < 2)
                return;
            using var path = new SKPath();
            path.MoveTo(points[0].x, points[0].y);
            for (int i = 1; i < points.Length; i++)
                path.LineTo(points[i].x, points[i].y);
            path.Close();
            if (_fillEnabled)
                Canvas.DrawPath(path, _fillPaint);
            if (_strokeEnabled)
                Canvas.DrawPath(path, _strokePaint);
        }

        /// <summary>Draws a quadrilateral through four points, in order.</summary>
        public void Quad(float x1, float y1, float x2, float y2, float x3, float y3, float x4, float y4) =>
            Polygon((x1, y1), (x2, y2), (x3, y3), (x4, y4));

        /// <summary>
        /// Draws an arc of the ellipse bounded per the current EllipseMode, from
        /// `start` to `stop` radians. Angles follow Processing's convention: 0
        /// points right, increasing clockwise.
        /// </summary>
        public void Arc(float a, float b, float c, float d, float start, float stop, ArcMode mode = ArcMode.Open)
        {
            EnsureReady();
            var oval = ResolveRectMode(_ellipseMode, a, b, c, d);
            float startDeg = start * (180f / MathF.PI);
            float sweepDeg = (stop - start) * (180f / MathF.PI);

            using var path = new SKPath();
            if (mode == ArcMode.Pie)
            {
                path.MoveTo(oval.MidX, oval.MidY);
                path.ArcTo(oval, startDeg, sweepDeg, false);
                path.Close();
            }
            else
            {
                path.ArcTo(oval, startDeg, sweepDeg, true);
                if (mode == ArcMode.Chord)
                    path.Close();
            }

            if (_fillEnabled)
                Canvas.DrawPath(path, _fillPaint);
            if (_strokeEnabled)
                Canvas.DrawPath(path, _strokePaint);
        }

        /// <summary>Draws a cubic Bezier curve from (x1,y1) to (x2,y2), shaped by control points (cx1,cy1) and (cx2,cy2).</summary>
        public void Bezier(float x1, float y1, float cx1, float cy1, float cx2, float cy2, float x2, float y2)
        {
            EnsureReady();
            using var path = new SKPath();
            path.MoveTo(x1, y1);
            path.CubicTo(cx1, cy1, cx2, cy2, x2, y2);
            Canvas.DrawPath(path, _strokePaint);
        }

        /// <summary>Sets the "tightness" of subsequent Curve() calls, like Processing's curveTightness(). 0 (the default) is the standard Catmull-Rom curve; values toward 1 pull the curve tighter to straight lines between the on-curve points.</summary>
        public void CurveTightness(float tightness) => _curveTightness = tightness;

        public void Curve(float x1, float y1, float x2, float y2, float x3, float y3, float x4, float y4)
        {
            // Catmull-Rom -> Bezier control point conversion, generalized by
            // curveTightness: s = (1 - tightness) / 6, which reduces to the
            // standard 1/6 tension when tightness is 0 (the default).
            float s = (1f - _curveTightness) / 6f;
            float cx1 = x2 + s * (x3 - x1);
            float cy1 = y2 + s * (y3 - y1);
            float cx2 = x3 - s * (x4 - x2);
            float cy2 = y3 - s * (y4 - y2);
            Bezier(x2, y2, cx1, cy1, cx2, cy2, x3, y3);
        }

        /// <summary>Evaluates one axis of a cubic Bezier curve at parameter t (0-1) without drawing it, like Processing's bezierPoint(). Call once for x and once for y with the same t to get a point along the curve — handy for animating something along a path traced earlier with Bezier().</summary>
        public float BezierPoint(float a, float b, float c, float d, float t)
        {
            float u = 1f - t;
            return u * u * u * a + 3f * u * u * t * b + 3f * u * t * t * c + t * t * t * d;
        }

        /// <summary>Evaluates the derivative (tangent direction, not normalized) of one axis of a cubic Bezier curve at t, like Processing's bezierTangent(). Use Atan2(BezierTangent(...y args...), BezierTangent(...x args...)) to get a heading angle for orienting something moving along the curve.</summary>
        public float BezierTangent(float a, float b, float c, float d, float t)
        {
            float u = 1f - t;
            return 3f * u * u * (b - a) + 6f * u * t * (c - b) + 3f * t * t * (d - c);
        }

        /// <summary>Evaluates one axis of a Catmull-Rom curve at parameter t (0-1) without drawing it, like Processing's curvePoint(). a/b/c/d are the same four control values (p0..p3) Curve()/CurveVertex() use — the curve passes through b at t=0 and c at t=1. Call once for x and once for y with the same t. Unlike Curve() itself, this always uses the standard tension and doesn't read CurveTightness() — matching Processing's own curvePoint(), which does the same.</summary>
        public float CurvePoint(float a, float b, float c, float d, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * ((2f * b) + (-a + c) * t + (2f * a - 5f * b + 4f * c - d) * t2 + (-a + 3f * b - 3f * c + d) * t3);
        }

        /// <summary>Evaluates the derivative (tangent direction, not normalized) of one axis of a Catmull-Rom curve at t, like Processing's curveTangent().</summary>
        public float CurveTangent(float a, float b, float c, float d, float t)
        {
            float t2 = t * t;
            return 0.5f * ((-a + c) + 2f * (2f * a - 5f * b + 4f * c - d) * t + 3f * (-a + 3f * b - 3f * c + d) * t2);
        }

        // =====================================================================
        // Custom shapes — https://processing.org/reference/beginShape_.html,
        // vertex_.html, endShape_.html, texture_.html.
        //
        // Two rendering paths at EndShape():
        //  - The simple path (unchanged from before): every vertex used the
        //    same fill color and no texture was set. Draws exactly as it did
        //    previously — one SKPath per primitive, proper stroke edges,
        //    proper fill/no-fill.
        //  - The mesh path: at least one vertex had a different fill color
        //    (Gouraud shading) or a texture was set via Texture(). Only
        //    Triangles/TriangleStrip/TriangleFan/Quads/QuadStrip support this
        //    — Polygon can't, since its vertices can include curved
        //    (Bezier/Quadratic) segments that don't triangulate. Uses
        //    SKCanvas.DrawVertices, which has no stroke concept — mesh shapes
        //    currently render fill only, no outline, regardless of Stroke().
        // =====================================================================

        private ShapeKind _shapeKind = ShapeKind.Polygon;
        private SKPath? _shapePath;
        private readonly List<SKPoint> _shapeVertices = new List<SKPoint>();
        private readonly List<SKColor> _shapeVertexColors = new List<SKColor>();
        private readonly List<SKPoint> _shapeVertexUVs = new List<SKPoint>();
        private readonly List<SKPoint> _curveVertexPoints = new List<SKPoint>();
        private PImage? _shapeTexture;

        public void BeginShape(ShapeKind kind = ShapeKind.Polygon)
        {
            _shapeKind = kind;
            _shapePath = kind == ShapeKind.Polygon ? new SKPath() : null;
            _shapeVertices.Clear();
            _shapeVertexColors.Clear();
            _shapeVertexUVs.Clear();
            _curveVertexPoints.Clear();
            _shapeTexture = null;
        }

        /// <summary>Sets the texture image for the shape currently being built. Only meaningful for Triangles/TriangleStrip/TriangleFan/Quads/QuadStrip — see the class remark above. Cleared automatically by the next BeginShape().</summary>
        public void Texture(PImage img) => _shapeTexture = img;

        /// <summary>Clears a texture set by Texture() for the current shape.</summary>
        public void NoTexture() => _shapeTexture = null;

        /// <summary>Adds a straight-line vertex. Its color is captured from the current Fill() at the moment this is called — set a different fill before each vertex to get a Gouraud-shaded (per-vertex-colored) mesh on the shape kinds that support it.</summary>
        public void Vertex(float x, float y)
        {
            RecordVertexColorAndUv(new SKPoint(0, 0));
            if (_shapeKind == ShapeKind.Polygon)
            {
                if (_shapePath == null)
                    throw new InvalidOperationException("Vertex() llamado sin BeginShape().");
                if (_shapePath.PointCount == 0)
                    _shapePath.MoveTo(x, y);
                else
                    _shapePath.LineTo(x, y);
            }
            else
            {
                _shapeVertices.Add(new SKPoint(x, y));
            }
        }

        /// <summary>Adds a vertex with explicit texture coordinates (u, v in 0-1, relative to the image set via Texture()), like Processing's vertex(x, y, u, v). Only valid for Triangles/TriangleStrip/TriangleFan/Quads/QuadStrip — Polygon can't mix in per-vertex texture data because of its curved-segment support.</summary>
        public void Vertex(float x, float y, float u, float v)
        {
            if (_shapeKind == ShapeKind.Polygon)
                throw new InvalidOperationException("Vertex(x, y, u, v) no es válido en el modo Polygon por defecto de BeginShape(); usa uno de los modos basados en triángulos/cuadriláteros.");
            RecordVertexColorAndUv(new SKPoint(u, v));
            _shapeVertices.Add(new SKPoint(x, y));
        }

        private void RecordVertexColorAndUv(SKPoint uv)
        {
            _shapeVertexColors.Add(_fillPaint.Color);
            _shapeVertexUVs.Add(uv);
        }

        public void BezierVertex(float cx1, float cy1, float cx2, float cy2, float x, float y)
        {
            if (_shapeKind != ShapeKind.Polygon || _shapePath == null)
                throw new InvalidOperationException("BezierVertex() solo es válido en el modo Polygon por defecto de BeginShape().");
            _shapePath.CubicTo(cx1, cy1, cx2, cy2, x, y);
        }

        public void QuadraticVertex(float cx, float cy, float x, float y)
        {
            if (_shapeKind != ShapeKind.Polygon || _shapePath == null)
                throw new InvalidOperationException("QuadraticVertex() solo es válido en el modo Polygon por defecto de BeginShape().");
            _shapePath.QuadTo(cx, cy, x, y);
        }

        /// <summary>
        /// Adds a Catmull-Rom curve vertex, like Processing's curveVertex().
        /// As in Processing, the curve only actually passes through the
        /// *interior* points: the very first and very last curveVertex()
        /// calls in a shape are used solely to shape the tangent at each end
        /// and aren't drawn to themselves — nothing appears until the 4th
        /// call, and the common pattern is to repeat the first point (and
        /// the last point) once so the curve visibly starts/ends there.
        /// Respects CurveTightness() the same way the standalone Curve()
        /// does. Only valid in the default Polygon mode of BeginShape(), same
        /// as BezierVertex()/QuadraticVertex().
        /// </summary>
        public void CurveVertex(float x, float y)
        {
            if (_shapeKind != ShapeKind.Polygon || _shapePath == null)
                throw new InvalidOperationException("CurveVertex() solo es válido en el modo Polygon por defecto de BeginShape().");

            _curveVertexPoints.Add(new SKPoint(x, y));
            int n = _curveVertexPoints.Count;
            if (n < 4)
                return; // hacen falta 4 puntos (p0..p3) para trazar el primer segmento p1->p2

            var p0 = _curveVertexPoints[n - 4];
            var p1 = _curveVertexPoints[n - 3];
            var p2 = _curveVertexPoints[n - 2];
            var p3 = _curveVertexPoints[n - 1];

            if (_shapePath.PointCount == 0)
                _shapePath.MoveTo(p1);

            float s = (1f - _curveTightness) / 6f;
            float cx1 = p1.X + s * (p2.X - p0.X);
            float cy1 = p1.Y + s * (p2.Y - p0.Y);
            float cx2 = p2.X - s * (p3.X - p1.X);
            float cy2 = p2.Y - s * (p3.Y - p1.Y);
            _shapePath.CubicTo(cx1, cy1, cx2, cy2, p2.X, p2.Y);
        }

        /// <summary>Finishes and draws the shape. `close` only applies to Polygon mode — it's ignored for every other ShapeKind, which define their own closedness.</summary>
        public void EndShape(bool close = false)
        {
            EnsureReady();
            switch (_shapeKind)
            {
                case ShapeKind.Polygon:
                    EndShapePolygon(close);
                    break;
                case ShapeKind.Points:
                    Canvas.DrawPoints(SKPointMode.Points, _shapeVertices.ToArray(), _strokePaint);
                    break;
                case ShapeKind.Lines:
                    DrawGrouped(_shapeVertices, 2, closedAndFillable: false);
                    break;
                case ShapeKind.Triangles:
                    if (ShapeNeedsMesh())
                        DrawMesh();
                    else
                        DrawGrouped(_shapeVertices, 3, closedAndFillable: true);
                    break;
                case ShapeKind.Quads:
                    if (ShapeNeedsMesh())
                        DrawMesh();
                    else
                        DrawGrouped(_shapeVertices, 4, closedAndFillable: true);
                    break;
                case ShapeKind.TriangleFan:
                    if (ShapeNeedsMesh())
                        DrawMesh();
                    else
                        DrawFanOrStrip(_shapeVertices, fan: true);
                    break;
                case ShapeKind.TriangleStrip:
                    if (ShapeNeedsMesh())
                        DrawMesh();
                    else
                        DrawFanOrStrip(_shapeVertices, fan: false);
                    break;
                case ShapeKind.QuadStrip:
                    if (ShapeNeedsMesh())
                        DrawMesh();
                    else
                        DrawQuadStrip(_shapeVertices);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            _shapePath?.Dispose();
            _shapePath = null;
            _shapeVertices.Clear();
            _shapeVertexColors.Clear();
            _shapeVertexUVs.Clear();
        }

        private bool ShapeNeedsMesh()
        {
            if (_shapeTexture != null)
                return true;
            if (_shapeVertexColors.Count == 0)
                return false;
            var first = _shapeVertexColors[0];
            foreach (var c in _shapeVertexColors)
                if (c != first)
                    return true;
            return false;
        }

        private void EndShapePolygon(bool close)
        {
            if (_shapePath == null)
                throw new InvalidOperationException("EndShape() llamado sin BeginShape().");
            if (close)
                _shapePath.Close();
            if (_fillEnabled)
                Canvas.DrawPath(_shapePath, _fillPaint);
            if (_strokeEnabled)
                Canvas.DrawPath(_shapePath, _strokePaint);
        }

        private void DrawGrouped(List<SKPoint> verts, int groupSize, bool closedAndFillable)
        {
            for (int i = 0; i + groupSize <= verts.Count; i += groupSize)
            {
                using var path = new SKPath();
                path.MoveTo(verts[i]);
                for (int j = 1; j < groupSize; j++)
                    path.LineTo(verts[i + j]);
                if (closedAndFillable)
                    path.Close();

                if (closedAndFillable && _fillEnabled)
                    Canvas.DrawPath(path, _fillPaint);
                if (_strokeEnabled)
                    Canvas.DrawPath(path, _strokePaint);
            }
        }

        /// <summary>Each triangle/quad in a fan/strip is stroked as its own closed path, so shared internal edges get drawn twice when a stroke is active — cosmetically harmless for solid colors, worth knowing for semi-transparent strokes.</summary>
        private void DrawFanOrStrip(List<SKPoint> verts, bool fan)
        {
            if (verts.Count < 3)
                return;
            for (int i = 1; i + 1 < verts.Count; i++)
            {
                var a = fan ? verts[0] : verts[i - 1];
                var b = verts[i];
                var c = verts[i + 1];
                using var path = new SKPath();
                path.MoveTo(a);
                path.LineTo(b);
                path.LineTo(c);
                path.Close();
                if (_fillEnabled)
                    Canvas.DrawPath(path, _fillPaint);
                if (_strokeEnabled)
                    Canvas.DrawPath(path, _strokePaint);
            }
        }

        private void DrawQuadStrip(List<SKPoint> verts)
        {
            for (int i = 0; i + 3 < verts.Count; i += 2)
            {
                using var path = new SKPath();
                path.MoveTo(verts[i]);
                path.LineTo(verts[i + 1]);
                path.LineTo(verts[i + 3]);
                path.LineTo(verts[i + 2]);
                path.Close();
                if (_fillEnabled)
                    Canvas.DrawPath(path, _fillPaint);
                if (_strokeEnabled)
                    Canvas.DrawPath(path, _strokePaint);
            }
        }

        /// <summary>Turns _shapeKind's vertex list into flat (v0,v1,v2) triangle index triples, mirroring the grouping DrawGrouped/DrawFanOrStrip/DrawQuadStrip already use for the path-based rendering above — kept in sync with those by design.</summary>
        private static List<(int a, int b, int c)> BuildTriangleIndices(ShapeKind kind, int vertexCount)
        {
            var tris = new List<(int a, int b, int c)>();
            switch (kind)
            {
                case ShapeKind.Triangles:
                    for (int i = 0; i + 3 <= vertexCount; i += 3)
                        tris.Add((i, i + 1, i + 2));
                    break;
                case ShapeKind.Quads:
                    for (int i = 0; i + 4 <= vertexCount; i += 4)
                    { tris.Add((i, i + 1, i + 2)); tris.Add((i, i + 2, i + 3)); }
                    break;
                case ShapeKind.TriangleFan:
                    for (int i = 1; i + 1 < vertexCount; i++)
                        tris.Add((0, i, i + 1));
                    break;
                case ShapeKind.TriangleStrip:
                    for (int i = 0; i + 2 < vertexCount; i++)
                        tris.Add((i, i + 1, i + 2));
                    break;
                case ShapeKind.QuadStrip:
                    for (int i = 0; i + 3 < vertexCount; i += 2)
                    { tris.Add((i, i + 1, i + 3)); tris.Add((i, i + 3, i + 2)); }
                    break;
            }
            return tris;
        }

        private void DrawMesh()
        {
            var tris = BuildTriangleIndices(_shapeKind, _shapeVertices.Count);
            if (tris.Count == 0)
                return;

            var positions = new SKPoint[tris.Count * 3];
            var colors = new SKColor[tris.Count * 3];
            SKPoint[]? texs = _shapeTexture != null ? new SKPoint[tris.Count * 3] : null;

            int k = 0;
            foreach (var (a, b, c) in tris)
            {
                foreach (var idx in new[] { a, b, c })
                {
                    positions[k] = _shapeVertices[idx];
                    colors[k] = _shapeVertexColors[idx];
                    // SKVertices expects texture coords in the shader's local
                    // (pixel) space, not the 0-1 UV range Vertex(x,y,u,v)
                    // takes — scale up to the texture's actual pixel size.
                    if (texs != null)
                        texs[k] = new SKPoint(_shapeVertexUVs[idx].X * _shapeTexture!.Width, _shapeVertexUVs[idx].Y * _shapeTexture.Height);
                    k++;
                }
            }

            using var vertices = SKVertices.CreateCopy(SKVertexMode.Triangles, positions, texs, colors);
            using var meshPaint = new SKPaint { IsAntialias = true };
            if (_shapeTexture != null)
                meshPaint.Shader = SKShader.CreateBitmap(_shapeTexture.Bitmap, SKShaderTileMode.Clamp, SKShaderTileMode.Clamp);

            // Modulate multiplies the (optional) texture shader's color by
            // each vertex's color; with no shader, the paint's own color
            // (white, unset above) makes this a pure passthrough of the
            // vertex colors — so Modulate works correctly for both the pure
            // Gouraud case and the textured case.
            Canvas.DrawVertices(vertices, SKBlendMode.Modulate, meshPaint);
        }

        // =====================================================================
        // Text
        // =====================================================================

        private TextAlignH _textAlignH = TextAlignH.Left;
        private TextAlignV _textAlignV = TextAlignV.Baseline;
        private float? _textLeading;

        public void TextSize(float size) => _textPaint.TextSize = size;

        public void TextAlign(TextAlignH horiz, TextAlignV vert = TextAlignV.Baseline)
        {
            _textAlignH = horiz;
            _textAlignV = vert;
        }

        public void TextLeading(float leading) => _textLeading = leading;

        public float TextWidth(string text)
        {
            float widest = 0f;
            foreach (var line in text.Split('\n'))
                widest = Math.Max(widest, _textPaint.MeasureText(line));
            return widest;
        }

        public float TextAscent() => -_textPaint.FontMetrics.Ascent;
        public float TextDescent() => _textPaint.FontMetrics.Descent;

        // Typeface loaded directly by path via TextFont(string, ...) — this
        // class created it, so this class owns and disposes it. A typeface
        // set via TextFont(PFont, ...) is owned by that PFont instead (it
        // disposes its own typeface) and is deliberately left alone here —
        // otherwise switching between the two ways of setting a font could
        // dispose a typeface the caller's PFont is still holding onto.
        private SKTypeface? _ownedTypeface;

        /// <summary>Sets the current font by loading a font file directly (.ttf/.otf/etc.), like a path-based shortcut for Processing's textFont(). For a font you'll reuse or that you loaded once via LoadFont()/CreateFont(), prefer the TextFont(PFont, ...) overload below instead.</summary>
        public void TextFont(string? path, float? size = null)
        {
            _ownedTypeface?.Dispose();
            _ownedTypeface = path == null ? null : SKTypeface.FromFile(path)
                ?? throw new InvalidOperationException($"No se pudo cargar la fuente: '{path}'. Verifica la ruta y el formato.");
            _textPaint.Typeface = _ownedTypeface;
            if (size.HasValue)
                _textPaint.TextSize = size.Value;
        }

        /// <summary>Sets the current font from a PFont previously obtained via LoadFont()/CreateFont(), like Processing's textFont(font, size). If size is omitted, uses the size the PFont was created/loaded at.</summary>
        public void TextFont(PFont font, float? size = null)
        {
            _textPaint.Typeface = font.Typeface;
            _textPaint.TextSize = size ?? font.Size;
        }

        /// <summary>Draws text at (x, y), per the current TextAlign. Multi-line strings ('\n') are split and spaced using the current TextLeading.</summary>
        public void Text(string text, float x, float y)
        {
            EnsureReady();
            var lines = text.Split('\n');
            float leading = _textLeading ?? _textPaint.FontSpacing;

            for (int i = 0; i < lines.Length; i++)
            {
                float lineY = y + i * leading;
                float lineX = ResolveTextX(x, lines[i]);
                Canvas.DrawText(lines[i], lineX, ResolveTextY(lineY), _textPaint);
            }
        }

        private float ResolveTextX(float x, string line) => _textAlignH switch
        {
            TextAlignH.Left => x,
            TextAlignH.Center => x - _textPaint.MeasureText(line) / 2f,
            TextAlignH.Right => x - _textPaint.MeasureText(line),
            _ => throw new ArgumentOutOfRangeException()
        };

        private float ResolveTextY(float y) => _textAlignV switch
        {
            TextAlignV.Baseline => y,
            TextAlignV.Top => y + TextAscent(),
            TextAlignV.Bottom => y - TextDescent(),
            TextAlignV.Center => y + (TextAscent() - TextDescent()) / 2f,
            _ => throw new ArgumentOutOfRangeException()
        };

        // =====================================================================
        // Gradients
        // =====================================================================

        public void LinearGradientFill(float x0, float y0, float x1, float y1, Color c0, Color c1)
        {
            _fillEnabled = true;
            _fillPaint.Shader = SKShader.CreateLinearGradient(
                new SKPoint(x0, y0), new SKPoint(x1, y1), new[] { c0.Skia, c1.Skia }, null, SKShaderTileMode.Clamp);
        }

        public void RadialGradientFill(float cx, float cy, float radius, Color cCenter, Color cEdge)
        {
            _fillEnabled = true;
            _fillPaint.Shader = SKShader.CreateRadialGradient(
                new SKPoint(cx, cy), radius, new[] { cCenter.Skia, cEdge.Skia }, null, SKShaderTileMode.Clamp);
        }

        // =====================================================================
        // Transformations
        // =====================================================================

        public void PushMatrix() { EnsureReady(); Canvas.Save(); }
        public void PopMatrix() { EnsureReady(); Canvas.Restore(); }
        public void Translate(float x, float y) { EnsureReady(); Canvas.Translate(x, y); }

        /// <summary>Rotates in degrees — a deliberate deviation from Processing's radians-based rotate(), since degrees maps directly to SkiaSharp's RotateDegrees.</summary>
        public void Rotate(float degrees) { EnsureReady(); Canvas.RotateDegrees(degrees); }

        public void Scale(float sx, float? sy = null) { EnsureReady(); Canvas.Scale(sx, sy ?? sx); }

        /// <summary>Shears drawing along the x-axis by angleRadians, like Processing's shearX().</summary>
        public void ShearX(float angleRadians) { EnsureReady(); Canvas.Skew(MathF.Tan(angleRadians), 0); }

        /// <summary>Shears drawing along the y-axis by angleRadians, like Processing's shearY().</summary>
        public void ShearY(float angleRadians) { EnsureReady(); Canvas.Skew(0, MathF.Tan(angleRadians)); }

        /// <summary>Replaces the current transformation matrix with the identity, discarding any Translate/Rotate/Scale/Shear/ApplyMatrix applied so far (including ones from before the innermost PushMatrix()) — like Processing's resetMatrix(). Note this is stronger than PopMatrix(): PopMatrix() only undoes back to the last PushMatrix(), while ResetMatrix() clears everything.</summary>
        public void ResetMatrix() { EnsureReady(); Canvas.ResetMatrix(); }

        /// <summary>Multiplies the current transformation matrix by an arbitrary affine matrix [[a c e][b d f][0 0 1]], like Processing's applyMatrix(). Useful for effects (e.g. a custom skew/warp) the named Translate/Rotate/Scale/Shear calls can't express directly.</summary>
        public void ApplyMatrix(float a, float b, float c, float d, float e, float f)
        {
            EnsureReady();
            var m = new SKMatrix { ScaleX = a, SkewY = b, SkewX = c, ScaleY = d, TransX = e, TransY = f, Persp2 = 1 };
            Canvas.Concat(ref m); // SkiaSharp moderno recibe SKMatrix por "in"; si tu versión de SkiaSharp es más antigua y espera "ref SKMatrix", cambia esta línea a Canvas.Concat(ref m).
        }

        /// <summary>Prints the current transformation matrix to the console as a 3x3 row-major matrix, like Processing's printMatrix() — mainly a debugging aid for figuring out what a chain of Translate/Rotate/Scale calls actually produced.</summary>
        public void PrintMatrix()
        {
            EnsureReady();
            var m = Canvas.TotalMatrix;
            DanaLogger.Info($"[{m.ScaleX,8:0.####} {m.SkewX,8:0.####} {m.TransX,8:0.####}]");
            DanaLogger.Info($"[{m.SkewY,8:0.####} {m.ScaleY,8:0.####} {m.TransY,8:0.####}]");
            DanaLogger.Info($"[{0,8:0.####} {0,8:0.####} {1,8:0.####}]");
        }

        // =====================================================================
        // Compositing other images/buffers/vector shapes onto this one
        // =====================================================================

        public void Image(PImage img, float x, float y) => Image(img, x, y, img.Width, img.Height);

        /// <summary>Draws img with (x, y, w, h) interpreted per the current ImageMode — CORNER by default, so this behaves exactly as before unless ImageMode() has been called.</summary>
        public void Image(PImage img, float x, float y, float w, float h)
        {
            EnsureReady();
            var rect = ResolveRectMode(_imageMode, x, y, w, h);
            using var tintPaint = BuildTintPaint();
            Canvas.DrawBitmap(img.Bitmap, rect, tintPaint);
        }

        public void Image(PGraphics pg, float x, float y) => Image(pg, x, y, pg.Width, pg.Height);

        /// <summary>Draws pg's current contents with (x, y, w, h) interpreted per the current ImageMode — see the PImage overload above.</summary>
        public void Image(PGraphics pg, float x, float y, float w, float h)
        {
            EnsureReady();
            var rect = ResolveRectMode(_imageMode, x, y, w, h);
            using var snapshot = pg.SnapshotForDraw();
            using var tintPaint = BuildTintPaint();
            Canvas.DrawImage(snapshot, rect, tintPaint);
        }

        /// <summary>Draws a loaded vector shape at (x, y) using its natural size, positioned/sized per the current ShapeMode.</summary>
        public void Shape(PShape shape, float x, float y) => Shape(shape, x, y, shape.Width, shape.Height);

        /// <summary>
        /// Draws a loaded vector shape, with (x, y, w, h) interpreted per the
        /// current ShapeMode — same four-argument convention Rect/Ellipse use,
        /// reusing ResolveRectMode. Note: Tint() does not affect Shape() —
        /// SVG pictures are drawn via DrawPicture, which doesn't accept a
        /// color-filter paint the way DrawBitmap does. If you need a tinted
        /// vector shape, render it into a PGraphics first and Image() that
        /// buffer instead.
        /// </summary>
        public void Shape(PShape shape, float x, float y, float w, float h)
        {
            EnsureReady();
            var rect = ResolveRectMode(_shapeDrawMode, x, y, w, h);
            Canvas.Save();
            Canvas.Translate(rect.Left, rect.Top);
            if (shape.Width > 0 && shape.Height > 0)
                Canvas.Scale(rect.Width / shape.Width, rect.Height / shape.Height);
            Canvas.DrawPicture(shape.Picture);
            Canvas.Restore();
        }

        /// <summary>Creates a new offscreen drawing buffer, like Processing's createGraphics(w, h). Available here (not just on Sketch) so buffers can nest.</summary>
        public PGraphics CreateGraphics(int w, int h) => new PGraphics(w, h);

        /// <summary>Loads a vector shape from an SVG file, like Processing's loadShape(). Requires the SkiaSharp.Extended.Svg NuGet package.</summary>
        public PShape LoadShape(string path) => PShape.LoadSvg(path);

        // =====================================================================
        // Pixels — https://processing.org/reference/loadPixels_.html and
        // siblings (pixels[], updatePixels(), get(), set(), copy(), blend(),
        // filter()). Reading pixels back requires a CPU-readable Surface,
        // same requirement as Save() below — a GPU-accelerated host would
        // need its own readback path instead of these.
        // =====================================================================

        private Color[]? _pixels;

        /// <summary>Reads this canvas's pixels into the Pixels array so they can be inspected/modified directly, like Processing's loadPixels(). Call UpdatePixels() afterward to push any edits back to the canvas — Pixels itself is just an in-memory snapshot until then.</summary>
        public void LoadPixels()
        {
            EnsureReady();
            using var pixmap = PeekPixelsOrThrow();
            var pixels = new Color[Width * Height];
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                    pixels[y * Width + x] = new Color(pixmap.GetPixelColor(x, y));
            _pixels = pixels;
        }

        /// <summary>Pixel data loaded by LoadPixels(), indexed row-major (index = y * Width + x), like Processing's pixels[] array. Throws if LoadPixels() hasn't been called (or was called before the canvas was resized) — this deliberately doesn't auto-load, so you always know whether you're looking at a fresh read or edits you made since.</summary>
        public Color[] Pixels => _pixels ?? throw new InvalidOperationException("Pixels no está disponible — llama a LoadPixels() primero.");

        /// <summary>Writes the Pixels array back onto the canvas, like Processing's updatePixels(). Pixels are copied verbatim (SKBlendMode.Src) — no blending with what was there before, matching Processing.</summary>
        public void UpdatePixels()
        {
            EnsureReady();
            if (_pixels == null)
                throw new InvalidOperationException("UpdatePixels() llamado sin un LoadPixels() previo.");
            var info = new SKImageInfo(Width, Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
            using var bitmap = new SKBitmap(info);
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                    bitmap.SetPixel(x, y, _pixels[y * Width + x].Skia);
            using var image = SKImage.FromBitmap(bitmap);
            using var paint = new SKPaint { BlendMode = SKBlendMode.Src, IsAntialias = false };
            Canvas.DrawImage(image, 0, 0, paint);
        }

        /// <summary>Reads the color at (x, y) straight from the canvas — no LoadPixels() needed, like Processing's get(x, y). Out-of-bounds coordinates return transparent black, matching Processing.</summary>
        public Color Get(int x, int y)
        {
            EnsureReady();
            if (x < 0 || y < 0 || x >= Width || y >= Height)
                return new Color(SKColors.Transparent);
            using var pixmap = PeekPixelsOrThrow();
            return new Color(pixmap.GetPixelColor(x, y));
        }

        /// <summary>Grabs a rectangular region of the canvas as a standalone PImage, like Processing's get(x, y, w, h). Coordinates that fall outside the canvas come back transparent rather than throwing, matching Processing's own tolerant behavior here.</summary>
        public PImage Get(int x, int y, int w, int h)
        {
            EnsureReady();
            using var pixmap = PeekPixelsOrThrow();
            var info = new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Unpremul);
            var bitmap = new SKBitmap(info);
            for (int j = 0; j < h; j++)
            {
                int sy = y + j;
                for (int i = 0; i < w; i++)
                {
                    int sx = x + i;
                    var color = (sx >= 0 && sy >= 0 && sx < Width && sy < Height) ? pixmap.GetPixelColor(sx, sy) : SKColors.Transparent;
                    bitmap.SetPixel(i, j, color);
                }
            }
            return new PImage(bitmap);
        }

        /// <summary>Grabs the entire canvas as a standalone PImage, like Processing's no-argument get().</summary>
        public PImage Get() => Get(0, 0, Width, Height);

        /// <summary>Sets a single pixel's color, like Processing's set(x, y, color). Out-of-bounds coordinates are silently ignored, matching Processing. Writes verbatim (no blending with the existing pixel) — same as UpdatePixels().</summary>
        public void Set(int x, int y, Color c)
        {
            EnsureReady();
            if (x < 0 || y < 0 || x >= Width || y >= Height)
                return;
            using var paint = new SKPaint { Color = c.Skia, BlendMode = SKBlendMode.Src };
            Canvas.DrawPoint(x, y, paint);
        }

        /// <summary>Draws img at (x, y) with pixels copied verbatim — no tint, no blending — like Processing's set(x, y, img).</summary>
        public void Set(int x, int y, PImage img)
        {
            EnsureReady();
            using var paint = new SKPaint { BlendMode = SKBlendMode.Src };
            Canvas.DrawBitmap(img.Bitmap, x, y, paint);
        }

        /// <summary>Copies a region of src, scaling it if the source and destination sizes differ, into this canvas — pixels replace whatever was there (no blending), like Processing's copy(img, sx, sy, sw, sh, dx, dy, dw, dh).</summary>
        public void Copy(PImage src, int sx, int sy, int sw, int sh, int dx, int dy, int dw, int dh)
        {
            EnsureReady();
            var srcRect = SKRect.Create(sx, sy, sw, sh);
            var dstRect = SKRect.Create(dx, dy, dw, dh);
            using var paint = new SKPaint { BlendMode = SKBlendMode.Src, IsAntialias = true };
            Canvas.DrawBitmap(src.Bitmap, srcRect, dstRect, paint);
        }

        /// <summary>Copies a region of this same canvas to another location on itself, like Processing's copy(sx, sy, sw, sh, dx, dy, dw, dh) called without a source image.</summary>
        public void Copy(int sx, int sy, int sw, int sh, int dx, int dy, int dw, int dh)
        {
            using var region = Get(sx, sy, sw, sh);
            Copy(region, 0, 0, sw, sh, dx, dy, dw, dh);
        }

        /// <summary>Draws a region of src into this canvas using the given blend mode, scaling if the sizes differ, like Processing's blend(img, sx, sy, sw, sh, dx, dy, dw, dh, MODE).</summary>
        public void Blend(PImage src, int sx, int sy, int sw, int sh, int dx, int dy, int dw, int dh, BlendModeKind mode)
        {
            EnsureReady();
            var srcRect = SKRect.Create(sx, sy, sw, sh);
            var dstRect = SKRect.Create(dx, dy, dw, dh);
            using var paint = new SKPaint { BlendMode = ResolveBlendMode(mode), IsAntialias = true };
            Canvas.DrawBitmap(src.Bitmap, srcRect, dstRect, paint);
        }

        /// <summary>Blends a region of this same canvas onto another location on itself, like Processing's blend(sx, sy, sw, sh, dx, dy, dw, dh, MODE) called without a source image.</summary>
        public void Blend(int sx, int sy, int sw, int sh, int dx, int dy, int dw, int dh, BlendModeKind mode)
        {
            using var region = Get(sx, sy, sw, sh);
            Blend(region, 0, 0, sw, sh, dx, dy, dw, dh, mode);
        }

        /// <summary>
        /// Applies an image filter to the whole canvas in place, like
        /// Processing's filter(kind) / filter(kind, param). `param` means
        /// different things per kind and is ignored where not applicable:
        /// Threshold (0-1 cutoff, default 0.5), Posterize (number of levels
        /// per channel, minimum 2), Blur (pixel radius). Gray/Invert/Opaque
        /// ignore it entirely.
        /// </summary>
        public void Filter(FilterKind kind, float param = 0.5f)
        {
            EnsureReady();
            if (kind == FilterKind.Blur)
            {
                using var snapshot = Get();
                using var blurPaint = new SKPaint { ImageFilter = SKImageFilter.CreateBlur(param, param), BlendMode = SKBlendMode.Src };
                Canvas.DrawBitmap(snapshot.Bitmap, 0, 0, blurPaint);
                return;
            }
            if (kind == FilterKind.Erode || kind == FilterKind.Dilate)
                throw new NotSupportedException($"Filter({kind}) todavía no está implementado.");

            LoadPixels();
            var pixels = Pixels;
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = ApplyPixelFilter(kind, pixels[i], param);
            UpdatePixels();
        }

        private static Color ApplyPixelFilter(FilterKind kind, Color c, float param) => kind switch
        {
            FilterKind.Gray => Grayscale(c),
            FilterKind.Invert => new Color((byte)(255 - c.R), (byte)(255 - c.G), (byte)(255 - c.B), c.A),
            FilterKind.Threshold => Threshold(c, param),
            FilterKind.Posterize => Posterize(c, (int)Math.Max(2, param)),
            FilterKind.Opaque => new Color(c.R, c.G, c.B, 255),
            _ => c
        };

        private static Color Grayscale(Color c)
        {
            byte g = (byte)Math.Clamp(c.R * 0.299f + c.G * 0.587f + c.B * 0.114f, 0, 255);
            return new Color(g, g, g, c.A);
        }

        private static Color Threshold(Color c, float level)
        {
            byte gray = (byte)Math.Clamp(c.R * 0.299f + c.G * 0.587f + c.B * 0.114f, 0, 255);
            byte v = gray < level * 255 ? (byte)0 : (byte)255;
            return new Color(v, v, v, c.A);
        }

        private static Color Posterize(Color c, int levels)
        {
            byte P(byte v) => (byte)Math.Clamp(MathF.Round(v / 255f * (levels - 1)) / (levels - 1) * 255f, 0, 255);
            return new Color(P(c.R), P(c.G), P(c.B), c.A);
        }

        /// <summary>Shared precondition + error message for every pixel-reading method above — requires a CPU-readable Surface, same as Save().</summary>
        private SKPixmap PeekPixelsOrThrow()
        {
            if (Surface == null)
                throw new InvalidOperationException(
                    "No se pueden leer píxeles: no hay una superficie (SKSurface) asociada a este canvas. " +
                    "Esto puede pasar si el host que ejecuta el sketch no proporcionó una superficie respaldada por CPU a SetCanvas().");
            var pixmap = Surface.PeekPixels();
            if (pixmap == null)
                throw new InvalidOperationException(
                    "No se pudieron leer los píxeles de la superficie — esto requiere una superficie respaldada por CPU (raster), no una acelerada por GPU.");
            return pixmap;
        }

        // =====================================================================
        // Saving output — https://processing.org/reference/save_.html.
        // SaveFrame() (with frame-number substitution) lives on Sketch, since
        // it needs Sketch.FrameCount — but it delegates to Save() here once
        // the filename's resolved, so PGraphics buffers get the same
        // encoding/writing logic for free.
        // =====================================================================

        /// <summary>
        /// Saves the current contents of this canvas to an image file, like
        /// Processing's save(). The format is chosen from the file
        /// extension: .png, .jpg/.jpeg, .bmp, or .webp. `quality` (0-100)
        /// only affects the lossy formats (jpg/webp) — ignored for png/bmp.
        ///
        /// Works by reading back the pixel buffer of Surface (not Canvas —
        /// SKCanvas itself has no pixel-readback API; the buffer belongs to
        /// whichever SKSurface owns the canvas). This only succeeds for a
        /// CPU/raster-backed surface — true for every PGraphics (always
        /// raster, see its constructor) and for Sketch as long as its host
        /// passes a software-rendered surface to SetCanvas(). A
        /// GPU-accelerated host would need an explicit GPU readback path
        /// instead of this method.
        /// </summary>
        public void Save(string path, int quality = 100)
        {
            EnsureReady();

            if (Surface == null)
                throw new InvalidOperationException(
                    "No se pudo guardar: no hay una superficie (SKSurface) asociada a este canvas. " +
                    "Esto puede pasar si el host que ejecuta el sketch no proporcionó una superficie respaldada por CPU a SetCanvas().");

            using var pixmap = Surface.PeekPixels();
            if (pixmap == null)
                throw new InvalidOperationException(
                    "No se pudo leer los píxeles de la superficie para guardar — esto requiere una superficie respaldada por CPU (raster), no una acelerada por GPU.");

            var format = ResolveImageFormat(path);
            using var image = SKImage.FromPixels(pixmap);
            using var data = image.Encode(format, quality);
            if (data == null)
                throw new InvalidOperationException($"No se pudo codificar la imagen para '{path}'.");

            var dir = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            using var stream = File.OpenWrite(path);
            data.SaveTo(stream);
        }

        private static SKEncodedImageFormat ResolveImageFormat(string path) => Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".png" => SKEncodedImageFormat.Png,
            ".jpg" or ".jpeg" => SKEncodedImageFormat.Jpeg,
            ".bmp" => SKEncodedImageFormat.Bmp,
            ".webp" => SKEncodedImageFormat.Webp,
            _ => throw new NotSupportedException($"Formato de imagen no soportado para '{path}'. Usa .png, .jpg/.jpeg, .bmp o .webp.")
        };

        // =====================================================================
        // Disposal
        // =====================================================================

        public virtual void Dispose()
        {
            if (_disposed)
                return;
            _fillPaint.Dispose();
            _strokePaint.Dispose();
            _ownedTypeface?.Dispose(); // NO tocar _textPaint.Typeface directamente: si el font activo vino de TextFont(PFont), ese typeface pertenece al PFont, no a este GraphicsContext.
            _textPaint.Dispose();
            _shapePath?.Dispose();
            _disposed = true;
        }
    }
}