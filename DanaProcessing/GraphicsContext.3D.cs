using System;
using System.IO;

namespace DanaProcessing
{
    public abstract partial class GraphicsContext
    {
        // =====================================================================
        // 3D — https://processing.org/reference/box_.html and siblings.
        // Everything here requires Renderer3D (see Sketch.Size()/
        // CreateGraphics()) and throws otherwise, the same way Processing
        // itself errors when box()/rotateZ()/etc. are called under the
        // default 2D renderer. 3D Primitives, Camera/Projection (including
        // BeginCamera()/EndCamera()), Lights, Material Properties,
        // Coordinates (ModelX/Y/Z()/ScreenX/Y/Z()), the axis-angle
        // Rotate(angle,x,y,z), and PShader are done — see the "NOT YET
        // BUILT" list at the bottom of Renderer3DBackend.cs for what's
        // left (just normal(), and only partially at that).
        // =====================================================================

        private Renderer3DBackend Require3D([System.Runtime.CompilerServices.CallerMemberName] string caller = "")
        {
            if (Renderer != RendererKind.Renderer3D)
                throw new InvalidOperationException($"{caller}() requiere Renderer3D — pasá RendererKind.Renderer3D a Size()/CreateGraphics().");
            return (Renderer3DBackend)_backend!;
        }

        /// <summary>Draws a cube of size×size×size centered on the current origin, like Processing's box(size).</summary>
        public void Box(float size) => Box(size, size, size);

        /// <summary>Draws a box w×h×d centered on the current origin, filled with the current Fill() color and shaded by the active lights (flat, unlit if none are active — see Lights()), like Processing's box(w, h, d).</summary>
        public void Box(float w, float h, float d)
        {
            EnsureReady();
            Require3D().DrawBox(w, h, d, _fillPaint.Color);
        }

        /// <summary>Sets the sphere mesh resolution for subsequent Sphere() calls, using res segments both around the equator and pole-to-pole, like Processing's sphereDetail(res). Default is 30; values below 3 are clamped up to 3, matching Processing.</summary>
        public void SphereDetail(int res) => SphereDetail(res, res);

        /// <summary>Sets the sphere mesh resolution independently for the equator (ures) and pole-to-pole (vres) directions, like Processing's sphereDetail(ures, vres). Takes effect for Sphere() calls made after this one — it doesn't retroactively change spheres already drawn this frame.</summary>
        public void SphereDetail(int ures, int vres)
        {
            EnsureReady();
            Require3D().SetSphereDetail(ures, vres);
        }

        /// <summary>Draws a sphere of the given radius centered on the current origin, filled with the current Fill() color and shaded by the active lights (flat, unlit if none are active — see Lights()), like Processing's sphere(radius). Mesh resolution follows the last SphereDetail() call (default 30×30).</summary>
        public void Sphere(float radius)
        {
            EnsureReady();
            Require3D().DrawSphere(radius, _fillPaint.Color);
        }

        /// <summary>3D translate, like Processing's translate(x, y, z). The 2D Translate(x, y) overload in the Transformations section still works under Renderer3D too (z is implicitly 0), matching Processing.</summary>
        public void Translate(float x, float y, float z)
        {
            EnsureReady();
            Require3D().Translate(x, y, z);
        }

        /// <summary>Rotates around the x-axis by angleRadians, like Processing's rotateX(). Note this takes radians — unlike the 2D Rotate(degrees), which deliberately takes degrees (see its own remark).</summary>
        public void RotateX(float angleRadians)
        {
            EnsureReady();
            Require3D().RotateX(angleRadians);
        }

        /// <summary>Rotates around the y-axis by angleRadians, like Processing's rotateY().</summary>
        public void RotateY(float angleRadians)
        {
            EnsureReady();
            Require3D().RotateY(angleRadians);
        }

        /// <summary>Rotates around the z-axis by angleRadians, like Processing's rotateZ(). Note this is a genuinely different operation from the 2D Rotate(degrees) — under Renderer3D, both exist and compose (Rotate() rotates the Skia 2D overlay, RotateZ() rotates 3D geometry).</summary>
        public void RotateZ(float angleRadians)
        {
            EnsureReady();
            Require3D().RotateZ(angleRadians);
        }

        /// <summary>3D scale, like Processing's scale(x, y, z).</summary>
        public void Scale(float x, float y, float z)
        {
            EnsureReady();
            Require3D().Scale(x, y, z);
        }

        /// <summary>Rotates by angleRadians around the arbitrary axis (x, y, z), like Processing's rotate(angle, x, y, z) — the P3D-only overload of rotate(). Takes radians, matching RotateX()/RotateY()/RotateZ() (not the 2D Rotate(degrees), which deliberately takes degrees — see its own remark). (x, y, z) doesn't need to be pre-normalized; a zero-length axis is a no-op, matching how Processing itself treats it. Useful for a single combined rotation (e.g. from a trackball/quaternion) instead of stacking RotateX()+RotateY()+RotateZ(), which composes differently depending on call order and can twist in unexpected ways once the object is no longer close to its rest orientation.</summary>
        public void Rotate(float angleRadians, float x, float y, float z)
        {
            EnsureReady();
            Require3D().RotateAxis(angleRadians, x, y, z);
        }

        // =====================================================================
        // Camera / projection — https://processing.org/reference/camera_.html
        // and siblings. Same Require3D() gate as everything else in this file.
        // =====================================================================

        /// <summary>Resets the camera to Processing's default view — eye pulled back from the canvas center, looking straight at it, +Y up — like Processing's camera() with no arguments.</summary>
        public void Camera()
        {
            EnsureReady();
            Require3D().SetDefaultView();
        }

        /// <summary>Positions the camera at (eyeX, eyeY, eyeZ), looking at (centerX, centerY, centerZ), with (upX, upY, upZ) as the up direction, like Processing's camera(eyeX, eyeY, eyeZ, centerX, centerY, centerZ, upX, upY, upZ).</summary>
        public void Camera(float eyeX, float eyeY, float eyeZ, float centerX, float centerY, float centerZ, float upX, float upY, float upZ)
        {
            EnsureReady();
            Require3D().SetView(eyeX, eyeY, eyeZ, centerX, centerY, centerZ, upX, upY, upZ);
        }

        /// <summary>Resets the projection to Processing's default perspective, like Processing's perspective() with no arguments.</summary>
        public void Perspective()
        {
            EnsureReady();
            Require3D().SetDefaultProjection();
        }

        /// <summary>Sets a perspective projection, like Processing's perspective(fovy, aspect, zNear, zFar). fovy is the vertical field of view in radians; aspect is width/height.</summary>
        public void Perspective(float fovy, float aspect, float zNear, float zFar)
        {
            EnsureReady();
            Require3D().SetPerspective(fovy, aspect, zNear, zFar);
        }

        /// <summary>Resets the projection to Processing's default orthographic volume (objects stay the same size regardless of distance from the camera), like Processing's ortho() with no arguments.</summary>
        public void Ortho()
        {
            EnsureReady();
            Require3D().SetDefaultOrtho();
        }

        /// <summary>Sets an orthographic projection over the given X/Y clipping volume, using Processing's default near/far planes, like Processing's ortho(left, right, bottom, top).</summary>
        public void Ortho(float left, float right, float bottom, float top)
        {
            EnsureReady();
            Require3D().SetOrtho(left, right, bottom, top);
        }

        /// <summary>Sets an orthographic projection, like Processing's ortho(left, right, bottom, top, near, far).</summary>
        public void Ortho(float left, float right, float bottom, float top, float near, float far)
        {
            EnsureReady();
            Require3D().SetOrtho(left, right, bottom, top, near, far);
        }

        /// <summary>Sets a perspective projection from explicit clipping-plane coordinates — unlike Perspective(), the planes don't need to be centered on the view axis — like Processing's frustum(left, right, bottom, top, near, far). near must be greater than zero, and far greater than near. Unlike camera()/perspective()/ortho(), Processing gives frustum() no no-argument form, so neither does this.</summary>
        public void Frustum(float left, float right, float bottom, float top, float near, float far)
        {
            EnsureReady();
            Require3D().SetFrustum(left, right, bottom, top, near, far);
        }

        /// <summary>Prints the current camera (view) matrix to the console, like Processing's printCamera().</summary>
        public void PrintCamera()
        {
            EnsureReady();
            Require3D().PrintCamera();
        }

        /// <summary>Prints the current projection matrix to the console, like Processing's printProjection().</summary>
        public void PrintProjection()
        {
            EnsureReady();
            Require3D().PrintProjection();
        }

        // =====================================================================
        // Lights — https://processing.org/reference/lights_.html and
        // siblings. Same Require3D() gate as everything else in this file.
        // The active light list resets every frame (BeginFrame()), so —
        // exactly like Processing — Lights()/PointLight()/etc. need to be
        // called again each Draw(), not just once in Setup().
        // =====================================================================

        /// <summary>Turns on Processing's default lighting rig — a mid-gray ambient light plus a mid-gray directional light shining straight along -Z, like lights() with no arguments. Without this (or one of the individual light calls below), Box()/Sphere() draw flat and fully unlit, which is Processing's own default.</summary>
        public void Lights()
        {
            EnsureReady();
            Require3D().SetDefaultLights();
        }

        /// <summary>Turns off all active lights, like noLights(). Box()/Sphere() go back to flat, unlit Fill() color.</summary>
        public void NoLights()
        {
            EnsureReady();
            Require3D().ClearLights();
        }

        /// <summary>Adds an ambient light with color (r, g, b) (each 0-255) that lights every surface uniformly regardless of orientation or position, like ambientLight(r, g, b).</summary>
        public void AmbientLight(float r, float g, float b)
        {
            EnsureReady();
            Require3D().AddAmbientLight(r, g, b);
        }

        /// <summary>Adds an ambient light with color (r, g, b) positioned at (x, y, z) — its contribution falls off with distance according to the current LightFalloff() — like ambientLight(r, g, b, x, y, z).</summary>
        public void AmbientLight(float r, float g, float b, float x, float y, float z)
        {
            EnsureReady();
            Require3D().AddAmbientLight(r, g, b, x, y, z);
        }

        /// <summary>Adds a directional light with color (r, g, b) shining along the direction (nx, ny, nz), like directionalLight(r, g, b, nx, ny, nz). Directional lights illuminate from a fixed direction with no position and no falloff — think sunlight.</summary>
        public void DirectionalLight(float r, float g, float b, float nx, float ny, float nz)
        {
            EnsureReady();
            Require3D().AddDirectionalLight(r, g, b, nx, ny, nz);
        }

        /// <summary>Adds a point light with color (r, g, b) at (x, y, z), radiating equally in all directions and attenuated by the current LightFalloff() with distance, like pointLight(r, g, b, x, y, z).</summary>
        public void PointLight(float r, float g, float b, float x, float y, float z)
        {
            EnsureReady();
            Require3D().AddPointLight(r, g, b, x, y, z);
        }

        /// <summary>Adds a spotlight with color (r, g, b) at (x, y, z), aimed along (nx, ny, nz), like spotLight(r, g, b, x, y, z, nx, ny, nz, angle, concentration). angle (in radians) is the half-angle of the light cone; concentration controls how sharply intensity falls off toward the cone's edge. Also attenuated by the current LightFalloff() with distance.</summary>
        public void SpotLight(float r, float g, float b, float x, float y, float z, float nx, float ny, float nz, float angle, float concentration)
        {
            EnsureReady();
            Require3D().AddSpotLight(r, g, b, x, y, z, nx, ny, nz, angle, concentration);
        }

        /// <summary>Sets the falloff (constant, linear, quadratic attenuation coefficients) applied to point/spot/positional-ambient lights added AFTER this call, like lightFalloff(constant, linear, quadratic). Processing's own default is (1, 0, 0) — no falloff at all.</summary>
        public void LightFalloff(float constant, float linear, float quadratic)
        {
            EnsureReady();
            Require3D().SetLightFalloff(constant, linear, quadratic);
        }

        /// <summary>Sets the specular color (r, g, b, each 0-255) used by lights added AFTER this call when computing specular highlights, like lightSpecular(r, g, b). Has no visible effect until Shininess() is set above 0.</summary>
        public void LightSpecular(float r, float g, float b)
        {
            EnsureReady();
            Require3D().SetLightSpecular(r, g, b);
        }

        // =====================================================================
        // Material Properties -- https://processing.org/reference/ambient_.html
        // and siblings. Same Require3D() gate as everything else in this
        // file. Persistent style state like Fill()/Stroke() -- set once,
        // applies to every shape drawn afterward, and (unlike the light
        // list) NOT reset per frame.
        // =====================================================================

        /// <summary>Sets the ambient reflectance color used by shapes drawn after this call, as a single gray value (r/g/b all set to the same 0-255 value), like Processing's ambient(gray).</summary>
        public void Ambient(float gray) => Ambient(gray, gray, gray);

        /// <summary>Sets the ambient reflectance color (r, g, b, each 0-255) used by shapes drawn after this call, like Processing's ambient(r, g, b). Ambient reflectance is how much of each light's ambient contribution a surface reflects; before this is ever called, it defaults to tracking the current Fill() color, matching Processing.</summary>
        public void Ambient(float r, float g, float b)
        {
            EnsureReady();
            Require3D().SetMaterialAmbient(r, g, b);
        }

        /// <summary>Sets the specular reflectance color used by shapes drawn after this call, as a single gray value, like Processing's specular(gray).</summary>
        public void Specular(float gray) => Specular(gray, gray, gray);

        /// <summary>Sets the specular reflectance color (r, g, b, each 0-255) used by shapes drawn after this call, like Processing's specular(r, g, b). Controls the color of specular highlights; has no visible effect until Shininess() is set above 0.</summary>
        public void Specular(float r, float g, float b)
        {
            EnsureReady();
            Require3D().SetMaterialSpecular(r, g, b);
        }

        /// <summary>Sets the emissive color used by shapes drawn after this call, as a single gray value, like Processing's emissive(gray).</summary>
        public void Emissive(float gray) => Emissive(gray, gray, gray);

        /// <summary>Sets the emissive color (r, g, b, each 0-255) used by shapes drawn after this call, like Processing's emissive(r, g, b). Emissive color is added on top of a surface's shading regardless of lighting — makes it look like it's glowing on its own, without it actually casting light onto other shapes.</summary>
        public void Emissive(float r, float g, float b)
        {
            EnsureReady();
            Require3D().SetMaterialEmissive(r, g, b);
        }

        /// <summary>Sets the shininess (specular exponent) used by shapes drawn after this call, like Processing's shininess(shine). 0 (the default) disables the specular highlight entirely; higher values give a smaller, glossier highlight. Needs Specular() and an active light to be visible.</summary>
        public void Shininess(float shine)
        {
            EnsureReady();
            Require3D().SetMaterialShininess(shine);
        }

        // =====================================================================
        // Camera (advanced) — https://processing.org/reference/beginCamera_.html
        // and endCamera(). Same Require3D() gate as everything else in this
        // file. Between these two calls, Translate()/RotateX()/RotateY()/
        // RotateZ()/Scale() place a "camera" the same way they'd place any
        // object — natural, intuitive composition — and EndCamera() inverts
        // the result into the actual view matrix once, at the end. That's
        // what real Processing's own reference means by "transformations
        // ... are equivalent to moving the camera around, but you must take
        // the inverse": a camera's OWN placement (local-to-world, like an
        // object) and the VIEW matrix used for rendering (world-to-eye) are
        // each other's inverse, not the same matrix. Start from a known
        // placement with Camera()/the 9-argument Camera() overload right
        // before BeginCamera() — same idiom as real Processing — since
        // BeginCamera() itself continues from whatever camera state was
        // already there rather than resetting it.
        // =====================================================================

        /// <summary>Starts placing the camera with Translate()/RotateX()/RotateY()/RotateZ()/Scale() — the same calls, composed the same way, you'd use to place any object — like Processing's beginCamera(). Must be paired with EndCamera(), which turns the result into the actual view matrix. Continues from the current camera (see this section's own remarks) rather than resetting — call Camera() first for a known starting point, same as real Processing.</summary>
        public void BeginCamera()
        {
            EnsureReady();
            Require3D().BeginCameraEdit();
        }

        /// <summary>Finishes placing the camera and turns that placement into the actual view matrix used for rendering (by inverting it), like Processing's endCamera().</summary>
        public void EndCamera()
        {
            EnsureReady();
            Require3D().EndCameraEdit();
        }

        // =====================================================================
        // Coordinates — https://processing.org/reference/modelX_.html and
        // siblings (modelY/modelZ/screenX/screenY/screenZ). Same Require3D()
        // gate as everything else in this file.
        // =====================================================================

        /// <summary>Returns the X coordinate of (x, y, z) after applying the current model and camera transforms, like Processing's modelX(x, y, z). Typical use: Translate()/RotateX()/etc. into position, call ModelX/Y/Z(0, 0, 0) to record that world position, then PopMatrix() and Translate() straight to it later.</summary>
        public float ModelX(float x, float y, float z)
        {
            EnsureReady();
            return Require3D().ModelPosition(x, y, z).X;
        }

        /// <summary>Returns the Y coordinate of (x, y, z) after applying the current model and camera transforms, like Processing's modelY(x, y, z).</summary>
        public float ModelY(float x, float y, float z)
        {
            EnsureReady();
            return Require3D().ModelPosition(x, y, z).Y;
        }

        /// <summary>Returns the Z coordinate of (x, y, z) after applying the current model and camera transforms, like Processing's modelZ(x, y, z).</summary>
        public float ModelZ(float x, float y, float z)
        {
            EnsureReady();
            return Require3D().ModelPosition(x, y, z).Z;
        }

        /// <summary>Returns the screen-space X pixel coordinate that (x, y, z) projects to, like Processing's screenX(x, y, z). Same pixel convention as MouseX (0 at the left edge).</summary>
        public float ScreenX(float x, float y, float z)
        {
            EnsureReady();
            return Require3D().ScreenPosition(x, y, z).X;
        }

        /// <summary>Returns the screen-space Y pixel coordinate that (x, y, z) projects to, like Processing's screenY(x, y, z). Same pixel convention as MouseY (0 at the top edge).</summary>
        public float ScreenY(float x, float y, float z)
        {
            EnsureReady();
            return Require3D().ScreenPosition(x, y, z).Y;
        }

        /// <summary>Returns the normalized device depth that (x, y, z) projects to, like Processing's screenZ(x, y, z). 0 is at the near clipping plane, 1 is at the far clipping plane — DanaProcessing's own convention (no PMatrix3D here to delegate to for Processing's exact formula), but useful the same way: smaller means closer to the camera.</summary>
        public float ScreenZ(float x, float y, float z)
        {
            EnsureReady();
            return Require3D().ScreenPosition(x, y, z).Z;
        }

        // =====================================================================
        // Normal — https://processing.org/reference/normal_.html. Same
        // Require3D() gate as everything else in this file.
        // =====================================================================
        /// <summary>Sets the current normal vector, like Processing's normal(nx, ny, nz). Affects vertices added by Vertex() AFTER this call, inside a BeginShape()/EndShape() block under Renderer3D — call it once per face (before that face's vertices) for flat shading, like the "Gema facetada" sample, or share one normal across several vertices for smooth shading.</summary>
        public void Normal(float nx, float ny, float nz)
        {
            Require3D().SetCurrentNormal(nx, ny, nz);
        }

        /// <summary>
        /// Builds a reusable 3D PShape once, like combining Processing's
        /// createShape() with its own beginShape()/endShape() (real
        /// Processing PShapes support being built that way too) —
        /// https://processing.org/reference/createShape_.html. Call the
        /// SAME Vertex()/Normal() calls you'd use with immediate-mode
        /// BeginShape()/EndShape() inside `buildVertices` — don't call
        /// BeginShape()/EndShape() yourself in there, CreateShape3D() wraps
        /// both. The mesh is uploaded to the GPU exactly ONCE (StaticDraw);
        /// draw it as many times as you want afterwards with
        /// Shape(shape, x, y) instead of re-recording and re-uploading the
        /// same geometry every frame the way a bare BeginShape()/EndShape()
        /// call under Renderer3D would.
        ///
        /// A diferencia de otras llamadas 3D, esto NO requiere estar entre
        /// BeginDraw()/EndDraw() -- reclama y libera el contexto de GL por
        /// su cuenta (ver UploadPersistentMesh()), así que se puede llamar
        /// tranquilamente una sola vez en Setup(), suelto, sin ningún
        /// bracket adicional.
        /// </summary>
        public PShape CreateShape3D(ShapeKind kind, Action buildVertices)
        {
            var backend = Require3D(); // valida Renderer3D antes de tocar nada -- sin EnsureReady(), ver remark arriba

            BeginShape(kind);
            buildVertices();

            if (!_shape3DActive)
                throw new InvalidOperationException("CreateShape3D(): buildVertices() no debe llamar EndShape() -- CreateShape3D() ya se encarga de eso.");

            var flat = TriangulateShape3D();
            _shape3DActive = false;

            if (flat.Length == 0)
                throw new InvalidOperationException("CreateShape3D(): no se agregó ningún vértice dentro de buildVertices().");

            var (vao, vbo, vertexCount, textureId) = backend.UploadPersistentMesh(flat, _shapeTexture);
            return PShape.FromMesh3D(backend, vao, vbo, vertexCount, textureId);
        }

        // =====================================================================
        // PShader — https://processing.org/reference/PShader.html and
        // shader()/resetShader()/loadShader(). Same Require3D() gate as
        // everything else in this file. See PShader's own remarks for the
        // scope of what a custom shader here can do.
        // =====================================================================

        /// <summary>Loads a GLSL fragment shader from disk, reusing DanaProcessing's built-in vertex shader, like Processing's loadShader(fragFilename). Doesn't touch the GPU until it's passed to Shader().</summary>
        public PShader LoadShader(string fragFilename) => new PShader(null, File.ReadAllText(fragFilename));

        /// <summary>Loads a GLSL vertex+fragment shader pair from disk, like Processing's loadShader(fragFilename, vertFilename) — note the parameter order matches Processing's own (fragment path first, then vertex) even though it reads backwards. The vertex shader must use DanaProcessing's fixed 3D attribute layout — see PShader's own remarks.</summary>
        public PShader LoadShader(string fragFilename, string vertFilename) => new PShader(File.ReadAllText(vertFilename), File.ReadAllText(fragFilename));

        /// <summary>Activates shader for subsequent Box()/Sphere() draws, like Processing's shader(shader). Stays active across frames until ResetShader() or another Shader() call.</summary>
        public void Shader(PShader shader)
        {
            EnsureReady();
            Require3D().SetActiveShader(shader);
        }

        /// <summary>Reverts to DanaProcessing's built-in 3D shader, like Processing's resetShader().</summary>
        public void ResetShader()
        {
            EnsureReady();
            Require3D().ResetShaderProgram();
        }
    }
}