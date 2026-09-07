using System;
using System.Collections.Generic;
using System.Numerics;
using SkiaSharp;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using Silk.NET.Maths;

namespace DanaProcessing
{
    /// <summary>
    /// One active light, as recorded by AmbientLight()/DirectionalLight()/
    /// PointLight()/SpotLight() -- flattened straight into the uLight*
    /// uniform arrays each draw call rather than kept as GL state, since
    /// the light LIST itself is rebuilt every frame (see BeginFrame()'s
    /// _lights.Clear() -- matches Processing's own documented behavior:
    /// lights() must be called every draw() to stay active).
    /// </summary>
    internal readonly struct Light3D
    {
        public readonly int Type; // Renderer3DBackend.LightTypeAmbient/Directional/Point/Spot
        public readonly Vector3 Color;
        public readonly Vector3 Position;    // point / spot / positional ambient
        public readonly Vector3 Direction;   // directional / spot (points FROM the light, like Processing)
        public readonly Vector3 Falloff;     // constant, linear, quadratic -- point/spot/positional ambient only
        public readonly float SpotAngle;
        public readonly float SpotConcentration;
        public readonly Vector3 SpecularColor;
        public readonly bool HasPosition;    // false only for the no-position AmbientLight() overload (infinite, no falloff)

        public Light3D(int type, Vector3 color, Vector3 position, Vector3 direction, Vector3 falloff, float spotAngle, float spotConcentration, Vector3 specularColor, bool hasPosition)
        {
            Type = type;
            Color = color;
            Position = position;
            Direction = direction;
            Falloff = falloff;
            SpotAngle = spotAngle;
            SpotConcentration = spotConcentration;
            SpecularColor = specularColor;
            HasPosition = hasPosition;
        }
    }

    /// <summary>
    /// First spike of a GPU-backed IGraphicsBackend: OpenGL 3.3 (core
    /// profile) via Silk.NET, rendering into an offscreen framebuffer. Two
    /// primitives (Box(), Sphere()), a full camera/projection API
    /// (Camera()/Perspective()/Ortho()/Frustum()), a multi-light shading
    /// model (Lights()/AmbientLight()/DirectionalLight()/PointLight()/
    /// SpotLight(), up to 8 lights like real Processing), and Material
    /// Properties (Ambient()/Specular()/Emissive()/Shininess()). No
    /// PShader yet — see the "not yet built" list at the bottom of this
    /// file for that plus beginCamera()/endCamera() and normal().
    ///
    /// REQUIRED NUGET PACKAGES (not restorable in the sandbox this was
    /// written in — add these locally and expect small fixes on first
    /// build; API surface across Silk.NET versions shifts):
    ///   Silk.NET.OpenGL
    ///   Silk.NET.Windowing
    /// (Silk.NET.Core / Silk.NET.Maths come along as transitive dependencies.)
    ///
    /// HOW A FRAME WORKS
    /// 1. BeginFrame(): bind our FBO, clear the depth buffer (color gets
    ///    cleared by Background(), same as the 2D path), reset the model
    ///    matrix stack, and clear the active light list -- Processing
    ///    itself requires lights()/pointLight()/etc. to be called every
    ///    draw() to stay active, so this mirrors that exactly.
    /// 2. Sketch/PGraphics code calls PushMatrix/Translate/RotateX/.../Box —
    ///    each Box()/Sphere() issues one draw call against the current
    ///    model matrix and the current light list.
    /// 3. EndFrame(): glReadPixels the FBO's color attachment back into a
    ///    plain Skia raster Surface — that's what IGraphicsBackend.Canvas/
    ///    Surface expose, so Get()/Save()/LoadPixels() work identically
    ///    whether a PGraphics is 2D or 3D. Any Fill()/Text()/Rect() called
    ///    AFTER the 3D geometry in the same frame draws with Skia directly
    ///    on top of that raster copy — 2D composited over 3D, not the fully
    ///    unified Processing P3D pipeline (see the roadmap note below).
    ///
    /// COORDINATE CONVENTION: matrices are System.Numerics.Matrix4x4,
    /// which is row-vector (v' = v * M, so M1 * M2 applies M1 first) and
    /// stored row-major in memory. GLSL wants idiomatic column-vector
    /// shaders (gl_Position = P * V * M * v). Feeding GL our row-major
    /// floats while telling it they're column-major — i.e. UniformMatrix4
    /// with transpose: FALSE — makes GL read rows as columns, which is
    /// exactly a transpose, and transposing a row-vector matrix gives you
    /// the equivalent column-vector matrix for the same transform. Passing
    /// transpose: true does the opposite (cancels that free transpose) and
    /// silently breaks the perspective divide — don't "fix" this to true.
    /// </summary>
    internal sealed class Renderer3DBackend : IGraphicsBackend
    {
        public RendererKind Kind => RendererKind.Renderer3D;
        public SKCanvas Canvas { get; }
        public SKSurface Surface { get; }

        private readonly int _width;
        private readonly int _height;

        // Hidden window: exists purely to obtain a valid OpenGL context
        // without putting anything on screen. Never Run(), never shown.
        private readonly IWindow _window;
        private readonly GL _gl;

        private uint _fbo, _colorTexture, _depthRbo;
        private uint _shaderProgram;
        private uint _cubeVao, _cubeVbo;
        private int _uModelLoc, _uViewLoc, _uProjectionLoc, _uFillColorLoc, _uEyePosLoc;

        // Material uniforms, backing Ambient()/Specular()/Emissive()/
        // Shininess() (see the Material Properties setters + UploadLighting()
        // further down). Defaults match Processing's own: material ambient
        // reflectance implicitly follows the fill color until ambient() is
        // called explicitly (see _materialAmbient below), specular/emissive
        // default to black, shininess defaults to 0 (no specular highlight
        // at all until shininess() says otherwise).
        private int _uMaterialAmbientLoc, _uMaterialSpecularLoc, _uMaterialEmissiveLoc, _uMaterialShininessLoc;

        // Lights -- see the LightTypeXxx consts and Light3D above. MaxLights
        // matches Processing's own cap (PGraphicsOpenGL.MAX_LIGHTS = 8).
        private const int MaxLights = 8;
        private const int LightTypeAmbient = 0;
        private const int LightTypeDirectional = 1;
        private const int LightTypePoint = 2;
        private const int LightTypeSpot = 3;

        private readonly List<Light3D> _lights = new();

        // Style state for lights created AFTER these are set -- same
        // "affects only what's created after it" contract as Fill()/
        // Stroke(), so (unlike the light LIST) these are NOT reset in
        // BeginFrame(); only their construction-time defaults matter,
        // which match Processing's own: falloff(1,0,0) (no falloff at
        // all), specular(0,0,0) (black -- no specular contribution).
        private Vector3 _currentLightFalloff = new(1f, 0f, 0f);
        private Vector3 _currentLightSpecular = Vector3.Zero;

        // Material Properties -- https://processing.org/reference/ambient_.html
        // and siblings (specular()/emissive()/shininess()). Persistent style
        // state like Fill()/Stroke(): set once, applies to every shape drawn
        // afterward, and NOT reset in BeginFrame() -- unlike the light list,
        // Processing doesn't require these to be re-declared every frame.
        // _materialAmbient is nullable: null (the default) means "track the
        // current fill color", which is Processing's own default before
        // ambient() is ever called; a non-null value means ambient() was
        // called explicitly and detaches from fill() tracking (a
        // simplification -- real Processing re-attaches ambient to fill()
        // in a couple of edge-case call orderings this doesn't replicate).
        private Vector3? _materialAmbient;
        private Vector3 _materialSpecular = Vector3.Zero;
        private Vector3 _materialEmissive = Vector3.Zero;
        private float _materialShininess;

        // Uniform locations for the uLight* arrays, fetched once per index
        // in SetUpShader() (safer than assuming GL lays array elements out
        // at contiguous locations, which isn't guaranteed by spec even
        // though most desktop drivers do it in practice).
        private int _uLightCountLoc;
        private readonly int[] _uLightTypeLoc = new int[MaxLights];
        private readonly int[] _uLightColorLoc = new int[MaxLights];
        private readonly int[] _uLightPositionLoc = new int[MaxLights];
        private readonly int[] _uLightDirectionLoc = new int[MaxLights];
        private readonly int[] _uLightFalloffLoc = new int[MaxLights];
        private readonly int[] _uLightSpotLoc = new int[MaxLights];
        private readonly int[] _uLightSpecularLoc = new int[MaxLights];
        private readonly int[] _uLightHasPositionLoc = new int[MaxLights];

        // Sphere mesh: unlike the cube (a fixed 36-vertex table generated
        // once in SetUpCubeMesh()), this depends on SphereDetail(ures,vres)
        // and so is built lazily — see SetSphereDetail()/EnsureSphereMesh().
        private uint _sphereVao, _sphereVbo;
        private int _sphereVertexCount;
        private int _sphereUres = SphereVertexData.DefaultDetail;
        private int _sphereVres = SphereVertexData.DefaultDetail;
        private bool _sphereMeshBuilt;

        private Matrix4x4 _model = Matrix4x4.Identity;
        private readonly Stack<Matrix4x4> _modelStack = new();
        private Matrix4x4 _view;
        private Matrix4x4 _projection;

        // World-space camera position, tracked alongside _view by
        // SetDefaultView()/SetView() -- used for the specular half-vector.
        // Not derived from _view each draw call since CreateLookAt doesn't
        // hand the eye position back out; cheaper to just remember it.
        private Vector3 _eyePosition;

        // OpenGL's NDC is Y-up (+Y = top of the rendered image once
        // EndFrame() undoes the separate bottom-left-origin readback
        // quirk). Everything else in DanaProcessing -- the 2D canvas,
        // MouseY, Height/2 as a center, translate(x, Height/2, z) in every
        // 3D sample -- is Y-down, matching Processing's own convention.
        // With up=(0,1,0) on the default camera (which is what real
        // Processing's camera() also uses), that mismatch means world +Y
        // renders toward the TOP of the screen instead of the bottom --
        // invisible on a centered, symmetric shape like a lone rotating
        // Box(), but very visible on anything whose Y position is driven
        // by MouseY (a light following the mouse moves opposite to the
        // mouse vertically). Real Processing's P3D avoids this by baking
        // a Y-flip into its own projection matrix; every SetXxxProjection
        // method below (SetDefaultProjection/SetPerspective/SetOrtho/
        // SetFrustum) does the same by post-multiplying with this. Post-
        // multiplying (rather than negating individual matrix terms like
        // M22) flips the FINAL clip-space Y regardless of which of the
        // four projection builders produced the matrix, symmetric or not.
        private static readonly Matrix4x4 ClipSpaceYFlip = Matrix4x4.CreateScale(1f, -1f, 1f);

        // Processing's own "cameraZ": how far back the default eye sits so
        // a 60 degree vertical FOV frames the whole canvas. Computed once
        // in SetDefaultCamera() and reused as the reference distance for
        // every other camera/projection default — Camera()'s default eye,
        // Perspective()'s default near/far (cameraZ/10, cameraZ*10), and
        // Ortho()'s default far (cameraZ*10) all key off this single value,
        // exactly like PGraphicsOpenGL does internally in real Processing.
        private float _defaultEyeZ;

        private bool _disposed;

        private Renderer3DBackend(int width, int height, IWindow window, GL gl, SKSurface surface)
        {
            _width = width;
            _height = height;
            _window = window;
            _gl = gl;
            Surface = surface;
            Canvas = surface.Canvas;
        }

        public static Renderer3DBackend Create(int width, int height)
        {
            var options = WindowOptions.Default with
            {
                Size = new Vector2D<int>(Math.Max(1, width), Math.Max(1, height)),
                IsVisible = false,
                Title = "DanaProcessing (offscreen GL context)",
                API = new GraphicsAPI(ContextAPI.OpenGL, ContextProfile.Core, ContextFlags.Default, new APIVersion(3, 3))
            };

            var window = Window.Create(options);
            window.Initialize(); // no window.Run() -- we drive rendering manually, frame by frame.
            var gl = GL.GetApi(window);

            var raster = SKSurface.Create(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul))
                ?? throw new InvalidOperationException($"No se pudo crear la superficie de lectura de {width}x{height}.");

            var backend = new Renderer3DBackend(width, height, window, gl, raster);
            backend.SetUpFramebuffer();
            backend.SetUpShader();
            backend.SetUpCubeMesh();
            backend.SetDefaultCamera();

            // WGL (and GL contexts generally) only allow a context to be
            // current on ONE thread at a time. Everything above ran on
            // whatever thread called Create() (Setup() -> Avalonia's UI
            // thread), which leaves the context current THERE. But
            // BeginFrame()/EndFrame() run every frame from Avalonia's
            // render thread instead — a different thread — so without this
            // release, that thread's MakeCurrent() fails with exactly
            // "WGL: Failed to make context current: The requested resource
            // is in use." Un-currenting it here frees it up for
            // BeginFrame() to successfully claim on the render thread.
            window.GLContext?.Clear();

            return backend;
        }

        private void SetUpFramebuffer()
        {
            _fbo = _gl.GenFramebuffer();
            _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _fbo);

            _colorTexture = _gl.GenTexture();
            _gl.BindTexture(TextureTarget.Texture2D, _colorTexture);
            unsafe
            {
                _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8, (uint)_width, (uint)_height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, null);
            }
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
            _gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, _colorTexture, 0);

            _depthRbo = _gl.GenRenderbuffer();
            _gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, _depthRbo);
            _gl.RenderbufferStorage(RenderbufferTarget.Renderbuffer, InternalFormat.DepthComponent24, (uint)_width, (uint)_height);
            _gl.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, RenderbufferTarget.Renderbuffer, _depthRbo);

            var status = _gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
            if (status != GLEnum.FramebufferComplete)
                throw new InvalidOperationException($"El framebuffer offscreen quedo incompleto: {status}.");

            _gl.Viewport(0, 0, (uint)_width, (uint)_height);
            _gl.Enable(EnableCap.DepthTest);
        }

        // Multi-light Blinn-Phong shader. uLightCount == 0 (the default --
        // matches Processing exactly: no lights() call means NO shading at
        // all) short-circuits to a flat, fully-unlit uFillColor -- ignores
        // normals entirely, same as Processing's own unlit default.
        // Otherwise: ambient lights add flat color (optionally falloff-
        // attenuated if positional); directional/point/spot lights add a
        // Lambertian diffuse term plus a Blinn-Phong specular term (the
        // specular term is zero for now since material shininess defaults
        // to 0 -- see the Material Properties note on the class doc above).
        private const string VertexShaderSource = @"
#version 330 core
layout(location = 0) in vec3 aPos;
layout(location = 1) in vec3 aNormal;
uniform mat4 uModel;
uniform mat4 uView;
uniform mat4 uProjection;
out vec3 vNormal;
out vec3 vWorldPos;
void main()
{
    vec4 worldPos = uModel * vec4(aPos, 1.0);
    vWorldPos = worldPos.xyz;
    gl_Position = uProjection * uView * worldPos;
    vNormal = normalize(mat3(transpose(inverse(uModel))) * aNormal);
}";

        private const int ShaderMaxLights = 8; // must match MaxLights above -- GLSL can't take a C# const

        private const string FragmentShaderSource = @"
#version 330 core
#define MAX_LIGHTS 8
in vec3 vNormal;
in vec3 vWorldPos;
out vec4 FragColor;

uniform vec4 uFillColor;
uniform vec3 uMaterialAmbient;
uniform vec3 uMaterialSpecular;
uniform vec3 uMaterialEmissive;
uniform float uMaterialShininess;
uniform vec3 uEyePos;

uniform int uLightCount;
uniform int uLightType[MAX_LIGHTS];
uniform vec3 uLightColor[MAX_LIGHTS];
uniform vec3 uLightPosition[MAX_LIGHTS];
uniform vec3 uLightDirection[MAX_LIGHTS];
uniform vec3 uLightFalloff[MAX_LIGHTS];
uniform vec2 uLightSpot[MAX_LIGHTS]; // x = angle (radians), y = concentration
uniform vec3 uLightSpecular[MAX_LIGHTS];
uniform int uLightHasPosition[MAX_LIGHTS]; // ambient lights only: 0 = infinite (no falloff), 1 = positional

void main()
{
    if (uLightCount <= 0)
    {
        // Processing's true default: no lights() called means shapes draw
        // flat-colored, completely unlit -- normals aren't even consulted.
        FragColor = uFillColor;
        return;
    }

    vec3 n = normalize(vNormal);
    vec3 viewDir = normalize(uEyePos - vWorldPos);

    vec3 totalAmbient = vec3(0.0);
    vec3 totalDiffuse = vec3(0.0);
    vec3 totalSpecular = vec3(0.0);

    for (int i = 0; i < uLightCount; i++)
    {
        vec3 lightColor = uLightColor[i];

        if (uLightType[i] == 0)
        {
            float atten = 1.0;
            if (uLightHasPosition[i] == 1)
            {
                float dist = length(uLightPosition[i] - vWorldPos);
                vec3 fo = uLightFalloff[i];
                atten = 1.0 / max(fo.x + fo.y * dist + fo.z * dist * dist, 0.0001);
            }
            totalAmbient += lightColor * atten;
            continue;
        }

        vec3 lightDir;
        float atten = 1.0;

        if (uLightType[i] == 1)
        {
            lightDir = normalize(-uLightDirection[i]);
        }
        else
        {
            vec3 toLight = uLightPosition[i] - vWorldPos;
            float dist = length(toLight);
            lightDir = toLight / max(dist, 0.0001);
            vec3 fo = uLightFalloff[i];
            atten = 1.0 / max(fo.x + fo.y * dist + fo.z * dist * dist, 0.0001);

            if (uLightType[i] == 3)
            {
                vec3 spotDir = normalize(uLightDirection[i]);
                float cosAngle = dot(-lightDir, spotDir);
                float cosCutoff = cos(uLightSpot[i].x);
                if (cosAngle < cosCutoff)
                    atten = 0.0;
                else
                    atten *= pow(cosAngle, uLightSpot[i].y);
            }
        }

        float diff = max(dot(n, lightDir), 0.0);
        totalDiffuse += lightColor * diff * atten;

        if (uMaterialShininess > 0.0)
        {
            vec3 halfVec = normalize(lightDir + viewDir);
            float spec = pow(max(dot(n, halfVec), 0.0), uMaterialShininess);
            totalSpecular += lightColor * uLightSpecular[i] * spec * atten;
        }
    }

    // Ambient reflectance uses uMaterialAmbient (defaults to the fill
    // color on the C# side -- see UploadLighting() -- until ambient() is
    // called explicitly); diffuse reflectance always follows the fill
    // color, matching Processing (there's no separate diffuse() call).
    vec3 rgb = uMaterialEmissive
             + uMaterialAmbient * totalAmbient
             + uFillColor.rgb * totalDiffuse
             + uMaterialSpecular * totalSpecular;

    FragColor = vec4(rgb, uFillColor.a);
}";

        private void SetUpShader()
        {
            uint vert = CompileShader(ShaderType.VertexShader, VertexShaderSource);
            uint frag = CompileShader(ShaderType.FragmentShader, FragmentShaderSource);

            _shaderProgram = _gl.CreateProgram();
            _gl.AttachShader(_shaderProgram, vert);
            _gl.AttachShader(_shaderProgram, frag);
            _gl.LinkProgram(_shaderProgram);
            _gl.GetProgram(_shaderProgram, ProgramPropertyARB.LinkStatus, out int linkStatus);
            if (linkStatus == 0)
                throw new InvalidOperationException($"Error linkeando el shader 3D: {_gl.GetProgramInfoLog(_shaderProgram)}");

            _gl.DeleteShader(vert);
            _gl.DeleteShader(frag);

            _uModelLoc = _gl.GetUniformLocation(_shaderProgram, "uModel");
            _uViewLoc = _gl.GetUniformLocation(_shaderProgram, "uView");
            _uProjectionLoc = _gl.GetUniformLocation(_shaderProgram, "uProjection");
            _uFillColorLoc = _gl.GetUniformLocation(_shaderProgram, "uFillColor");
            _uEyePosLoc = _gl.GetUniformLocation(_shaderProgram, "uEyePos");
            _uMaterialAmbientLoc = _gl.GetUniformLocation(_shaderProgram, "uMaterialAmbient");
            _uMaterialSpecularLoc = _gl.GetUniformLocation(_shaderProgram, "uMaterialSpecular");
            _uMaterialEmissiveLoc = _gl.GetUniformLocation(_shaderProgram, "uMaterialEmissive");
            _uMaterialShininessLoc = _gl.GetUniformLocation(_shaderProgram, "uMaterialShininess");

            _uLightCountLoc = _gl.GetUniformLocation(_shaderProgram, "uLightCount");
            for (int i = 0; i < MaxLights; i++)
            {
                _uLightTypeLoc[i] = _gl.GetUniformLocation(_shaderProgram, $"uLightType[{i}]");
                _uLightColorLoc[i] = _gl.GetUniformLocation(_shaderProgram, $"uLightColor[{i}]");
                _uLightPositionLoc[i] = _gl.GetUniformLocation(_shaderProgram, $"uLightPosition[{i}]");
                _uLightDirectionLoc[i] = _gl.GetUniformLocation(_shaderProgram, $"uLightDirection[{i}]");
                _uLightFalloffLoc[i] = _gl.GetUniformLocation(_shaderProgram, $"uLightFalloff[{i}]");
                _uLightSpotLoc[i] = _gl.GetUniformLocation(_shaderProgram, $"uLightSpot[{i}]");
                _uLightSpecularLoc[i] = _gl.GetUniformLocation(_shaderProgram, $"uLightSpecular[{i}]");
                _uLightHasPositionLoc[i] = _gl.GetUniformLocation(_shaderProgram, $"uLightHasPosition[{i}]");
            }
        }

        private uint CompileShader(ShaderType type, string source)
        {
            uint shader = _gl.CreateShader(type);
            _gl.ShaderSource(shader, source);
            _gl.CompileShader(shader);
            _gl.GetShader(shader, ShaderParameterName.CompileStatus, out int status);
            if (status == 0)
                throw new InvalidOperationException($"Error compilando shader {type}: {_gl.GetShaderInfoLog(shader)}");
            return shader;
        }

        // A unit cube (1x1x1, centered at the origin) with per-face normals
        // -- Box(w,h,d) scales this via the model matrix rather than baking
        // dimensions into the mesh, so there's exactly one VBO regardless of
        // how many differently-sized boxes a sketch draws.
        private void SetUpCubeMesh()
        {
            float[] vertices = CubeVertexData.PositionsAndNormals;

            _cubeVao = _gl.GenVertexArray();
            _cubeVbo = _gl.GenBuffer();
            _gl.BindVertexArray(_cubeVao);
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _cubeVbo);
            unsafe
            {
                fixed (float* v = vertices)
                    _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(vertices.Length * sizeof(float)), v, BufferUsageARB.StaticDraw);
            }

            const uint stride = 6 * sizeof(float);
            unsafe
            {
                _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, (void*)0);
                _gl.EnableVertexAttribArray(0);
                _gl.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, stride, (void*)(3 * sizeof(float)));
                _gl.EnableVertexAttribArray(1);
            }
            _gl.BindVertexArray(0);
        }

        // Uploads (or re-uploads) the unit sphere mesh at the current
        // _sphereUres/_sphereVres resolution. Called lazily from
        // DrawSphere() rather than eagerly like SetUpCubeMesh() -- the
        // detail level can change at runtime via SphereDetail(), and a
        // sketch that never calls Sphere() shouldn't pay for a mesh upload
        // at all. Same VAO/VBO layout as the cube: position+normal,
        // stride 6 floats, no index buffer.
        private void EnsureSphereMesh()
        {
            if (_sphereMeshBuilt)
                return;

            float[] vertices = SphereVertexData.Generate(_sphereUres, _sphereVres);
            _sphereVertexCount = vertices.Length / 6;

            if (_sphereVao == 0)
            {
                _sphereVao = _gl.GenVertexArray();
                _sphereVbo = _gl.GenBuffer();
            }

            _gl.BindVertexArray(_sphereVao);
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _sphereVbo);
            unsafe
            {
                fixed (float* v = vertices)
                    _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(vertices.Length * sizeof(float)), v, BufferUsageARB.StaticDraw);
            }

            const uint stride = 6 * sizeof(float);
            unsafe
            {
                _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, (void*)0);
                _gl.EnableVertexAttribArray(0);
                _gl.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, stride, (void*)(3 * sizeof(float)));
                _gl.EnableVertexAttribArray(1);
            }
            _gl.BindVertexArray(0);

            _sphereMeshBuilt = true;
        }

        /// <summary>Processing's own default camera: eye at the canvas center pulled back far enough for a 60 degree vertical FOV, looking straight at the canvas center on the z=0 plane, +Y up. Also caches _defaultEyeZ, the reference distance every other Camera()/Perspective()/Ortho() default keys off.</summary>
        private void SetDefaultCamera()
        {
            float fovRadians = MathF.PI / 3f; // 60 degrees, matches Processing's default
            _defaultEyeZ = (_height / 2f) / MathF.Tan(fovRadians / 2f);
            SetDefaultView();
            SetDefaultProjection();
        }

        // =====================================================================
        // Camera / projection -- https://processing.org/reference/camera_.html
        // and siblings. Called from GraphicsContext.3D.cs, gated there on
        // Renderer == RendererKind.Renderer3D so 2D contexts never reach here.
        // =====================================================================

        /// <summary>Resets the view matrix to Processing's default camera — like camera() called with no arguments.</summary>
        public void SetDefaultView()
        {
            var eye = new Vector3(_width / 2f, _height / 2f, _defaultEyeZ);
            var center = new Vector3(_width / 2f, _height / 2f, 0f);
            _view = Matrix4x4.CreateLookAt(eye, center, new Vector3(0, 1, 0));
            _eyePosition = eye;
        }

        /// <summary>Sets the view matrix from an explicit eye/center/up triple, like Processing's camera(eyeX, eyeY, eyeZ, centerX, centerY, centerZ, upX, upY, upZ).</summary>
        public void SetView(float eyeX, float eyeY, float eyeZ, float centerX, float centerY, float centerZ, float upX, float upY, float upZ)
        {
            var eye = new Vector3(eyeX, eyeY, eyeZ);
            _view = Matrix4x4.CreateLookAt(eye, new Vector3(centerX, centerY, centerZ), new Vector3(upX, upY, upZ));
            _eyePosition = eye;
        }

        /// <summary>Resets the projection matrix to Processing's default perspective — like perspective() called with no arguments.</summary>
        public void SetDefaultProjection()
        {
            float fovRadians = MathF.PI / 3f;
            _projection = Matrix4x4.CreatePerspectiveFieldOfView(fovRadians, (float)_width / _height, _defaultEyeZ / 10f, _defaultEyeZ * 10f) * ClipSpaceYFlip;
        }

        /// <summary>Sets a symmetric perspective projection matrix, like Processing's perspective(fovy, aspect, zNear, zFar). fovy is the vertical field of view in radians.</summary>
        public void SetPerspective(float fovRadians, float aspect, float zNear, float zFar)
        {
            _projection = Matrix4x4.CreatePerspectiveFieldOfView(fovRadians, aspect, zNear, zFar) * ClipSpaceYFlip;
        }

        /// <summary>Resets the projection matrix to Processing's default orthographic volume — like ortho() called with no arguments.</summary>
        public void SetDefaultOrtho() => SetOrtho(-_width / 2f, _width / 2f, -_height / 2f, _height / 2f);

        /// <summary>Sets an orthographic projection over the given X/Y clipping volume, using Processing's default near/far (0 and 10x the default eye distance), like Processing's ortho(left, right, bottom, top).</summary>
        public void SetOrtho(float left, float right, float bottom, float top) => SetOrtho(left, right, bottom, top, 0f, _defaultEyeZ * 10f);

        /// <summary>Sets an orthographic projection matrix, like Processing's ortho(left, right, bottom, top, near, far). Unlike Perspective(), nothing gets smaller with distance — useful for isometric-style views.</summary>
        public void SetOrtho(float left, float right, float bottom, float top, float near, float far)
        {
            _projection = Matrix4x4.CreateOrthographicOffCenter(left, right, bottom, top, near, far) * ClipSpaceYFlip;
        }

        /// <summary>Sets a general (possibly asymmetric) perspective projection matrix from explicit clipping-plane coordinates, like Processing's frustum(left, right, bottom, top, near, far). near must be greater than zero; far must be greater than near.</summary>
        public void SetFrustum(float left, float right, float bottom, float top, float near, float far)
        {
            _projection = Matrix4x4.CreatePerspectiveOffCenter(left, right, bottom, top, near, far) * ClipSpaceYFlip;
        }

        /// <summary>Logs the current view (camera) matrix to the console, like Processing's printCamera().</summary>
        public void PrintCamera() => DanaLogger.Info(FormatMatrix("camera", _view));

        /// <summary>Logs the current projection matrix to the console, like Processing's printProjection().</summary>
        public void PrintProjection() => DanaLogger.Info(FormatMatrix("projection", _projection));

        // Raw row-major dump (same convention as everything else in this
        // file — see the COORDINATE CONVENTION note at the top) rather than
        // Processing's own printMatrix() formatting, since there's no
        // PMatrix3D here to delegate to.
        private static string FormatMatrix(string label, Matrix4x4 m) =>
            $"{label} matrix:\n" +
            $"[{m.M11,10:F4} {m.M12,10:F4} {m.M13,10:F4} {m.M14,10:F4}]\n" +
            $"[{m.M21,10:F4} {m.M22,10:F4} {m.M23,10:F4} {m.M24,10:F4}]\n" +
            $"[{m.M31,10:F4} {m.M32,10:F4} {m.M33,10:F4} {m.M34,10:F4}]\n" +
            $"[{m.M41,10:F4} {m.M42,10:F4} {m.M43,10:F4} {m.M44,10:F4}]";

        // =====================================================================
        // Lights -- https://processing.org/reference/lights_.html and
        // siblings. The active light LIST resets every BeginFrame() (see
        // below); LightFalloff()/LightSpecular() are style state that
        // persists like Fill()/Stroke() and only affects lights added
        // afterwards. All *Light() colors come in as Processing-style
        // 0-255 components and get normalized to 0-1 for the shader here,
        // matching how _fillPaint/_strokePaint already handle color.
        // =====================================================================

        /// <summary>Sets Processing's own default lighting rig, like lights(): a mid-gray ambient light plus a mid-gray directional light shining straight along -Z (from the camera into the scene), with falloff(1,0,0) and specular(0,0,0). Equivalent to calling AmbientLight(128,128,128) then DirectionalLight(128,128,128, 0,0,-1) with those falloff/specular defaults.</summary>
        public void SetDefaultLights()
        {
            _lights.Clear();
            SetLightFalloff(1f, 0f, 0f);
            SetLightSpecular(0f, 0f, 0f);
            AddAmbientLight(128, 128, 128);
            AddDirectionalLight(128, 128, 128, 0, 0, -1);
        }

        /// <summary>Turns off all lighting, like noLights(). Box()/Sphere() go back to flat, fully-unlit Fill() color -- Processing's own default before lights() is ever called.</summary>
        public void ClearLights() => _lights.Clear();

        /// <summary>Sets the falloff (constant, linear, quadratic attenuation) applied to point/spot/positional-ambient lights added AFTER this call, like lightFalloff(constant, linear, quadratic). Doesn't affect lights already added.</summary>
        public void SetLightFalloff(float constant, float linear, float quadratic) => _currentLightFalloff = new Vector3(constant, linear, quadratic);

        /// <summary>Sets the specular color used by lights added AFTER this call when computing highlights, like lightSpecular(r, g, b). Has no visible effect until a material shininess is set (Material Properties, not yet built) -- see the class doc note.</summary>
        public void SetLightSpecular(float r, float g, float b) => _currentLightSpecular = Rgb01(r, g, b);

        // =====================================================================
        // Material Properties -- https://processing.org/reference/ambient_.html
        // and siblings. Same persistence contract as LightFalloff()/
        // LightSpecular() above -- and same contract as Fill()/Stroke():
        // set once, applies to every shape drawn afterward, NOT reset
        // per frame (unlike the light list itself).
        // =====================================================================

        /// <summary>Sets the ambient reflectance color (r, g, b, each 0-255) for shapes drawn after this call, like ambient(r, g, b). Detaches from the fill-color default (see _materialAmbient's doc comment) until this is called again.</summary>
        public void SetMaterialAmbient(float r, float g, float b) => _materialAmbient = Rgb01(r, g, b);

        /// <summary>Sets the specular reflectance color (r, g, b, each 0-255) used for highlights on shapes drawn after this call, like specular(r, g, b). Has no visible effect until SetMaterialShininess() is above 0.</summary>
        public void SetMaterialSpecular(float r, float g, float b) => _materialSpecular = Rgb01(r, g, b);

        /// <summary>Sets the emissive color (r, g, b, each 0-255) for shapes drawn after this call, like emissive(r, g, b). Added on top of everything else regardless of lighting -- makes a surface look like it's glowing on its own, without it actually casting light onto other shapes.</summary>
        public void SetMaterialEmissive(float r, float g, float b) => _materialEmissive = Rgb01(r, g, b);

        /// <summary>Sets the shininess exponent used by the Blinn-Phong specular term for shapes drawn after this call, like shininess(shine). 0 (the default) disables the specular highlight entirely; higher values give a smaller, glossier highlight.</summary>
        public void SetMaterialShininess(float shine) => _materialShininess = shine;

        /// <summary>Adds an ambient light with no position (uniform, unattenuated), like ambientLight(r, g, b).</summary>
        public void AddAmbientLight(float r, float g, float b) =>
            AddLightClamped(new Light3D(LightTypeAmbient, Rgb01(r, g, b), Vector3.Zero, Vector3.Zero, _currentLightFalloff, 0f, 0f, _currentLightSpecular, hasPosition: false));

        /// <summary>Adds an ambient light positioned at (x, y, z), attenuated by the current LightFalloff() as distance from that position increases, like ambientLight(r, g, b, x, y, z).</summary>
        public void AddAmbientLight(float r, float g, float b, float x, float y, float z) =>
            AddLightClamped(new Light3D(LightTypeAmbient, Rgb01(r, g, b), new Vector3(x, y, z), Vector3.Zero, _currentLightFalloff, 0f, 0f, _currentLightSpecular, hasPosition: true));

        /// <summary>Adds a directional light shining along (dx, dy, dz) (the direction light travels FROM the light, same convention as Processing), like directionalLight(r, g, b, dx, dy, dz). Directional lights aren't attenuated by distance.</summary>
        public void AddDirectionalLight(float r, float g, float b, float dx, float dy, float dz) =>
            AddLightClamped(new Light3D(LightTypeDirectional, Rgb01(r, g, b), Vector3.Zero, new Vector3(dx, dy, dz), _currentLightFalloff, 0f, 0f, _currentLightSpecular, hasPosition: false));

        /// <summary>Adds a point light at (x, y, z), radiating equally in all directions and attenuated by the current LightFalloff(), like pointLight(r, g, b, x, y, z).</summary>
        public void AddPointLight(float r, float g, float b, float x, float y, float z) =>
            AddLightClamped(new Light3D(LightTypePoint, Rgb01(r, g, b), new Vector3(x, y, z), Vector3.Zero, _currentLightFalloff, 0f, 0f, _currentLightSpecular, hasPosition: true));

        /// <summary>Adds a spotlight at (x, y, z) aimed along (dx, dy, dz), like spotLight(r, g, b, x, y, z, dx, dy, dz, angle, concentration). angle (radians) is the half-angle of the cone; concentration controls how sharply intensity falls off toward the cone's edge (higher = tighter hotspot). Also attenuated by the current LightFalloff() with distance.</summary>
        public void AddSpotLight(float r, float g, float b, float x, float y, float z, float dx, float dy, float dz, float angleRadians, float concentration) =>
            AddLightClamped(new Light3D(LightTypeSpot, Rgb01(r, g, b), new Vector3(x, y, z), new Vector3(dx, dy, dz), _currentLightFalloff, angleRadians, concentration, _currentLightSpecular, hasPosition: true));

        private void AddLightClamped(Light3D light)
        {
            // Processing silently ignores lights past its own 8-light cap
            // rather than throwing -- matched here for the same reason:
            // a sketch that's slightly over budget shouldn't crash, it
            // should just stop getting brighter.
            if (_lights.Count >= MaxLights)
                return;
            _lights.Add(light);
        }

        private static Vector3 Rgb01(float r, float g, float b) => new(r / 255f, g / 255f, b / 255f);

        // Uploads the entire active light list plus the material uniforms.
        // Called once per DrawBox()/DrawSphere(), same "re-upload
        // everything every call" simplicity as uModel/uView/uProjection/
        // uFillColor already had before lights existed. Takes fillColor
        // so it can resolve the ambient default (_materialAmbient == null
        // means "track fill()", per Processing's own behavior) without
        // DrawBox()/DrawSphere() needing to know about that resolution.
        private void UploadLighting(SKColor fillColor)
        {
            _gl.Uniform3(_uEyePosLoc, _eyePosition.X, _eyePosition.Y, _eyePosition.Z);

            var ambient = _materialAmbient ?? Rgb01(fillColor.Red, fillColor.Green, fillColor.Blue);
            _gl.Uniform3(_uMaterialAmbientLoc, ambient.X, ambient.Y, ambient.Z);
            _gl.Uniform3(_uMaterialSpecularLoc, _materialSpecular.X, _materialSpecular.Y, _materialSpecular.Z);
            _gl.Uniform3(_uMaterialEmissiveLoc, _materialEmissive.X, _materialEmissive.Y, _materialEmissive.Z);
            _gl.Uniform1(_uMaterialShininessLoc, _materialShininess);

            _gl.Uniform1(_uLightCountLoc, _lights.Count);
            for (int i = 0; i < _lights.Count; i++)
            {
                var light = _lights[i];
                _gl.Uniform1(_uLightTypeLoc[i], light.Type);
                _gl.Uniform3(_uLightColorLoc[i], light.Color.X, light.Color.Y, light.Color.Z);
                _gl.Uniform3(_uLightPositionLoc[i], light.Position.X, light.Position.Y, light.Position.Z);
                _gl.Uniform3(_uLightDirectionLoc[i], light.Direction.X, light.Direction.Y, light.Direction.Z);
                _gl.Uniform3(_uLightFalloffLoc[i], light.Falloff.X, light.Falloff.Y, light.Falloff.Z);
                _gl.Uniform2(_uLightSpotLoc[i], light.SpotAngle, light.SpotConcentration);
                _gl.Uniform3(_uLightSpecularLoc[i], light.SpecularColor.X, light.SpecularColor.Y, light.SpecularColor.Z);
                _gl.Uniform1(_uLightHasPositionLoc[i], light.HasPosition ? 1 : 0);
            }
        }

        // =====================================================================
        // Per-frame lifecycle (IGraphicsBackend)
        // =====================================================================

        public void BeginFrame()
        {
            // CRÍTICO: un contexto GL solo es válido en el hilo donde está
            // "current". Create() lo deja current en el hilo que llamó a
            // Setup() (el hilo de UI de Avalonia) -- pero Draw() se llama
            // cada frame desde PaintSketch(), que Avalonia dispara desde su
            // hilo de render/composición, que NO es el mismo hilo. Sin este
            // MakeCurrent() cada llamada a _gl.* de acá en adelante toca
            // punteros de función inválidos para ESE hilo -> AccessViolation.
            _window.GLContext?.MakeCurrent();

            _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _fbo);
            _gl.Viewport(0, 0, (uint)_width, (uint)_height);

            // Fondo fijo gris oscuro -- no hay Background()/clear-color configurable
            // todavía para el pipeline 3D (Background() en Sketch/PGraphics dibuja
            // sobre el Canvas de Skia, que EndFrame() pisa por completo con
            // BlendMode.Src al volcar el FBO -- así que llamar Background() antes
            // de dibujar geometría 3D no tiene efecto visible; queda como mejora
            // futura conectarlo a glClearColor).
            _gl.ClearColor(0.08f, 0.08f, 0.1f, 1f);
            _gl.Clear((uint)(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit));

            _model = Matrix4x4.Identity;
            _modelStack.Clear();

            // Matches Processing's documented requirement: lights() (and
            // pointLight()/etc.) must be called every draw() to stay
            // active, which only makes sense if the light list resets
            // each frame -- so it does, right here.
            _lights.Clear();
        }

        public void EndFrame()
        {
            // Mismo motivo que en BeginFrame() -- barato de más y evita un
            // crash si algún día algo corre EndFrame() sin pasar por
            // BeginFrame() en el mismo hilo justo antes.
            _window.GLContext?.MakeCurrent();

            _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _fbo);
            _gl.Finish();

            // Read the FBO straight into a scratch buffer, then blit it into
            // the raster Surface as an SKBitmap. SKSurface doesn't expose its
            // backing memory directly, so a copy is unavoidable here --
            // revisit if per-frame readback shows up as a bottleneck once
            // real sketches are running.
            int stride = _width * 4;
            var pixels = new byte[stride * _height];
            unsafe
            {
                fixed (byte* p = pixels)
                    _gl.ReadPixels(0, 0, (uint)_width, (uint)_height, PixelFormat.Rgba, PixelType.UnsignedByte, p);
            }

            using var bitmap = new SKBitmap(new SKImageInfo(_width, _height, SKColorType.Rgba8888, SKAlphaType.Premul));
            System.Runtime.InteropServices.Marshal.Copy(pixels, 0, bitmap.GetPixels(), pixels.Length);

            // OpenGL's row 0 is the bottom of the image; Skia's row 0 is the
            // top -- flip vertically so Get()/Save() come out right-side up.
            using var flipped = new SKBitmap(bitmap.Info);
            using (var flipCanvas = new SKCanvas(flipped))
            {
                flipCanvas.Scale(1, -1);
                flipCanvas.Translate(0, -_height);
                flipCanvas.DrawBitmap(bitmap, 0, 0);
            }

            Canvas.DrawBitmap(flipped, 0, 0, new SKPaint { BlendMode = SKBlendMode.Src });
            Canvas.Flush();
        }

        // =====================================================================
        // 3D primitives -- called from GraphicsContext.3D.cs, gated there on
        // Renderer == RendererKind.Renderer3D so 2D contexts never reach here.
        // =====================================================================

        public void PushMatrix() => _modelStack.Push(_model);

        public void PopMatrix()
        {
            if (_modelStack.Count > 0)
                _model = _modelStack.Pop();
        }

        // Row-vector convention (System.Numerics): prepending the new
        // operation makes it apply first, so calls compose the way
        // Processing sketches expect -- Translate() then RotateX() rotates
        // around the already-translated origin, not the world origin.
        public void Translate(float x, float y, float z) => _model = Matrix4x4.CreateTranslation(x, y, z) * _model;
        public void RotateX(float radians) => _model = Matrix4x4.CreateRotationX(radians) * _model;
        public void RotateY(float radians) => _model = Matrix4x4.CreateRotationY(radians) * _model;
        public void RotateZ(float radians) => _model = Matrix4x4.CreateRotationZ(radians) * _model;
        public void Scale(float x, float y, float z) => _model = Matrix4x4.CreateScale(x, y, z) * _model;

        /// <summary>Draws a box centered on the current origin, sized w x h x d, filled with fillColor and shaded by the active lights (or drawn flat if none are active). Respects the current model transform (Translate/RotateX/Y/Z/Scale/PushMatrix/PopMatrix).</summary>
        public void DrawBox(float w, float h, float d, SKColor fillColor)
        {
            var scaledModel = Matrix4x4.CreateScale(w, h, d) * _model;

            _gl.UseProgram(_shaderProgram);
            UploadMatrix(_uModelLoc, scaledModel);
            UploadMatrix(_uViewLoc, _view);
            UploadMatrix(_uProjectionLoc, _projection);
            _gl.Uniform4(_uFillColorLoc, fillColor.Red / 255f, fillColor.Green / 255f, fillColor.Blue / 255f, fillColor.Alpha / 255f);
            UploadLighting(fillColor);

            _gl.BindVertexArray(_cubeVao);
            _gl.DrawArrays(GLEnum.Triangles, 0, 36);
            _gl.BindVertexArray(0);
        }

        /// <summary>Sets the sphere mesh resolution used by DrawSphere(), like Processing's sphereDetail(ures, vres). Applied lazily: no GL work happens here, the mesh is only (re)built the next time DrawSphere() actually runs -- so calling this repeatedly, or never calling Sphere() at all, never touches the GPU.</summary>
        public void SetSphereDetail(int ures, int vres)
        {
            ures = Math.Max(SphereVertexData.MinDetail, ures);
            vres = Math.Max(SphereVertexData.MinDetail, vres);
            if (ures == _sphereUres && vres == _sphereVres && _sphereMeshBuilt)
                return; // no-op: same resolution as what's already uploaded

            _sphereUres = ures;
            _sphereVres = vres;
            _sphereMeshBuilt = false; // EnsureSphereMesh() rebuilds on the next DrawSphere()
        }

        /// <summary>Draws a sphere of the given radius centered on the current origin, filled with fillColor and shaded by the active lights (or drawn flat if none are active) -- like Processing's sphere(radius). Mesh resolution follows the last SetSphereDetail() call (default 30x30). Respects the current model transform (Translate/RotateX/Y/Z/Scale/PushMatrix/PopMatrix), same as DrawBox().</summary>
        public void DrawSphere(float radius, SKColor fillColor)
        {
            EnsureSphereMesh();

            var scaledModel = Matrix4x4.CreateScale(radius) * _model;

            _gl.UseProgram(_shaderProgram);
            UploadMatrix(_uModelLoc, scaledModel);
            UploadMatrix(_uViewLoc, _view);
            UploadMatrix(_uProjectionLoc, _projection);
            _gl.Uniform4(_uFillColorLoc, fillColor.Red / 255f, fillColor.Green / 255f, fillColor.Blue / 255f, fillColor.Alpha / 255f);
            UploadLighting(fillColor);

            _gl.BindVertexArray(_sphereVao);
            _gl.DrawArrays(GLEnum.Triangles, 0, (uint)_sphereVertexCount);
            _gl.BindVertexArray(0);
        }

        private unsafe void UploadMatrix(int location, Matrix4x4 m)
        {
            // transpose: FALSE — see the corrected coordinate-convention
            // note at the top of this file. System.Numerics.Matrix4x4 is
            // stored row-major in memory; handing that raw data to GL while
            // claiming it's column-major (transpose: false) makes GL read
            // rows as columns, which is exactly a transpose — precisely the
            // row-vector -> column-vector conversion this needs. Passing
            // true here cancels that free transpose and silently breaks the
            // perspective divide (this was the actual bug behind the
            // "everything collapses into a diagonal line" symptom).
            _gl.UniformMatrix4(location, 1, false, (float*)&m);
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            // Same reasoning as BeginFrame() — Dispose() can be called from
            // yet another thread (e.g. the UI thread swapping in a new
            // sketch via LoadSketch()), and deleting GL objects needs the
            // context current on whichever thread does it.
            _window.GLContext?.MakeCurrent();
            _gl.DeleteVertexArray(_cubeVao);
            _gl.DeleteBuffer(_cubeVbo);
            if (_sphereVao != 0)
            {
                _gl.DeleteVertexArray(_sphereVao);
                _gl.DeleteBuffer(_sphereVbo);
            }
            _gl.DeleteProgram(_shaderProgram);
            _gl.DeleteFramebuffer(_fbo);
            _gl.DeleteTexture(_colorTexture);
            _gl.DeleteRenderbuffer(_depthRbo);
            Surface.Dispose();
            _window.Dispose();
            _disposed = true;
        }

        // =====================================================================
        // DONE: 3D Primitives (Box/Sphere), Camera/Projection, Lights,
        // Material Properties (ambient/specular/emissive/shininess).
        //
        // NOT YET BUILT (tracked here so it doesn't get lost):
        // - beginCamera()/endCamera() -- advanced camera customization where
        //   Translate/Rotate calls between the two apply to the view matrix
        //   instead of the model matrix. Needs its own routing flag through
        //   Translate()/RotateX()/Y()/Z()/Scale() above.
        // - normal() -- sets a custom per-vertex normal inside beginShape()/
        //   vertex(); no-op until 3D custom shapes (vertex-based, not just
        //   Box()/Sphere()) exist to attach normals to.
        // - Any 3D primitive besides Box() and Sphere() (Processing itself
        //   only has these two built-in 3D primitives, so this list is done
        //   as far as primitives go).
        // - PShader / custom shaders.
        // - True 2D-in-3D compositing (2D primitives as flat geometry
        //   inside the same MVP pipeline) -- for now 2D draws on top of
        //   whatever 3D rendered, as a separate compositing pass.
        // =====================================================================
    }
}