using System;
using SkiaSharp;
using Silk.NET.OpenGL;

namespace DanaProcessing
{
    /// <summary>
    /// GpuParticleSystem's actual GL work -- continuation of Renderer3DBackend,
    /// split into its own file since it's a self-contained addition on top of
    /// an already-large class. See GpuParticleSystem.cs for the public type
    /// and GraphicsContext.3D.cs for the three sketch-facing methods
    /// (CreateGpuParticles/StepParticles/DrawParticles) that call into here.
    ///
    /// Two backends, chosen once per system at creation time by
    /// SupportsComputeShaders (see Renderer3DBackend.cs's CreateGLContext()):
    ///  - BACKEND A (real compute shader, GL 4.3+): particle state lives in
    ///    one SSBO (std430, vec4-aligned position+velocity per particle).
    ///    StepParticles() dispatches a real compute shader over it, with a
    ///    memory barrier before the following draw reads it. DrawParticles()
    ///    indexes the SSBO directly by gl_VertexID -- no instancing, just a
    ///    plain glDrawArrays(POINTS) against an empty VAO.
    ///  - BACKEND B (ping-pong texture GPGPU, GL 3.3 -- macOS and anywhere
    ///    else 4.3 isn't genuinely available): particle state lives in two
    ///    RGBA32F position/velocity texture pairs (ping/pong), each attached
    ///    to its own FBO via MRT. StepParticles() renders a shared fullscreen
    ///    quad into the OTHER slot's FBO, running the sketch's simulation
    ///    snippet as a fragment shader that reads the current slot via
    ///    texelFetch(). DrawParticles() reads the same texture per-vertex,
    ///    by gl_VertexID, via texelFetch as well.
    ///
    /// Both backends compile the EXACT SAME sketch-supplied simulationGlsl
    /// string, unmodified, into their respective wrapper templates -- that's
    /// only possible because CreateGpuParticles()'s documented contract
    /// restricts a snippet to reading/writing its own position/velocity plus
    /// uDeltaTime/uTime/the noise helpers, nothing cross-particle. The two
    /// wrapper TEMPLATES themselves are genuinely different code (a compute
    /// shader body vs. a fragment shader body) -- that part isn't shared.
    /// </summary>
    internal sealed partial class Renderer3DBackend
    {
        // Shared fullscreen quad backing Backend B's simulation step -- lazily
        // built the first time ANY GpuParticleSystem on this backend needs
        // it (EnsureFullscreenQuad()), reused by every one afterward. Cleaned
        // up in Renderer3DBackend.cs's Dispose().
        private uint _quadVao, _quadVbo;

        // =====================================================================
        // Public surface -- called by GraphicsContext.3D.cs's
        // CreateGpuParticles()/StepParticles()/DrawParticles(), and by
        // GpuParticleSystem.Dispose() (via GpuParticleHandle.Backend).
        // =====================================================================

        /// <summary>Builds a new particle system on whichever backend this
        /// Renderer3DBackend picked at Create() time (SupportsComputeShaders).
        /// Claims/releases the GL context itself, like UploadPersistentMesh() --
        /// safe to call standalone from Setup().</summary>
        public GpuParticleHandle CreateParticleSystem(int count, string simulationGlsl, PVector[] seedPositions, PVector[]? seedVelocities)
        {
            return _computeCapable
                ? CreateComputeParticleSystem(count, simulationGlsl, seedPositions, seedVelocities)
                : CreateTextureParticleSystem(count, simulationGlsl, seedPositions, seedVelocities);
        }

        /// <summary>Runs one simulation step. Assumes the GL context is
        /// already current on this thread -- called mid-Draw(), same
        /// contract as DrawBox()/DrawUploadedMesh().</summary>
        public void StepParticles(GpuParticleHandle handle, float deltaTime)
        {
            handle.SimTimeAccumulated += deltaTime;
            if (handle.ComputeBackend)
                StepComputeParticles(handle, deltaTime);
            else
                StepTextureParticles(handle, deltaTime);
        }

        /// <summary>Draws every particle as a point sprite, tinted by
        /// fillColor, under the current model/view/projection. Assumes the
        /// GL context is already current, same as StepParticles().</summary>
        public void DrawParticles(GpuParticleHandle handle, float pointSize, SKColor fillColor)
        {
            // Off by default; DrawArrays(POINTS) ignores gl_PointSize output
            // from the vertex shader without this. Cheap enough to just set
            // every call rather than tracking whether it's already on.
            _gl.Enable(EnableCap.ProgramPointSize);

            _gl.UseProgram(handle.DrawProgram);
            UploadMatrix(handle.UModelLoc, _model);
            UploadMatrix(handle.UViewLoc, _view);
            UploadMatrix(handle.UProjectionLoc, _projection);
            _gl.Uniform1(handle.UPointSizeLoc, pointSize);
            _gl.Uniform4(handle.UFillColorLoc, fillColor.Red / 255f, fillColor.Green / 255f, fillColor.Blue / 255f, fillColor.Alpha / 255f);

            if (handle.ComputeBackend)
            {
                _gl.BindBufferBase(GLEnum.ShaderStorageBuffer, 0, handle.Ssbo);
            }
            else
            {
                _gl.ActiveTexture(TextureUnit.Texture0);
                _gl.BindTexture(TextureTarget.Texture2D, handle.PositionTex[handle.CurrentIndex]);
                _gl.Uniform1(handle.UPositionTexLoc, 0);
                _gl.ActiveTexture(TextureUnit.Texture1);
                _gl.BindTexture(TextureTarget.Texture2D, handle.VelocityTex[handle.CurrentIndex]);
                _gl.Uniform1(handle.UVelocityTexLoc, 1);
            }

            _gl.BindVertexArray(handle.DrawVao);
            _gl.DrawArrays(GLEnum.Points, 0, (uint)handle.Count);
            _gl.BindVertexArray(0);
        }

        /// <summary>Releases a particle system's GL state, whichever backend
        /// it's on -- called from GpuParticleSystem.Dispose(). Same
        /// already-disposed-backend no-op and claim/release contract as
        /// DeleteMesh().</summary>
        public void DeleteParticleSystem(GpuParticleHandle handle)
        {
            if (_disposed)
                return;

            _window.GLContext?.MakeCurrent();
            try
            {
                _gl.DeleteVertexArray(handle.DrawVao);
                _gl.DeleteProgram(handle.DrawProgram);

                if (handle.ComputeBackend)
                {
                    _gl.DeleteBuffer(handle.Ssbo);
                    _gl.DeleteProgram(handle.ComputeProgram);
                }
                else
                {
                    for (int slot = 0; slot < 2; slot++)
                    {
                        _gl.DeleteFramebuffer(handle.Fbo[slot]);
                        _gl.DeleteTexture(handle.PositionTex[slot]);
                        _gl.DeleteTexture(handle.VelocityTex[slot]);
                    }
                    _gl.DeleteProgram(handle.StepProgram);
                }
            }
            finally
            {
                _window.GLContext?.Clear();
            }
        }

        // =====================================================================
        // Backend A -- real compute shader (GL 4.3+, ARB_compute_shader)
        // =====================================================================

        private unsafe GpuParticleHandle CreateComputeParticleSystem(int count, string simulationGlsl, PVector[] seedPositions, PVector[]? seedVelocities)
        {
            _window.GLContext?.MakeCurrent();
            try
            {
                var handle = new GpuParticleHandle { Backend = this, Count = count, ComputeBackend = true };

                // 8 floats/particle: position.xyz + pad, velocity.xyz + pad --
                // vec4-aligned to sidestep std430's vec3-padding rules.
                var seed = new float[count * 8];
                for (int i = 0; i < count; i++)
                {
                    var p = seedPositions[i];
                    var v = seedVelocities != null ? seedVelocities[i] : default;
                    int b = i * 8;
                    seed[b + 0] = p.X; seed[b + 1] = p.Y; seed[b + 2] = p.Z; seed[b + 3] = 0f;
                    seed[b + 4] = v.X; seed[b + 5] = v.Y; seed[b + 6] = v.Z; seed[b + 7] = 0f;
                }

                handle.Ssbo = _gl.GenBuffer();
                _gl.BindBuffer(GLEnum.ShaderStorageBuffer, handle.Ssbo);
                fixed (float* data = seed)
                    _gl.BufferData(GLEnum.ShaderStorageBuffer, (nuint)(seed.Length * sizeof(float)), data, GLEnum.DynamicDraw);
                _gl.BindBuffer(GLEnum.ShaderStorageBuffer, 0);

                handle.ComputeProgram = CompileComputeProgram(BuildComputeSource(simulationGlsl, count));
                handle.UDeltaTimeLocCompute = _gl.GetUniformLocation(handle.ComputeProgram, "uDeltaTime");
                handle.UTimeLocCompute = _gl.GetUniformLocation(handle.ComputeProgram, "uTime");

                BuildDrawProgram(handle, computeBackend: true, texWidth: 0);
                return handle;
            }
            finally
            {
                _window.GLContext?.Clear();
            }
        }

        private void StepComputeParticles(GpuParticleHandle handle, float deltaTime)
        {
            _gl.UseProgram(handle.ComputeProgram);
            _gl.Uniform1(handle.UDeltaTimeLocCompute, deltaTime);
            _gl.Uniform1(handle.UTimeLocCompute, handle.SimTimeAccumulated);
            _gl.BindBufferBase(GLEnum.ShaderStorageBuffer, 0, handle.Ssbo);
            _gl.DispatchCompute((uint)Math.Ceiling(handle.Count / 64.0), 1, 1);

            // Required before the following DrawParticles() reads the SSBO --
            // without this the vertex shader can see stale or partially-
            // written data (GL gives no ordering guarantee between a compute
            // dispatch and a later draw touching the same buffer otherwise).
            _gl.MemoryBarrier((uint)GLEnum.ShaderStorageBarrierBit);
        }

        // Compiles+links a SINGLE-STAGE compute program -- CompileProgram()
        // (used everywhere else in this class) always links a vertex+fragment
        // pair, which doesn't fit a compute shader's one-stage pipeline.
        private uint CompileComputeProgram(string computeSource)
        {
            uint shader = CompileShader(ShaderType.ComputeShader, computeSource);
            uint program = _gl.CreateProgram();
            _gl.AttachShader(program, shader);
            _gl.LinkProgram(program);
            _gl.GetProgram(program, ProgramPropertyARB.LinkStatus, out int linkStatus);

            if (linkStatus == 0)
            {
                string log = _gl.GetProgramInfoLog(program);
                _gl.DeleteShader(shader);
                _gl.DeleteProgram(program);
                throw new InvalidOperationException($"Error linkeando el compute shader de particulas: {log}");
            }

            _gl.DeleteShader(shader);
            return program;
        }

        // count is baked in as a literal (not a uniform) -- fixed for the
        // system's whole lifetime, no reason to re-upload it every dispatch.
        private static string BuildComputeSource(string snippet, int count)
        {
            return
                "#version 430 core\n" +
                "layout(local_size_x = 64) in;\n\n" +
                "struct Particle { vec4 position; vec4 velocity; };\n" +
                "layout(std430, binding = 0) buffer ParticleBuffer { Particle particles[]; };\n\n" +
                "uniform float uDeltaTime;\n" +
                "uniform float uTime;\n\n" +
                NoiseHelperGlsl + "\n" +
                "void main() {\n" +
                "    uint i = gl_GlobalInvocationID.x;\n" +
                "    if (i >= " + count + "u) return;\n\n" +
                "    vec3 position = particles[i].position.xyz;\n" +
                "    vec3 velocity = particles[i].velocity.xyz;\n\n" +
                "    {\n" +
                snippet + "\n" +
                "    }\n\n" +
                "    particles[i].position = vec4(position, 1.0);\n" +
                "    particles[i].velocity = vec4(velocity, 0.0);\n" +
                "}\n";
        }

        private const string ComputeDrawVertexSource =
            "#version 430 core\n" +
            "struct Particle { vec4 position; vec4 velocity; };\n" +
            "layout(std430, binding = 0) buffer ParticleBuffer { Particle particles[]; };\n" +
            "uniform mat4 uModel;\n" +
            "uniform mat4 uView;\n" +
            "uniform mat4 uProjection;\n" +
            "uniform float uPointSize;\n" +
            "out vec3 vVelocity;\n" +
            "void main() {\n" +
            "    vec3 pos = particles[gl_VertexID].position.xyz;\n" +
            "    vVelocity = particles[gl_VertexID].velocity.xyz;\n" +
            "    gl_Position = uProjection * uView * uModel * vec4(pos, 1.0);\n" +
            "    gl_PointSize = uPointSize;\n" +
            "}\n";

        // =====================================================================
        // Backend B -- ping-pong texture GPGPU (GL 3.3, macOS + universal
        // fallback wherever real compute shaders aren't genuinely available)
        // =====================================================================

        private unsafe GpuParticleHandle CreateTextureParticleSystem(int count, string simulationGlsl, PVector[] seedPositions, PVector[]? seedVelocities)
        {
            _window.GLContext?.MakeCurrent();
            try
            {
                EnsureFullscreenQuad();

                // Smallest roughly-square texture that holds `count` texels,
                // row-major index y*texW+x. Any trailing padding cells past
                // count-1 are simply never drawn (DrawParticles() only ever
                // emits `count` vertices) or read (the sim step only ever
                // touches texels gl_FragCoord can land on, which is exactly
                // texW*texH -- padding cells just simulate harmlessly).
                int texW = (int)Math.Ceiling(Math.Sqrt(count));
                int texH = (int)Math.Ceiling(count / (double)texW);

                var handle = new GpuParticleHandle { Backend = this, Count = count, ComputeBackend = false, TexWidth = texW, TexHeight = texH, CurrentIndex = 0 };

                var posData = new float[texW * texH * 4];
                var velData = new float[texW * texH * 4];
                for (int i = 0; i < count; i++)
                {
                    var p = seedPositions[i];
                    var v = seedVelocities != null ? seedVelocities[i] : default;
                    int b = i * 4;
                    posData[b + 0] = p.X; posData[b + 1] = p.Y; posData[b + 2] = p.Z; posData[b + 3] = 1f;
                    velData[b + 0] = v.X; velData[b + 1] = v.Y; velData[b + 2] = v.Z; velData[b + 3] = 0f;
                }

                var drawBuffers = new[] { GLEnum.ColorAttachment0, GLEnum.ColorAttachment1 };
                for (int slot = 0; slot < 2; slot++)
                {
                    // Slot 0 seeds real data; slot 1 starts uninitialized --
                    // it's the simulation step's WRITE target on the very
                    // first StepParticles() call, so nothing ever reads its
                    // garbage initial contents before that step fills it in.
                    handle.PositionTex[slot] = CreateParticleDataTexture(texW, texH, slot == 0 ? posData : null);
                    handle.VelocityTex[slot] = CreateParticleDataTexture(texW, texH, slot == 0 ? velData : null);

                    handle.Fbo[slot] = _gl.GenFramebuffer();
                    _gl.BindFramebuffer(FramebufferTarget.Framebuffer, handle.Fbo[slot]);
                    _gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, handle.PositionTex[slot], 0);
                    _gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment1, TextureTarget.Texture2D, handle.VelocityTex[slot], 0);
                    _gl.DrawBuffers(2, drawBuffers);

                    var status = _gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
                    if (status != GLEnum.FramebufferComplete)
                        throw new InvalidOperationException($"El framebuffer de particulas (ping-pong, slot {slot}) quedo incompleto: {status}.");
                }
                _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _sampleCount > 0 ? _msaaFbo : _fbo);

                handle.StepProgram = CompileProgram(StepQuadVertexSource, BuildStepFragmentSource(simulationGlsl));
                handle.UPrevPositionLoc = _gl.GetUniformLocation(handle.StepProgram, "uPrevPosition");
                handle.UPrevVelocityLoc = _gl.GetUniformLocation(handle.StepProgram, "uPrevVelocity");
                handle.UDeltaTimeLocStep = _gl.GetUniformLocation(handle.StepProgram, "uDeltaTime");
                handle.UTimeLocStep = _gl.GetUniformLocation(handle.StepProgram, "uTime");

                BuildDrawProgram(handle, computeBackend: false, texWidth: texW);
                return handle;
            }
            finally
            {
                _window.GLContext?.Clear();
            }
        }

        private unsafe uint CreateParticleDataTexture(int width, int height, float[]? initialData)
        {
            uint tex = _gl.GenTexture();
            _gl.BindTexture(TextureTarget.Texture2D, tex);
            // NEAREST, not LINEAR -- this holds raw simulation data (a
            // position/velocity, not a color), interpolating it would
            // corrupt every particle's state.
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Nearest);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Nearest);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);

            if (initialData != null)
            {
                fixed (float* data = initialData)
                    _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba32f, (uint)width, (uint)height, 0, PixelFormat.Rgba, PixelType.Float, data);
            }
            else
            {
                _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba32f, (uint)width, (uint)height, 0, PixelFormat.Rgba, PixelType.Float, null);
            }

            return tex;
        }

        private void StepTextureParticles(GpuParticleHandle handle, float deltaTime)
        {
            int readIndex = handle.CurrentIndex;
            int writeIndex = 1 - readIndex;

            _gl.BindFramebuffer(FramebufferTarget.Framebuffer, handle.Fbo[writeIndex]);
            _gl.Viewport(0, 0, (uint)handle.TexWidth, (uint)handle.TexHeight);
            _gl.Disable(EnableCap.DepthTest); // the ping-pong FBOs have no depth attachment at all

            _gl.UseProgram(handle.StepProgram);
            _gl.ActiveTexture(TextureUnit.Texture0);
            _gl.BindTexture(TextureTarget.Texture2D, handle.PositionTex[readIndex]);
            _gl.Uniform1(handle.UPrevPositionLoc, 0);
            _gl.ActiveTexture(TextureUnit.Texture1);
            _gl.BindTexture(TextureTarget.Texture2D, handle.VelocityTex[readIndex]);
            _gl.Uniform1(handle.UPrevVelocityLoc, 1);
            _gl.Uniform1(handle.UDeltaTimeLocStep, deltaTime);
            _gl.Uniform1(handle.UTimeLocStep, handle.SimTimeAccumulated);

            _gl.BindVertexArray(_quadVao);
            _gl.DrawArrays(GLEnum.TriangleStrip, 0, 4);
            _gl.BindVertexArray(0);

            handle.CurrentIndex = writeIndex;

            // Restore the sketch's own render target/viewport/depth-test
            // state -- this pass just rebound a DIFFERENT fbo, a SMALLER
            // viewport (texW x texH, not the sketch canvas), and disabled
            // depth test. Skipping this would corrupt every Box()/Sphere()/
            // etc. drawn later in the same Draw().
            _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _sampleCount > 0 ? _msaaFbo : _fbo);
            _gl.Viewport(0, 0, (uint)_width, (uint)_height);
            _gl.Enable(EnableCap.DepthTest);
        }

        private unsafe void EnsureFullscreenQuad()
        {
            if (_quadVao != 0)
                return;

            // Two-triangle strip covering NDC space, xy only -- the step
            // fragment shader derives its texel from gl_FragCoord, no UV
            // attribute needed.
            float[] verts = { -1f, -1f, 1f, -1f, -1f, 1f, 1f, 1f };

            _quadVao = _gl.GenVertexArray();
            _quadVbo = _gl.GenBuffer();
            _gl.BindVertexArray(_quadVao);
            _gl.BindBuffer(GLEnum.ArrayBuffer, _quadVbo);
            fixed (float* data = verts)
                _gl.BufferData(GLEnum.ArrayBuffer, (nuint)(verts.Length * sizeof(float)), data, GLEnum.StaticDraw);

            _gl.EnableVertexAttribArray(0);
            _gl.VertexAttribPointer(0, 2, GLEnum.Float, false, 2 * sizeof(float), (void*)0);
            _gl.BindVertexArray(0);
        }

        private const string StepQuadVertexSource =
            "#version 330 core\n" +
            "layout(location = 0) in vec2 aPos;\n" +
            "void main() {\n" +
            "    gl_Position = vec4(aPos, 0.0, 1.0);\n" +
            "}\n";

        private static string BuildStepFragmentSource(string snippet)
        {
            return
                "#version 330 core\n" +
                "layout(location = 0) out vec4 outPosition;\n" +
                "layout(location = 1) out vec4 outVelocity;\n\n" +
                "uniform sampler2D uPrevPosition;\n" +
                "uniform sampler2D uPrevVelocity;\n" +
                "uniform float uDeltaTime;\n" +
                "uniform float uTime;\n\n" +
                NoiseHelperGlsl + "\n" +
                "void main() {\n" +
                "    ivec2 texel = ivec2(gl_FragCoord.xy);\n" +
                "    vec3 position = texelFetch(uPrevPosition, texel, 0).xyz;\n" +
                "    vec3 velocity = texelFetch(uPrevVelocity, texel, 0).xyz;\n\n" +
                "    {\n" +
                snippet + "\n" +
                "    }\n\n" +
                "    outPosition = vec4(position, 1.0);\n" +
                "    outVelocity = vec4(velocity, 0.0);\n" +
                "}\n";
        }

        private static string BuildTextureDrawVertexSource(int texWidth)
        {
            return
                "#version 330 core\n" +
                "uniform sampler2D uPositionTex;\n" +
                "uniform sampler2D uVelocityTex;\n" +
                "uniform mat4 uModel;\n" +
                "uniform mat4 uView;\n" +
                "uniform mat4 uProjection;\n" +
                "uniform float uPointSize;\n" +
                "out vec3 vVelocity;\n" +
                "void main() {\n" +
                "    ivec2 texel = ivec2(gl_VertexID % " + texWidth + ", gl_VertexID / " + texWidth + ");\n" +
                "    vec3 pos = texelFetch(uPositionTex, texel, 0).xyz;\n" +
                "    vVelocity = texelFetch(uVelocityTex, texel, 0).xyz;\n" +
                "    gl_Position = uProjection * uView * uModel * vec4(pos, 1.0);\n" +
                "    gl_PointSize = uPointSize;\n" +
                "}\n";
        }

        // =====================================================================
        // Shared by both backends
        // =====================================================================

        // Builds the point-sprite draw program (vertex shader differs by
        // backend -- SSBO-indexed vs texture-indexed; the fragment shader
        // body is identical either way, only the #version pragma differs --
        // see BuildParticleFragmentSource()) and the empty VAO every particle
        // draw call binds. Core profile requires SOME VAO bound even though
        // this one has zero enabled vertex attributes -- every vertex is
        // fetched by gl_VertexID out of the SSBO/texture instead of a
        // conventional vertex buffer.
        private void BuildDrawProgram(GpuParticleHandle handle, bool computeBackend, int texWidth)
        {
            string vertexSource = computeBackend ? ComputeDrawVertexSource : BuildTextureDrawVertexSource(texWidth);
            string fragmentSource = BuildParticleFragmentSource(computeBackend ? "#version 430 core" : "#version 330 core");

            handle.DrawProgram = CompileProgram(vertexSource, fragmentSource);
            handle.UModelLoc = _gl.GetUniformLocation(handle.DrawProgram, "uModel");
            handle.UViewLoc = _gl.GetUniformLocation(handle.DrawProgram, "uView");
            handle.UProjectionLoc = _gl.GetUniformLocation(handle.DrawProgram, "uProjection");
            handle.UPointSizeLoc = _gl.GetUniformLocation(handle.DrawProgram, "uPointSize");
            handle.UFillColorLoc = _gl.GetUniformLocation(handle.DrawProgram, "uFillColor");
            if (!computeBackend)
            {
                handle.UPositionTexLoc = _gl.GetUniformLocation(handle.DrawProgram, "uPositionTex");
                handle.UVelocityTexLoc = _gl.GetUniformLocation(handle.DrawProgram, "uVelocityTex");
            }

            handle.DrawVao = _gl.GenVertexArray();
        }

        private static string BuildParticleFragmentSource(string versionDirective)
        {
            return
                versionDirective + "\n" +
                "in vec3 vVelocity;\n" +
                "uniform vec4 uFillColor;\n" +
                "out vec4 FragColor;\n" +
                "void main() {\n" +
                "    vec2 c = gl_PointCoord * 2.0 - 1.0;\n" +
                "    if (dot(c, c) > 1.0) discard;\n" + // round sprite, not a square
                "    FragColor = uFillColor;\n" +
                "}\n";
        }

        // Own hash-based value noise (not a copy of any third-party
        // implementation -- simple enough to write directly and avoids any
        // attribution/licensing question). dana_curlNoise3 turns it into a
        // divergence-free-ish vector field via finite differences, the
        // standard cheap trick for GPU curl noise -- exact incompressibility
        // isn't the point here, visually turbulent, non-converging particle
        // motion is. This is the "GPU noise" half of the ROADMAP pitch,
        // deliberately exposed only as helpers a particle snippet can call
        // rather than as a standalone noise API (see the out-of-scope notes
        // on GraphicsContext.3D.cs's CreateGpuParticles()).
        private const string NoiseHelperGlsl =
            "float dana_hash31(vec3 p) {\n" +
            "    p = fract(p * 0.3183099 + vec3(0.1, 0.2, 0.3));\n" +
            "    p *= 17.0;\n" +
            "    return fract(p.x * p.y * p.z * (p.x + p.y + p.z));\n" +
            "}\n\n" +
            "float dana_noise3(vec3 p) {\n" +
            "    vec3 i = floor(p);\n" +
            "    vec3 f = fract(p);\n" +
            "    f = f * f * (3.0 - 2.0 * f);\n" +
            "    float n000 = dana_hash31(i + vec3(0.0, 0.0, 0.0));\n" +
            "    float n100 = dana_hash31(i + vec3(1.0, 0.0, 0.0));\n" +
            "    float n010 = dana_hash31(i + vec3(0.0, 1.0, 0.0));\n" +
            "    float n110 = dana_hash31(i + vec3(1.0, 1.0, 0.0));\n" +
            "    float n001 = dana_hash31(i + vec3(0.0, 0.0, 1.0));\n" +
            "    float n101 = dana_hash31(i + vec3(1.0, 0.0, 1.0));\n" +
            "    float n011 = dana_hash31(i + vec3(0.0, 1.0, 1.0));\n" +
            "    float n111 = dana_hash31(i + vec3(1.0, 1.0, 1.0));\n" +
            "    float nx00 = mix(n000, n100, f.x);\n" +
            "    float nx10 = mix(n010, n110, f.x);\n" +
            "    float nx01 = mix(n001, n101, f.x);\n" +
            "    float nx11 = mix(n011, n111, f.x);\n" +
            "    float nxy0 = mix(nx00, nx10, f.y);\n" +
            "    float nxy1 = mix(nx01, nx11, f.y);\n" +
            "    return mix(nxy0, nxy1, f.z) * 2.0 - 1.0;\n" +
            "}\n\n" +
            "vec3 dana_curlNoise3(vec3 p) {\n" +
            "    const float e = 0.1;\n" +
            "    float n1, n2, a, b;\n" +
            "    vec3 curl;\n" +
            "    n1 = dana_noise3(p + vec3(0.0, e, 0.0)); n2 = dana_noise3(p - vec3(0.0, e, 0.0)); a = (n1 - n2) / (2.0 * e);\n" +
            "    n1 = dana_noise3(p + vec3(0.0, 0.0, e)); n2 = dana_noise3(p - vec3(0.0, 0.0, e)); b = (n1 - n2) / (2.0 * e);\n" +
            "    curl.x = a - b;\n" +
            "    n1 = dana_noise3(p + vec3(0.0, 0.0, e)); n2 = dana_noise3(p - vec3(0.0, 0.0, e)); a = (n1 - n2) / (2.0 * e);\n" +
            "    n1 = dana_noise3(p + vec3(e, 0.0, 0.0)); n2 = dana_noise3(p - vec3(e, 0.0, 0.0)); b = (n1 - n2) / (2.0 * e);\n" +
            "    curl.y = a - b;\n" +
            "    n1 = dana_noise3(p + vec3(e, 0.0, 0.0)); n2 = dana_noise3(p - vec3(e, 0.0, 0.0)); a = (n1 - n2) / (2.0 * e);\n" +
            "    n1 = dana_noise3(p + vec3(0.0, e, 0.0)); n2 = dana_noise3(p - vec3(0.0, e, 0.0)); b = (n1 - n2) / (2.0 * e);\n" +
            "    curl.z = a - b;\n" +
            "    return curl;\n" +
            "}\n";
    }
}
