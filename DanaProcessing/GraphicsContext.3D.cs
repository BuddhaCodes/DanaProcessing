using System;

namespace DanaProcessing
{
    public abstract partial class GraphicsContext
    {
        // =====================================================================
        // 3D — https://processing.org/reference/box_.html and siblings.
        // Everything here requires Renderer3D (see Sketch.Size()/
        // CreateGraphics()) and throws otherwise, the same way Processing
        // itself errors when box()/rotateZ()/etc. are called under the
        // default 2D renderer. 3D Primitives, Camera/Projection, Lights,
        // and Material Properties are done — see the "NOT YET BUILT" list
        // at the bottom of Renderer3DBackend.cs for beginCamera()/
        // endCamera(), normal(), and PShader.
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
    }
}