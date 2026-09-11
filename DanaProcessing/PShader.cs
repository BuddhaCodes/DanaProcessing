namespace DanaProcessing
{
    /// <summary>
    /// A GLSL shader program, like Processing's PShader --
    /// https://processing.org/reference/PShader.html. Build one with
    /// LoadShader(), activate it with Shader(), and go back to the
    /// built-in pipeline with ResetShader() -- all three require
    /// Renderer3D, same as the rest of the 3D API.
    ///
    /// SCOPE: DanaProcessing has exactly one vertex-attribute layout for
    /// all 3D geometry -- location 0 = vec3 position, location 1 = vec3
    /// normal (see Renderer3DBackend's SetUpCubeMesh()/EnsureSphereMesh())
    /// -- so a custom vertex shader must use that same layout to work with
    /// Box()/Sphere(). The common case, and the only one Processing's own
    /// loadShader(fragFilename) single-argument overload allows, is
    /// supplying just a fragment shader; that reuses DanaProcessing's
    /// built-in vertex shader (and its uModel/uView/uProjection/uFillColor/
    /// lighting uniforms) automatically. There's also no separate shader
    /// per PShaderFlag (POINTS/LINES/etc.) like real Processing -- one
    /// PShader replaces the whole 3D pipeline's program at once.
    /// </summary>
    public sealed class PShader
    {
        internal readonly string? VertexSource;
        internal readonly string? FragmentSource;

        // Compiled lazily the first time this shader is passed to Shader()
        // -- see Renderer3DBackend.SetActiveShader() -- and cached here so
        // using the same PShader every draw() frame (the normal Processing
        // pattern) doesn't recompile a GL program every frame. CompiledFor
        // (an internal Renderer3DBackend, boxed as object so this stays a
        // public type) is re-checked on every SetActiveShader() call so a
        // PShader reused across more than one PGraphics/Renderer3DBackend
        // -- each with its own GL context -- gets its own compiled program
        // instead of reusing a program handle that belongs to a different
        // context.
        internal uint GLProgram;
        internal object? CompiledFor;

        internal PShader(string? vertexSource, string? fragmentSource)
        {
            VertexSource = vertexSource;
            FragmentSource = fragmentSource;
        }
    }
}