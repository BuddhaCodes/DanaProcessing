namespace DanaProcessing
{
    /// <summary>
    /// Backend GPU state for a GpuParticleSystem -- mirrors Mesh3DHandle's
    /// role for PShape (see Types/PShape.cs): the public GpuParticleSystem
    /// is a thin handle, this owns the actual GL objects, on whichever of
    /// the two backends CreateGpuParticles() picked for it (see
    /// Renderer3DBackend.GpuParticles.cs).
    /// </summary>
    internal sealed class GpuParticleHandle
    {
        public Renderer3DBackend Backend = null!;
        public int Count;
        public bool ComputeBackend;
        public float SimTimeAccumulated;

        // Backend A -- real compute shader (GL 4.3+, ARB_compute_shader).
        public uint Ssbo;
        public uint ComputeProgram;
        public int UDeltaTimeLocCompute, UTimeLocCompute;

        // Backend B -- ping-pong texture GPGPU fallback (GL 3.3, macOS +
        // anywhere else 4.3 isn't genuinely available). Index 0/1 are the
        // two ping-pong slots; CurrentIndex is whichever one holds the most
        // recently written (i.e. currently valid) state.
        public uint[] PositionTex = new uint[2];
        public uint[] VelocityTex = new uint[2];
        public uint[] Fbo = new uint[2];
        public int TexWidth, TexHeight, CurrentIndex;
        public uint StepProgram;
        public int UPrevPositionLoc, UPrevVelocityLoc, UDeltaTimeLocStep, UTimeLocStep;

        // Shared by both backends -- the point-sprite draw pass.
        public uint DrawVao, DrawProgram;
        public int UModelLoc, UViewLoc, UProjectionLoc, UPointSizeLoc, UFillColorLoc;
        public int UPositionTexLoc, UVelocityTexLoc; // Backend B's draw pass only
    }

    /// <summary>
    /// A GPU-driven particle system: Count particles, simulated entirely on
    /// the GPU every StepParticles() call, drawn with DrawParticles() --
    /// like combining CreateShape3D()'s "upload once, reuse every frame"
    /// model with PShader's "sketch supplies GLSL" model, but for a live
    /// simulation instead of a static mesh or a fixed-pipeline replacement.
    /// Build one with GraphicsContext.CreateGpuParticles() under Renderer3D.
    ///
    /// Runs on one of two backends, chosen automatically and invisibly at
    /// creation time (see Renderer3DBackend.SupportsComputeShaders):
    ///  - a real GLSL compute shader (glDispatchCompute + an SSBO) when the
    ///    GPU/driver genuinely supports GL 4.3+ (ARB_compute_shader) --
    ///    Windows and Linux, typically.
    ///  - a classic ping-pong render-to-texture simulation (two RGBA32F
    ///    position/velocity textures, swapped every step) everywhere else --
    ///    notably macOS, where Apple's native OpenGL implementation is
    ///    hard-capped at GL 4.1, below what compute shaders need.
    /// StepParticles()/DrawParticles() behave identically either way -- a
    /// sketch never needs to know or care which backend it landed on (see
    /// UsesComputeShader below if it wants to say so anyway, e.g. for an
    /// on-screen debug label).
    /// </summary>
    public sealed class GpuParticleSystem : System.IDisposable
    {
        internal readonly GpuParticleHandle Handle;

        /// <summary>How many particles this system holds -- fixed for its
        /// lifetime, set by CreateGpuParticles()'s seed array length.</summary>
        public int Count => Handle.Count;

        /// <summary>True when this system is running on the real GLSL
        /// compute-shader backend, false when it's on the ping-pong-texture
        /// fallback. Purely informational -- both behave identically from a
        /// sketch's point of view -- exposed so a sketch can show which
        /// path it's on (see the "GPU particles" sample), not because it
        /// needs to branch on it.</summary>
        public bool UsesComputeShader => Handle.ComputeBackend;

        internal GpuParticleSystem(GpuParticleHandle handle) => Handle = handle;

        /// <summary>Releases this system's GPU state (SSBO or textures/FBOs,
        /// plus its draw program/VAO). Like PShape's 3D meshes, this is the
        /// sketch's own responsibility to call when a particle system is no
        /// longer needed -- Renderer3DBackend.Dispose() only cleans up its
        /// own backend-shared state (e.g. the ping-pong fallback's shared
        /// fullscreen quad), not every outstanding particle system.</summary>
        public void Dispose() => Handle.Backend.DeleteParticleSystem(Handle);
    }
}
