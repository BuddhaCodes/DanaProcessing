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
        private SKColor? _tintColor; // null = sin tinte, Image() dibuja el bitmap sin modificar
        private float _curveTightness = 0f;

        /// <summary>
        /// Sets how Fill/Stroke/Background interpret their arguments from here
        /// on: RGB (each 0-255) or HSB (hue 0-360, saturation/brightness 0-100)
        /// — matches Processing's own colorMode(). Switching modes doesn't
        /// recolor anything already drawn or already set via a previous
        /// Fill()/Stroke() call — only calls made after this one are affected.
        /// </summary>
        public void ColorMode(ColorSpaceMode mode) => _colorMode = mode;

        public void RectMode(ShapeAlignMode mode) => _rectMode = mode;
        public void EllipseMode(ShapeAlignMode mode) => _ellipseMode = mode;

        /// <summary>Sets how Shape()'s x/y/w/h are interpreted — CORNER (default, x,y is top-left), CORNERS (x,y and w,h are two opposite corners, not a size), or CENTER. Matches Processing's shapeMode(). RADIUS isn't meaningful for shapeMode in Processing either, so it isn't supported here.</summary>
        public void ShapeMode(ShapeAlignMode mode) => _shapeDrawMode = mode;

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
        private SKColor ResolveColor(float a1, float a2, float a3, byte alpha) =>
            _colorMode == ColorSpaceMode.HSB
                ? SKColor.FromHsv(a1, a2, a3, alpha)
                : new SKColor(ClampByte(a1), ClampByte(a2), ClampByte(a3), alpha);

        private static byte ClampByte(float v) => (byte)Math.Clamp(v, 0, 255);

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
        public void Background(float a1, float a2, float a3)
        {
            EnsureReady();
            using var paint = new SKPaint { Color = ResolveColor(a1, a2, a3, 255), Style = SKPaintStyle.Fill };
            Canvas.DrawRect(new SKRect(0, 0, Width, Height), paint);
        }

        /// <summary>Clears this surface to fully transparent. Most useful on a PGraphics you intend to composite over other content via Image().</summary>
        public void Clear()
        {
            EnsureReady();
            Canvas.Clear(SKColors.Transparent);
        }

        /// <summary>Sets the fill color, interpreted per the current ColorMode (RGB by default).</summary>
        public void Fill(float a1, float a2, float a3, byte alpha = 255)
        {
            _fillEnabled = true;
            _fillPaint.Shader = null; // plain fill overrides any gradient set earlier
            _fillPaint.Color = ResolveColor(a1, a2, a3, alpha);
            _textPaint.Color = _fillPaint.Color;
        }

        /// <summary>Fill using Hue (0-360), Saturation (0-100), Brightness (0-100) — regardless of the current ColorMode.</summary>
        public void FillHSB(float h, float s, float br, byte a = 255)
        {
            _fillEnabled = true;
            _fillPaint.Shader = null;
            _fillPaint.Color = SKColor.FromHsv(h, s, br, a);
            _textPaint.Color = _fillPaint.Color;
        }

        public void NoFill() => _fillEnabled = false;

        /// <summary>Sets the stroke color, interpreted per the current ColorMode (RGB by default).</summary>
        public void Stroke(float a1, float a2, float a3, byte alpha = 255)
        {
            _strokeEnabled = true;
            _strokePaint.Color = ResolveColor(a1, a2, a3, alpha);
        }

        /// <summary>Stroke using Hue (0-360), Saturation (0-100), Brightness (0-100) — regardless of the current ColorMode.</summary>
        public void StrokeHSB(float h, float s, float br, byte a = 255)
        {
            _strokeEnabled = true;
            _strokePaint.Color = SKColor.FromHsv(h, s, br, a);
        }

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
        private PImage? _shapeTexture;

        public void BeginShape(ShapeKind kind = ShapeKind.Polygon)
        {
            _shapeKind = kind;
            _shapePath = kind == ShapeKind.Polygon ? new SKPath() : null;
            _shapeVertices.Clear();
            _shapeVertexColors.Clear();
            _shapeVertexUVs.Clear();
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

        public void TextFont(string? path, float? size = null)
        {
            _textPaint.Typeface?.Dispose();
            _textPaint.Typeface = path == null ? null : SKTypeface.FromFile(path)
                ?? throw new InvalidOperationException($"No se pudo cargar la fuente: '{path}'. Verifica la ruta y el formato.");
            if (size.HasValue)
                _textPaint.TextSize = size.Value;
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

        // =====================================================================
        // Compositing other images/buffers/vector shapes onto this one
        // =====================================================================

        public void Image(PImage img, float x, float y) => Image(img, x, y, img.Width, img.Height);

        public void Image(PImage img, float x, float y, float w, float h)
        {
            EnsureReady();
            using var tintPaint = BuildTintPaint();
            Canvas.DrawBitmap(img.Bitmap, new SKRect(x, y, x + w, y + h), tintPaint);
        }

        public void Image(PGraphics pg, float x, float y) => Image(pg, x, y, pg.Width, pg.Height);

        public void Image(PGraphics pg, float x, float y, float w, float h)
        {
            EnsureReady();
            using var snapshot = pg.SnapshotForDraw();
            using var tintPaint = BuildTintPaint();
            Canvas.DrawImage(snapshot, new SKRect(x, y, x + w, y + h), tintPaint);
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
            _textPaint.Typeface?.Dispose();
            _textPaint.Dispose();
            _shapePath?.Dispose();
            _disposed = true;
        }
    }
}