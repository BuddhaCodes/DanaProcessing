using System;
using SkiaSharp;

namespace DanaProcessing
{
    /// <summary>
    /// A camera for doing your own 3D on the 2D canvas: where it is, what it looks at,
    /// and how wide it sees. Project() turns a 3D point into a screen position -- for
    /// drawing anything there, hit-testing the mouse, or placing labels -- and
    /// Points(cloud, view) uses it to draw tens of thousands of points at once.
    /// X points right, Y up and Z away from you (like Unity; the 3D renderer follows
    /// Processing instead, with Y down). Geo.ToSphere() uses the same axes.
    /// </summary>
    public sealed class View3D
    {
        public PVector Eye = new(0, 0, -10);
        public PVector Target = new(0, 0, 0);
        public PVector Up = new(0, 1, 0);

        /// <summary>Vertical field of view, degrees.</summary>
        public float FovY = 60;

        /// <summary>A camera at <paramref name="distance"/> from <paramref name="target"/>, turned <paramref name="yaw"/> radians around it (left/right) and <paramref name="pitch"/> up/down.</summary>
        public static View3D Orbit(float yaw, float pitch, float distance, PVector target = default, float fovY = 60)
        {
            float cp = MathF.Cos(pitch);
            var eye = new PVector(target.X + MathF.Sin(yaw) * cp * distance, target.Y + MathF.Sin(pitch) * distance, target.Z - MathF.Cos(yaw) * cp * distance);
            return new View3D { Eye = eye, Target = target, FovY = fovY };
        }

        // The camera's axes and focal length, for one screen size; refreshed by Prepare().
        internal float Fx, Fy, Fz, Rx, Ry, Rz, Ux, Uy, Uz, Focal, Cx, Cy;

        internal void Prepare(float width, float height)
        {
            float fx = Target.X - Eye.X, fy = Target.Y - Eye.Y, fz = Target.Z - Eye.Z;
            float fl = MathF.Sqrt(fx * fx + fy * fy + fz * fz);
            if (fl == 0) { fx = 0; fy = 0; fz = 1; fl = 1; }
            Fx = fx / fl; Fy = fy / fl; Fz = fz / fl;
            // right = up x forward: x right, y up, z away from you (left-handed, like Unity), so a
            // camera looking along +z sees +x on its right. If looking straight along Up, pick another up.
            float rx = Up.Y * Fz - Up.Z * Fy, ry = Up.Z * Fx - Up.X * Fz, rz = Up.X * Fy - Up.Y * Fx;
            float rl = MathF.Sqrt(rx * rx + ry * ry + rz * rz);
            if (rl < 1e-5f) { rx = Fz; ry = 0; rz = -Fx; rl = MathF.Sqrt(rx * rx + rz * rz); if (rl < 1e-5f) { rx = 1; rl = 1; } }
            Rx = rx / rl; Ry = ry / rl; Rz = rz / rl;
            // up = forward x right
            Ux = Fy * Rz - Fz * Ry; Uy = Fz * Rx - Fx * Rz; Uz = Fx * Ry - Fy * Rx;
            Focal = height / 2f / MathF.Tan(FovY * MathF.PI / 360f);
            Cx = width / 2f; Cy = height / 2f;
        }

        /// <summary>
        /// Where a 3D point lands on a <paramref name="width"/> x <paramref name="height"/> screen,
        /// and its depth (distance along the view; 0 or less = behind the camera, don't draw it).
        /// A size s at that depth is s * Scale(depth, height) pixels.
        /// </summary>
        public (float X, float Y, float Depth) Project(PVector p, float width, float height)
        {
            Prepare(width, height);
            return ProjectPrepared(p.X, p.Y, p.Z);
        }

        internal (float X, float Y, float Depth) ProjectPrepared(float x, float y, float z)
        {
            float dx = x - Eye.X, dy = y - Eye.Y, dz = z - Eye.Z;
            float zc = dx * Fx + dy * Fy + dz * Fz;
            if (zc <= 1e-4f) return (float.NaN, float.NaN, zc);
            float xc = dx * Rx + dy * Ry + dz * Rz, yc = dx * Ux + dy * Uy + dz * Uz;
            return (Cx + Focal * xc / zc, Cy - Focal * yc / zc, zc);
        }

        /// <summary>Pixels per world unit at this depth, on a screen this tall.</summary>
        public float Scale(float depth, float height) => height / 2f / MathF.Tan(FovY * MathF.PI / 360f) / depth;
    }

    /// <summary>
    /// Many colored points in 3D, drawn in one go by Points(cloud, view): a dotted globe,
    /// a star field, a scan, a particle snapshot. Adding is cheap; drawing projects them,
    /// optionally sorts them far to near and hides the ones behind a ball, then sends them
    /// to Skia as a single batch of triangles -- far faster than a Circle() per point.
    /// <code>
    /// cloud.Clear();
    /// foreach (var p in dots) cloud.Add(p.X, p.Y, p.Z, Color(120, 200, 230), 0.05f);
    /// Points(cloud, View3D.Orbit(yaw, pitch, 25));
    /// </code>
    /// </summary>
    public sealed class PointCloud
    {
        internal float[] X = new float[1024], Y = new float[1024], Z = new float[1024], Size = new float[1024];
        internal uint[] Rgba = new uint[1024];

        public int Count { get; private set; }

        /// <summary>Round points (hexagons) or squares; squares are cheaper. Points under 3 pixels are always squares.</summary>
        public bool Round = true;

        /// <summary>Draw far points first so near ones cover them (on by default).</summary>
        public bool DepthSort = true;

        /// <summary>
        /// When true (the default) a point's size is in world units and shrinks with distance;
        /// when false it's in pixels whatever the distance.
        /// </summary>
        public bool SizeInWorld = true;

        /// <summary>Points behind this ball (from the camera's point of view) aren't drawn: the far side of a globe. Leave Radius at 0 for none.</summary>
        public PVector OccluderCenter;
        public float OccluderRadius;

        /// <summary>How many points the last Points() call actually drew.</summary>
        public int Drawn { get; internal set; }

        public void Clear() => Count = 0;

        /// <summary>Adds a point; returns its index.</summary>
        public int Add(float x, float y, float z, Color color, float size = 1)
        {
            if (Count == X.Length) Grow();
            int i = Count++;
            X[i] = x; Y[i] = y; Z[i] = z; Size[i] = size;
            Rgba[i] = (uint)(color.A << 24 | color.B << 16 | color.G << 8 | color.R);
            return i;
        }

        public int Add(PVector p, Color color, float size = 1) => Add(p.X, p.Y, p.Z, color, size);

        private void Grow()
        {
            int n = X.Length * 2;
            Array.Resize(ref X, n); Array.Resize(ref Y, n); Array.Resize(ref Z, n);
            Array.Resize(ref Size, n); Array.Resize(ref Rgba, n);
        }

        // Scratch, reused between frames.
        internal float[] Sx = Array.Empty<float>(), Sy = Array.Empty<float>(), Sr = Array.Empty<float>(), Depth = Array.Empty<float>();
        internal int[] Order = Array.Empty<int>();
        internal SKPoint[] Vertices = Array.Empty<SKPoint>();
        internal SKColor[] Colors = Array.Empty<SKColor>();
    }

    public abstract partial class GraphicsContext
    {
        /// <summary>
        /// Draws every point of <paramref name="cloud"/> as seen by <paramref name="view"/>, in a single
        /// batch. Fill/Stroke don't apply: each point has its own color. The canvas's current
        /// transform does (Translate/Scale move the whole picture).
        /// </summary>
        public void Points(PointCloud cloud, View3D view)
        {
            EnsureReady();
            int n = cloud.Count;
            if (n == 0) { cloud.Drawn = 0; return; }
            if (cloud.Sx.Length < n)
            {
                int cap = Math.Max(n, cloud.Sx.Length * 2);
                cloud.Sx = new float[cap]; cloud.Sy = new float[cap]; cloud.Sr = new float[cap];
                cloud.Depth = new float[cap]; cloud.Order = new int[cap];
            }
            view.Prepare(Width, Height);
            bool occlude = cloud.OccluderRadius > 0;
            var oc = cloud.OccluderCenter;
            float r2 = cloud.OccluderRadius * cloud.OccluderRadius;
            float ex = view.Eye.X, ey = view.Eye.Y, ez = view.Eye.Z;
            // The eye relative to the ball, for the occlusion test.
            float ecx = ex - oc.X, ecy = ey - oc.Y, ecz = ez - oc.Z;
            float ecc = ecx * ecx + ecy * ecy + ecz * ecz - r2;

            int m = 0;
            for (int i = 0; i < n; i++)
            {
                var (sx, sy, depth) = view.ProjectPrepared(cloud.X[i], cloud.Y[i], cloud.Z[i]);
                if (depth <= 1e-3f) continue;
                if (occlude && ecc > 0)
                {
                    // Does the segment eye -> point pass through the ball before reaching the point?
                    float dx = cloud.X[i] - ex, dy = cloud.Y[i] - ey, dz = cloud.Z[i] - ez;
                    float a = dx * dx + dy * dy + dz * dz;
                    float b = 2 * (dx * ecx + dy * ecy + dz * ecz);
                    float disc = b * b - 4 * a * ecc;
                    if (disc > 0)
                    {
                        float t = (-b - MathF.Sqrt(disc)) / (2 * a);
                        if (t > 0 && t < 1) continue;
                    }
                }
                float radius = cloud.SizeInWorld ? cloud.Size[i] * view.Focal / depth / 2 : cloud.Size[i] / 2;
                if (sx < -radius || sy < -radius || sx > Width + radius || sy > Height + radius) continue;
                cloud.Sx[i] = sx; cloud.Sy[i] = sy; cloud.Sr[i] = Math.Max(radius, 0.35f);
                cloud.Depth[m] = -depth;
                cloud.Order[m] = i;
                m++;
            }
            if (cloud.DepthSort)
                Array.Sort(cloud.Depth, cloud.Order, 0, m);
            cloud.Drawn = m;
            if (m == 0) return;

            // Each point: a square (2 triangles) or, when round and big enough, a hexagon (6).
            int needed = 0;
            for (int q = 0; q < m; q++)
                needed += cloud.Round && cloud.Sr[cloud.Order[q]] >= 1.5f ? 18 : 6;
            if (cloud.Vertices.Length < needed)
            {
                cloud.Vertices = new SKPoint[needed + needed / 4];
                cloud.Colors = new SKColor[cloud.Vertices.Length];
            }
            var v = cloud.Vertices;
            var col = cloud.Colors;
            int k = 0;
            for (int q = 0; q < m; q++)
            {
                int i = cloud.Order[q];
                float x = cloud.Sx[i], y = cloud.Sy[i], r = cloud.Sr[i];
                uint c = cloud.Rgba[i];
                var color = new SKColor((byte)c, (byte)(c >> 8), (byte)(c >> 16), (byte)(c >> 24));
                if (cloud.Round && r >= 1.5f)
                {
                    float hx = r * 0.8660254f, hy = r * 0.5f;
                    var p0 = new SKPoint(x, y - r); var p1 = new SKPoint(x + hx, y - hy); var p2 = new SKPoint(x + hx, y + hy);
                    var p3 = new SKPoint(x, y + r); var p4 = new SKPoint(x - hx, y + hy); var p5 = new SKPoint(x - hx, y - hy);
                    var center = new SKPoint(x, y);
                    v[k++] = center; v[k++] = p0; v[k++] = p1;
                    v[k++] = center; v[k++] = p1; v[k++] = p2;
                    v[k++] = center; v[k++] = p2; v[k++] = p3;
                    v[k++] = center; v[k++] = p3; v[k++] = p4;
                    v[k++] = center; v[k++] = p4; v[k++] = p5;
                    v[k++] = center; v[k++] = p5; v[k++] = p0;
                    for (int j = 0; j < 18; j++) col[k - 18 + j] = color;
                }
                else
                {
                    v[k++] = new SKPoint(x - r, y - r); v[k++] = new SKPoint(x + r, y - r); v[k++] = new SKPoint(x + r, y + r);
                    v[k++] = new SKPoint(x - r, y - r); v[k++] = new SKPoint(x + r, y + r); v[k++] = new SKPoint(x - r, y + r);
                    for (int j = 0; j < 6; j++) col[k - 6 + j] = color;
                }
            }
            var positions = k == v.Length ? v : v.AsSpan(0, k).ToArray();
            var colors = k == col.Length ? col : col.AsSpan(0, k).ToArray();
            using var vertices = SKVertices.CreateCopy(SKVertexMode.Triangles, positions, null, colors);
            using var paint = new SKPaint { IsAntialias = false, Color = SKColors.White };
            Canvas.DrawVertices(vertices, SKBlendMode.Dst, paint);
        }
    }
}
