namespace DanaProcessing
{
    /// <summary>
    /// Raw vertex data for a 1x1x1 cube centered at the origin, used by
    /// Renderer3DBackend to draw Box(). 36 vertices (6 faces * 2 triangles *
    /// 3 vertices, no index buffer -- simplest thing that works for a
    /// single low-poly primitive). Each vertex is 6 floats: position (x,y,z)
    /// then normal (nx,ny,nz), matching the stride Renderer3DBackend sets up
    /// in SetUpCubeMesh().
    /// </summary>
    internal static class CubeVertexData
    {
        public static readonly float[] PositionsAndNormals =
        {
            // Front face (+Z)
            -0.5f, -0.5f,  0.5f,  0f, 0f, 1f,
             0.5f, -0.5f,  0.5f,  0f, 0f, 1f,
             0.5f,  0.5f,  0.5f,  0f, 0f, 1f,
             0.5f,  0.5f,  0.5f,  0f, 0f, 1f,
            -0.5f,  0.5f,  0.5f,  0f, 0f, 1f,
            -0.5f, -0.5f,  0.5f,  0f, 0f, 1f,

            // Back face (-Z)
             0.5f, -0.5f, -0.5f,  0f, 0f, -1f,
            -0.5f, -0.5f, -0.5f,  0f, 0f, -1f,
            -0.5f,  0.5f, -0.5f,  0f, 0f, -1f,
            -0.5f,  0.5f, -0.5f,  0f, 0f, -1f,
             0.5f,  0.5f, -0.5f,  0f, 0f, -1f,
             0.5f, -0.5f, -0.5f,  0f, 0f, -1f,

            // Left face (-X)
            -0.5f, -0.5f, -0.5f,  -1f, 0f, 0f,
            -0.5f, -0.5f,  0.5f,  -1f, 0f, 0f,
            -0.5f,  0.5f,  0.5f,  -1f, 0f, 0f,
            -0.5f,  0.5f,  0.5f,  -1f, 0f, 0f,
            -0.5f,  0.5f, -0.5f,  -1f, 0f, 0f,
            -0.5f, -0.5f, -0.5f,  -1f, 0f, 0f,

            // Right face (+X)
             0.5f, -0.5f,  0.5f,  1f, 0f, 0f,
             0.5f, -0.5f, -0.5f,  1f, 0f, 0f,
             0.5f,  0.5f, -0.5f,  1f, 0f, 0f,
             0.5f,  0.5f, -0.5f,  1f, 0f, 0f,
             0.5f,  0.5f,  0.5f,  1f, 0f, 0f,
             0.5f, -0.5f,  0.5f,  1f, 0f, 0f,

            // Top face (+Y)
            -0.5f,  0.5f,  0.5f,  0f, 1f, 0f,
             0.5f,  0.5f,  0.5f,  0f, 1f, 0f,
             0.5f,  0.5f, -0.5f,  0f, 1f, 0f,
             0.5f,  0.5f, -0.5f,  0f, 1f, 0f,
            -0.5f,  0.5f, -0.5f,  0f, 1f, 0f,
            -0.5f,  0.5f,  0.5f,  0f, 1f, 0f,

            // Bottom face (-Y)
            -0.5f, -0.5f, -0.5f,  0f, -1f, 0f,
             0.5f, -0.5f, -0.5f,  0f, -1f, 0f,
             0.5f, -0.5f,  0.5f,  0f, -1f, 0f,
             0.5f, -0.5f,  0.5f,  0f, -1f, 0f,
            -0.5f, -0.5f,  0.5f,  0f, -1f, 0f,
            -0.5f, -0.5f, -0.5f,  0f, -1f, 0f,
        };
    }
}