using System;
using SkiaSharp;
using SkiaSharp.Extended.Svg;
using SKSvg = SkiaSharp.Extended.Svg.SKSvg;

namespace DanaProcessing
{
    /// <summary>
    /// Which primitive CreateShape() builds, like Processing's RECT/ELLIPSE/
    /// LINE/TRIANGLE/QUAD constants — https://processing.org/reference/createShape_.html.
    /// GROUP isn't here: it has its own CreateShape(params PShape[]) overload
    /// instead, since it takes children rather than numeric coordinates.
    /// </summary>
    public enum PShapeType { Rect, Ellipse, Line, Triangle, Quad }

    /// <summary>
    /// A persistent 3D mesh's GPU handle, backing a PShape built with
    /// Sketch/GraphicsContext.CreateShape3D(). Uploaded once (StaticDraw),
    /// unlike the temporary per-call VBO that immediate-mode
    /// BeginShape()/EndShape() uses under Renderer3D — that's the whole
    /// point of building a shape this way. Tied to the specific
    /// Renderer3DBackend it was uploaded to; drawing it through a different
    /// backend (a different PGraphics/Sketch) throws — see Shape3D() in
    /// GraphicsContext.cs.
    /// </summary>
    internal sealed class Mesh3DHandle
    {
        public uint Vao;
        public uint Vbo;
        public int VertexCount;
        public uint TextureId; // 0 = sin textura
        public Renderer3DBackend Backend = null!;
    }

    /// <summary>
    /// A loaded or built vector/mesh shape, equivalent to Processing's
    /// PShape. Four ways to get one: LoadSvg(path) (via Sketch.LoadShape())
    /// for existing 2D vector art, CreatePrimitive/CreateGroup (via
    /// Sketch's CreateShape() overloads) to build 2D shapes out of code, or
    /// CreateShape3D() to build a reusable 3D mesh under Renderer3D. Each
    /// PShape is EITHER a 2D SKPicture OR a 3D mesh handle, never both —
    /// Picture is null for a 3D shape, Mesh3D is null for everything else.
    /// </summary>
    public sealed class PShape : IDisposable
    {
        internal SKPicture? Picture { get; }
        internal Mesh3DHandle? Mesh3D { get; }

        /// <summary>Natural width — from the SVG's own canvas size for LoadSvg(), or the shape's bounding box for CreatePrimitive()/CreateGroup(). 0 for a 3D mesh shape (position/scale it with PushMatrix()/Translate()/Scale() around Shape() instead — see Shape3D()'s remarks).</summary>
        public float Width { get; }

        /// <summary>Natural height — see Width.</summary>
        public float Height { get; }

        private PShape(SKPicture picture, float width, float height)
        {
            Picture = picture;
            Mesh3D = null;
            Width = width;
            Height = height;
        }

        private PShape(Mesh3DHandle mesh3D)
        {
            Picture = null;
            Mesh3D = mesh3D;
            Width = 0;
            Height = 0;
        }

        /// <summary>Loads an SVG file from disk. Throws if the file doesn't exist or isn't a parseable SVG.</summary>
        public static PShape LoadSvg(string path)
        {
            // Deliberadamente sin `using` -- el Dispose() de este SKSvg
            // también dispone el SKPicture que cargó, y PShape necesita que
            // esa picture sobreviva a este método (es lo que se dibuja
            // después, cada frame, vía Shape()). Esto deja sin liberar el
            // wrapper managed chico de SKSvg en sí, no la picture -- un
            // costo aceptable por una carga que se hace una sola vez.
            var svg = new SKSvg();

            SKPicture? picture;
            try
            {
                picture = svg.Load(path);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"No se pudo cargar el SVG: '{path}'. Verifica la ruta y el formato.", ex);
            }

            if (picture == null)
                throw new InvalidOperationException($"No se pudo cargar el SVG: '{path}'. Verifica la ruta y el formato.");

            var size = svg.CanvasSize;
            return new PShape(picture, size.Width, size.Height);
        }

        /// <summary>
        /// Builds one of the primitive shape types from raw coordinates, like
        /// Processing's createShape(RECT/ELLIPSE/LINE/TRIANGLE/QUAD, ...).
        /// Called via GraphicsContext.CreateShape(type, v) — not meant to be
        /// called directly from sketch code, since that's what bakes in the
        /// CURRENT fill/stroke/strokeWeight as the shape's fixed style.
        /// </summary>
        internal static PShape CreatePrimitive(PShapeType type, float[] v, Color? fill, Color? stroke, float strokeWeight)
        {
            int expected = type switch
            {
                PShapeType.Rect => 4,
                PShapeType.Ellipse => 4,
                PShapeType.Line => 4,
                PShapeType.Triangle => 6,
                PShapeType.Quad => 8,
                _ => throw new ArgumentOutOfRangeException(nameof(type)),
            };
            if (v.Length != expected)
                throw new ArgumentException($"CreateShape({type}, ...) espera {expected} valores, se recibieron {v.Length}.", nameof(v));

            var bounds = ComputeBounds(type, v, strokeWeight);

            using var recorder = new SKPictureRecorder();
            var canvas = recorder.BeginRecording(bounds);

            using var fillPaint = fill.HasValue
                ? new SKPaint { Style = SKPaintStyle.Fill, IsAntialias = true, Color = fill.Value.Skia }
                : null;
            using var strokePaint = stroke.HasValue
                ? new SKPaint { Style = SKPaintStyle.Stroke, IsAntialias = true, Color = stroke.Value.Skia, StrokeWidth = strokeWeight }
                : null;

            switch (type)
            {
                case PShapeType.Rect:
                {
                    var rect = new SKRect(v[0], v[1], v[0] + v[2], v[1] + v[3]);
                    if (fillPaint != null)
                        canvas.DrawRect(rect, fillPaint);
                    if (strokePaint != null)
                        canvas.DrawRect(rect, strokePaint);
                    break;
                }
                case PShapeType.Ellipse:
                {
                    var oval = new SKRect(v[0], v[1], v[0] + v[2], v[1] + v[3]);
                    if (fillPaint != null)
                        canvas.DrawOval(oval, fillPaint);
                    if (strokePaint != null)
                        canvas.DrawOval(oval, strokePaint);
                    break;
                }
                case PShapeType.Line:
                {
                    if (strokePaint != null)
                        canvas.DrawLine(v[0], v[1], v[2], v[3], strokePaint);
                    break;
                }
                case PShapeType.Triangle:
                {
                    using var path = new SKPath();
                    path.MoveTo(v[0], v[1]);
                    path.LineTo(v[2], v[3]);
                    path.LineTo(v[4], v[5]);
                    path.Close();
                    if (fillPaint != null)
                        canvas.DrawPath(path, fillPaint);
                    if (strokePaint != null)
                        canvas.DrawPath(path, strokePaint);
                    break;
                }
                case PShapeType.Quad:
                {
                    using var path = new SKPath();
                    path.MoveTo(v[0], v[1]);
                    path.LineTo(v[2], v[3]);
                    path.LineTo(v[4], v[5]);
                    path.LineTo(v[6], v[7]);
                    path.Close();
                    if (fillPaint != null)
                        canvas.DrawPath(path, fillPaint);
                    if (strokePaint != null)
                        canvas.DrawPath(path, strokePaint);
                    break;
                }
            }

            var picture = recorder.EndRecording();
            return new PShape(picture, bounds.Width, bounds.Height);
        }

        /// <summary>
        /// Combines several 2D shapes into one, like Processing's
        /// createShape(GROUP) followed by addChild() for each piece. Each
        /// child keeps the absolute local coordinates it was built with, so
        /// the group's own bounds are just the union of its children's.
        /// 2D only — group a set of CreateShape3D() meshes by drawing each
        /// with its own Shape() call instead (there's no 3D-mesh grouping
        /// here yet).
        /// </summary>
        internal static PShape CreateGroup(PShape[] children)
        {
            if (children == null || children.Length == 0)
                throw new ArgumentException("CreateShape(GROUP) necesita al menos un shape hijo.", nameof(children));

            SKRect bounds = default;
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i].Picture == null)
                    throw new ArgumentException("CreateShape(GROUP) solo agrupa shapes 2D (creados con CreateShape(RECT/ELLIPSE/...) o LoadShape()) -- un shape 3D de CreateShape3D() no tiene una Picture que combinar.", nameof(children));
                bounds = i == 0 ? children[0].Picture!.CullRect : SKRect.Union(bounds, children[i].Picture!.CullRect);
            }

            using var recorder = new SKPictureRecorder();
            var canvas = recorder.BeginRecording(bounds);
            foreach (var child in children)
                canvas.DrawPicture(child.Picture);
            var picture = recorder.EndRecording();

            return new PShape(picture, bounds.Width, bounds.Height);
        }

        /// <summary>
        /// Wraps an already-uploaded 3D mesh (from Renderer3DBackend.UploadPersistentMesh())
        /// as a PShape. Called by GraphicsContext.CreateShape3D() — not
        /// meant to be called directly from sketch code.
        /// </summary>
        internal static PShape FromMesh3D(Renderer3DBackend backend, uint vao, uint vbo, int vertexCount, uint textureId = 0) =>
            new PShape(new Mesh3DHandle { Backend = backend, Vao = vao, Vbo = vbo, VertexCount = vertexCount, TextureId = textureId });

        /// <summary>Bounding rect used only to size the SKPictureRecorder — padded by half the stroke weight so a thick outline isn't clipped at the edges.</summary>
        private static SKRect ComputeBounds(PShapeType type, float[] v, float strokeWeight)
        {
            float pad = strokeWeight / 2f + 1f;
            return type switch
            {
                PShapeType.Rect or PShapeType.Ellipse =>
                    new SKRect(v[0] - pad, v[1] - pad, v[0] + v[2] + pad, v[1] + v[3] + pad),
                PShapeType.Line =>
                    new SKRect(Math.Min(v[0], v[2]) - pad, Math.Min(v[1], v[3]) - pad,
                                Math.Max(v[0], v[2]) + pad, Math.Max(v[1], v[3]) + pad),
                PShapeType.Triangle =>
                    BoundsOfPoints(pad, v[0], v[1], v[2], v[3], v[4], v[5]),
                PShapeType.Quad =>
                    BoundsOfPoints(pad, v[0], v[1], v[2], v[3], v[4], v[5], v[6], v[7]),
                _ => throw new ArgumentOutOfRangeException(nameof(type)),
            };
        }

        /// <summary>Bounding rect of a flat (x0,y0,x1,y1,...) point list, padded by `pad` on every side.</summary>
        private static SKRect BoundsOfPoints(float pad, params float[] xy)
        {
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            for (int i = 0; i < xy.Length; i += 2)
            {
                minX = Math.Min(minX, xy[i]);
                maxX = Math.Max(maxX, xy[i]);
                minY = Math.Min(minY, xy[i + 1]);
                maxY = Math.Max(maxY, xy[i + 1]);
            }
            return new SKRect(minX - pad, minY - pad, maxX + pad, maxY + pad);
        }

        public void Dispose()
        {
            if (Mesh3D != null)
                Mesh3D.Backend.DeleteMesh(Mesh3D.Vao, Mesh3D.Vbo);
            else
                Picture?.Dispose();
        }
    }
}