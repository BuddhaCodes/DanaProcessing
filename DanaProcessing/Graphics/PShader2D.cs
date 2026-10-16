using SkiaSharp;

namespace DanaProcessing
{
    /// <summary>
    /// A fragment shader written in SkSL -- Skia's shading language, GLSL with
    /// a few renames (half4/float2 instead of vec4/vec2, main(float2 fragCoord)
    /// returning the color). Runs in the plain 2D renderer, on every OS, no
    /// OpenGL needed: get one with Sketch.CreateShader2D(sksl), or skip that and
    /// call Sketch.Shade(sksl) straight from Draw() for live editing.
    ///
    /// Not the same thing as PShader, which is a GLSL program for the 3D
    /// renderer's Box()/Sphere() meshes.
    /// </summary>
    public sealed class PShader2D : IDisposable
    {
        internal SKRuntimeEffect Effect { get; }
        internal SKRuntimeEffectUniforms Uniforms { get; }

        // Float count each automatic uniform (iTime, iMouse...) accepted the
        // first time it was tried -- 0 when the shader doesn't declare it or
        // declares it with a type we don't fill. Probed once, not every frame.
        private readonly Dictionary<string, int> _autoLengths = new();

        internal PShader2D(SKRuntimeEffect effect)
        {
            Effect = effect;
            Uniforms = new SKRuntimeEffectUniforms(effect);
        }

        /// <summary>Every uniform the shader declares, by name.</summary>
        public IReadOnlyList<string> UniformNames => Effect.Uniforms;

        /// <summary>True if the shader declares a uniform called <paramref name="name"/>.</summary>
        public bool Has(string name) => Uniforms.Contains(name);

        /// <summary>Sets a <c>float</c> uniform.</summary>
        public void Set(string name, float x) => Uniforms[name] = x;

        /// <summary>Sets a <c>float2</c> uniform.</summary>
        public void Set(string name, float x, float y) => Uniforms[name] = new[] { x, y };

        /// <summary>Sets a <c>float3</c> uniform.</summary>
        public void Set(string name, float x, float y, float z) => Uniforms[name] = new[] { x, y, z };

        /// <summary>Sets a <c>float4</c> uniform.</summary>
        public void Set(string name, float x, float y, float z, float w) => Uniforms[name] = new[] { x, y, z, w };

        /// <summary>Sets a float array (or float2/3/4, matrix) uniform; the length must match the declaration.</summary>
        public void Set(string name, float[] values) => Uniforms[name] = values;

        /// <summary>Sets a <c>half4</c>/<c>float4</c> (or <c>float3</c>) uniform from a Color, channels 0..1.</summary>
        public void Set(string name, Color color) =>
            Uniforms[name] = new[] { color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f };

        /// <summary>
        /// How many floats the uniform <paramref name="name"/> takes, trying
        /// <paramref name="candidates"/> in order (1 = a plain float) -- 0 if the
        /// shader doesn't declare it or none fits.
        /// </summary>
        internal int AutoLength(string name, int[] candidates)
        {
            if (_autoLengths.TryGetValue(name, out int known))
                return known;

            int accepted = 0;
            if (Uniforms.Contains(name))
            {
                foreach (int length in candidates)
                {
                    try
                    {
                        if (length == 1)
                            Uniforms[name] = 0f;
                        else
                            Uniforms[name] = new float[length];
                        accepted = length;
                        break;
                    }
                    catch (Exception)
                    {
                        // wrong type or size for this declaration: try the next one
                    }
                }
            }
            _autoLengths[name] = accepted;
            return accepted;
        }

        public void Dispose()
        {
            Uniforms.Dispose();
            Effect.Dispose();
        }
    }
}
