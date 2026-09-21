using System;
using System.Collections.Generic;

namespace DanaProcessing
{
    /// <summary>
    /// Generates the raw position+normal vertex data for a unit-radius UV
    /// sphere centered at the origin, used by Renderer3DBackend to draw
    /// Sphere() -- https://processing.org/reference/sphere_.html
    ///
    /// Unlike CubeVertexData (a fixed 36-vertex table), this mesh's
    /// resolution is configurable via SphereDetail(ures, vres) --
    /// https://processing.org/reference/sphereDetail_.html -- so it's a
    /// generator, not a static table. Renderer3DBackend regenerates it
    /// lazily (only when the detail actually changes and a Sphere() is
    /// about to be drawn) via EnsureSphereMesh().
    ///
    /// Same vertex layout as CubeVertexData: 6 floats per vertex (position
    /// xyz, normal xyz), no index buffer -- a flat triangle list, drawn
    /// with PrimitiveType.Triangles. Sphere(radius) scales this unit
    /// sphere via the model matrix, the same trick Box(w,h,d) uses on
    /// CubeVertexData's unit cube, so there's exactly one sphere mesh in
    /// GPU memory regardless of how many differently-sized spheres a
    /// sketch draws (as long as they share the current SphereDetail()).
    /// </summary>
    internal static class SphereVertexData
    {
        /// <summary>Processing's default: sphereDetail() with no arguments resets ures/vres to 30.</summary>
        public const int DefaultDetail = 30;

        /// <summary>Processing clamps sphereDetail() to a minimum of 3 (below that the mesh degenerates into an octahedron-or-worse) -- same floor applied here.</summary>
        public const int MinDetail = 3;

        /// <summary>
        /// Builds a flat position+normal vertex list for a unit sphere with
        /// ures segments around the equator (longitude) and vres segments
        /// from pole to pole (latitude). For a unit sphere centered at the
        /// origin the normal at any point equals its position, so each
        /// vertex is emitted as (pos, pos) rather than computed separately.
        /// </summary>
        public static float[] Generate(int ures, int vres)
        {
            ures = Math.Max(MinDetail, ures);
            vres = Math.Max(MinDetail, vres);

            // 2 triangles * 3 vertices * 6 floats, per quad, per (u,v) cell.
            var verts = new List<float>(ures * vres * 6 * 6);

            for (int v = 0; v < vres; v++)
            {
                float theta1 = v * MathF.PI / vres;
                float theta2 = (v + 1) * MathF.PI / vres;

                for (int u = 0; u < ures; u++)
                {
                    float phi1 = u * 2f * MathF.PI / ures;
                    float phi2 = (u + 1) * 2f * MathF.PI / ures;

                    // Four corners of this lat/long quad, on the unit sphere.
                    var p00 = SpherePoint(theta1, phi1);
                    var p01 = SpherePoint(theta1, phi2);
                    var p10 = SpherePoint(theta2, phi1);
                    var p11 = SpherePoint(theta2, phi2);

                    // Two triangles per quad. At the poles (theta1==0 or
                    // theta2==PI) one edge collapses to a single point, so
                    // one of these two triangles comes out zero-area rather
                    // than needing special-cased fan geometry there -- same
                    // "simplest thing that works" tradeoff CubeVertexData
                    // makes by skipping an index buffer entirely.
                    AddVertex(verts, p00);
                    AddVertex(verts, p10);
                    AddVertex(verts, p11);

                    AddVertex(verts, p00);
                    AddVertex(verts, p11);
                    AddVertex(verts, p01);
                }
            }

            return verts.ToArray();
        }

        // theta: 0 at the north pole (+Y) to PI at the south pole (-Y).
        // phi: 0..2*PI around the equator.
        private static (float x, float y, float z) SpherePoint(float theta, float phi)
        {
            float sinTheta = MathF.Sin(theta);
            return (sinTheta * MathF.Cos(phi), MathF.Cos(theta), sinTheta * MathF.Sin(phi));
        }

        private static void AddVertex(List<float> verts, (float x, float y, float z) p)
        {
            // Unit sphere centered at the origin: normal == position.
            verts.Add(p.x);
            verts.Add(p.y);
            verts.Add(p.z);
            verts.Add(p.x);
            verts.Add(p.y);
            verts.Add(p.z);
        }
    }
}