using SkiaSharp;

namespace DanaProcessing
{
    public abstract partial class Sketch
    {
        // =====================================================================
        // 2D shaders (SkSL), Shadertoy-style. Shade() fills a rectangle with a
        // fragment shader and fills in these uniforms for you, whenever the
        // shader declares them:
        //   uniform float  iTime;        seconds since the sketch started
        //   uniform float2 iResolution;  Width, Height
        //   uniform float4 iMouse;       MouseX, MouseY, 1 while pressed, 0
        //                                (declare it float2 to get just x, y)
        //   uniform float  iFrame;       FrameCount
        //   uniform float  iAmplitude;   Amplitude: how loud the sketch's sound is
        //   uniform float  iSpectrum[N]; Spectrum(): N = 8, 16, 32 or 64 bands
        // fragCoord is in sketch pixels, y pointing DOWN like everything else
        // here (Shadertoy's points up).
        // =====================================================================

        private static readonly int[] ScalarOrVector = { 1, 2, 3, 4 };
        private static readonly int[] SpectrumSizes = { 8, 16, 32, 64 };

        // Shade(string): compiled effects by source text, so editing the SkSL
        // and pressing Hot Reload (or just saving) swaps the shader while
        // iTime keeps running. Survives hot reload with the rest of the base
        // fields; capped so a long session doesn't pile up old versions.
        private Dictionary<string, PShader2D>? _shaderCache;
        private Queue<string>? _shaderCacheOrder;
        private PShader2D? _lastGoodShader;
        private string? _shaderError;
        private const int ShaderCacheSize = 8;

        /// <summary>Compiles SkSL into a reusable shader. Throws with the compiler's own message if it doesn't compile.</summary>
        public PShader2D CreateShader2D(string sksl)
        {
            var effect = SKRuntimeEffect.CreateShader(sksl, out string errors);
            if (effect == null)
                throw new InvalidOperationException("El shader no compila:\n" + errors);
            return new PShader2D(effect);
        }

        /// <summary>Like CreateShader2D, reading the SkSL from a file.</summary>
        public PShader2D LoadShader2D(string path) => CreateShader2D(File.ReadAllText(ResolveInputPath(path)));

        /// <summary>Fills the whole canvas with <paramref name="shader"/>.</summary>
        public void Shade(PShader2D shader) => Shade(shader, 0, 0, Width, Height);

        /// <summary>Fills a rectangle with <paramref name="shader"/> (current transform applies, like Rect()).</summary>
        public void Shade(PShader2D shader, float x, float y, float w, float h)
        {
            EnsureReady();
            FillAutoUniforms(shader);
            using var skShader = shader.Effect.ToShader(shader.Uniforms);
            using var paint = new SKPaint { Shader = skShader };
            Canvas.DrawRect(SKRect.Create(x, y, w, h), paint);
        }

        /// <summary>
        /// Live-coding version: fills the canvas with this SkSL source, compiled
        /// once and cached by its text. Edit the string and Hot Reload (or save)
        /// to see the new shader without restarting. If the new code doesn't
        /// compile, the last shader that did keeps running and the error shows
        /// in a corner instead of stopping the sketch.
        /// </summary>
        public void Shade(string sksl) => Shade(sksl, 0, 0, Width, Height);

        /// <summary>Live-coding Shade(sksl), limited to a rectangle.</summary>
        public void Shade(string sksl, float x, float y, float w, float h)
        {
            EnsureReady();
            var shader = GetCachedShader(sksl);
            if (shader != null)
                Shade(shader, x, y, w, h);
            if (_shaderError != null)
                DrawShaderError(_shaderError);
        }

        private PShader2D? GetCachedShader(string sksl)
        {
            _shaderCache ??= new Dictionary<string, PShader2D>();
            _shaderCacheOrder ??= new Queue<string>();

            if (_shaderCache.TryGetValue(sksl, out var cached))
            {
                _lastGoodShader = cached;
                _shaderError = null;
                return cached;
            }

            var effect = SKRuntimeEffect.CreateShader(sksl, out string errors);
            if (effect == null)
            {
                _shaderError = string.IsNullOrWhiteSpace(errors) ? "El shader no compila." : errors.Trim();
                return _lastGoodShader; // keep showing the last one that worked
            }

            var shader = new PShader2D(effect);
            _shaderCache[sksl] = shader;
            _shaderCacheOrder.Enqueue(sksl);
            while (_shaderCacheOrder.Count > ShaderCacheSize)
            {
                var oldest = _shaderCacheOrder.Dequeue();
                if (_shaderCache.Remove(oldest, out var evicted) && !ReferenceEquals(evicted, _lastGoodShader))
                    evicted.Dispose();
            }
            _lastGoodShader = shader;
            _shaderError = null;
            return shader;
        }

        private void FillAutoUniforms(PShader2D shader)
        {
            SetAuto(shader, "iTime", Millis() / 1000f);
            SetAuto(shader, "iResolution", Width, Height);
            SetAuto(shader, "iMouse", MouseX, MouseY, IsMousePressed ? 1f : 0f, 0f);
            SetAuto(shader, "iFrame", FrameCount);
            SetAuto(shader, "iAmplitude", Amplitude);

            int bands = shader.AutoLength("iSpectrum", SpectrumSizes);
            if (bands > 0)
            {
                var values = new float[bands];
                Spectrum(values);
                shader.Uniforms["iSpectrum"] = values;
            }
        }

        private static void SetAuto(PShader2D shader, string name, params float[] full)
        {
            int length = shader.AutoLength(name, ScalarOrVector);
            if (length == 0)
                return;
            if (length == 1)
            {
                shader.Uniforms[name] = full.Length > 0 ? full[0] : 0f;
                return;
            }
            var values = new float[length];
            Array.Copy(full, values, Math.Min(length, full.Length));
            shader.Uniforms[name] = values;
        }

        // Raw Skia, so the sketch's own Fill/TextSize state is untouched.
        private void DrawShaderError(string error)
        {
            string firstLine = error.Split('\n')[0];
            if (firstLine.Length > 110)
                firstLine = firstLine.Substring(0, 110) + "...";

            using var bg = new SKPaint { Color = new SKColor(60, 10, 10, 220) };
            using var text = new TextStyle(new SKColor(255, 190, 190), 12);
            float width = text.MeasureText(firstLine) + 16;
            Canvas.Save();
            Canvas.DrawRect(SKRect.Create(8, Height - 30, width, 22), bg);
            Canvas.DrawText(firstLine, 16, Height - 14, text);
            Canvas.Restore();
        }
    }
}
