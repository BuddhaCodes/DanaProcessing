using System.Globalization;
using System.Reflection;

namespace DanaProcessing
{
    /// <summary>
    /// One [Tweak] field's current value, as C# source -- what
    /// <see cref="Sketch.GetTweakValues"/> returns. <see cref="Value"/> is the raw
    /// boxed value (float, double, int, bool or Color); <see cref="CSharpLiteral"/>
    /// is that value written so it can be pasted straight into a field
    /// initializer (<c>2.5f</c>, <c>12</c>, <c>true</c>, <c>new Color(255, 170, 90)</c>).
    /// <see cref="Touched"/> is true once its slider/checkbox has actually been
    /// moved in this session -- untouched fields keep whatever the code says,
    /// even an expression like <c>MathF.PI / 2</c>.
    /// </summary>
    public sealed record TweakValue(string FieldName, object Value, string CSharpLiteral, bool Touched);

    public abstract partial class Sketch
    {
        // =====================================================================
        // Tweak mode -- [Tweak] fields get live sliders drawn over the sketch.
        // See TweakAttribute for the sketch-facing side and TweakPanel for how
        // the panel draws, takes the mouse and survives hot reload.
        // =====================================================================

        // Lazy instead of a field initializer: sketches created without a
        // constructor run (RuntimeHelpers.GetUninitializedObject, as the hot
        // reload tests do) must not hit a null panel.
        private TweakPanel? _tweakPanel;
        private TweakPanel Tweaks => _tweakPanel ??= new TweakPanel();

        /// <summary>Shows the [Tweak] panel again after HideTweaks(). It's shown by default whenever the sketch has at least one [Tweak] field.</summary>
        public void ShowTweaks() => Tweaks.Visible = true;

        /// <summary>Hides the [Tweak] panel -- e.g. before SaveFrame() or while recording. The fields keep their current values.</summary>
        public void HideTweaks() => Tweaks.Visible = false;

        /// <summary>True while the [Tweak] panel is on screen (visible and the sketch has [Tweak] fields).</summary>
        public bool IsTweakPanelVisible => Tweaks.IsActive(this);

        /// <summary>
        /// The current value of every [Tweak] field, written as C# -- what the
        /// IDE's "Apply tweaks to code" uses to put what you tuned with the
        /// sliders back into the field initializers. Fields of a type [Tweak]
        /// doesn't support are left out.
        /// </summary>
        public IReadOnlyList<TweakValue> GetTweakValues()
        {
            var values = new List<TweakValue>();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            foreach (var field in GetType().GetFields(flags))
            {
                if (field.GetCustomAttribute<TweakAttribute>() == null)
                    continue;
                object? value = field.GetValue(this);
                string? literal = value switch
                {
                    float f => FloatLiteral(f) + "f",
                    double d => FloatLiteral(d),
                    int i => i.ToString(CultureInfo.InvariantCulture),
                    bool b => b ? "true" : "false",
                    Color c => c.A == 255
                        ? $"new Color({c.R}, {c.G}, {c.B})"
                        : $"new Color({c.R}, {c.G}, {c.B}, {c.A})",
                    _ => null,
                };
                if (value != null && literal != null)
                    values.Add(new TweakValue(field.Name, value, literal, Tweaks.WasTouched(field.Name)));
            }
            return values;
        }

        /// <summary>
        /// Sets [Tweak] fields from C# literals ("2.5f", "12", "true",
        /// "new Color(255, 170, 90)") -- what a PNG saved by a sketch stores (see
        /// SketchImageInfo) -- and marks them as touched, so "Apply tweaks to
        /// code" writes them into the source. Unknown fields and literals that
        /// don't parse are skipped; returns how many were applied.
        /// </summary>
        internal int ApplyTweakLiterals(IReadOnlyDictionary<string, string> literals)
        {
            int applied = 0;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            foreach (var field in GetType().GetFields(flags))
            {
                if (field.GetCustomAttribute<TweakAttribute>() == null || !literals.TryGetValue(field.Name, out var literal))
                    continue;
                var value = ParseTweakLiteral(field.FieldType, literal);
                if (value == null)
                    continue;
                field.SetValue(this, value);
                Tweaks.MarkTouched(field.Name);
                applied++;
            }
            return applied;
        }

        /// <summary>
        /// Gives every [Tweak] field a random value inside its slider's range
        /// (the attribute's, or 0..2x its current value like the panel does) --
        /// what the variations sheet uses to explore. Returns the new values as
        /// C# literals, ready for <see cref="ApplyTweakLiterals"/>.
        /// </summary>
        internal IReadOnlyDictionary<string, string> RandomizeTweaks(Random rng)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            foreach (var field in GetType().GetFields(flags))
            {
                var attr = field.GetCustomAttribute<TweakAttribute>();
                if (attr == null)
                    continue;
                object? current = field.GetValue(this);
                float Pick(float value)
                {
                    float min = attr.HasRange ? attr.Min : (value < 0 ? 2 * value : 0);
                    float max = attr.HasRange ? attr.Max : (value < 0 ? -2 * value : Math.Max(1f, 2 * value));
                    return min + (float)rng.NextDouble() * (max - min);
                }
                switch (current)
                {
                    case float f: field.SetValue(this, Pick(f)); break;
                    case double d: field.SetValue(this, (double)Pick((float)d)); break;
                    case int i:
                    {
                        float min = attr.HasRange ? attr.Min : (i < 0 ? 2 * i : 0);
                        float max = attr.HasRange ? attr.Max : (i < 0 ? -2 * i : Math.Max(10, 2 * i));
                        field.SetValue(this, (int)Math.Round(min + rng.NextDouble() * (max - min)));
                        break;
                    }
                    case bool: field.SetValue(this, rng.Next(2) == 0); break;
                    case DanaProcessing.Color:
                    {
                        int lo = attr.HasRange ? (int)attr.Min : 0, hi = attr.HasRange ? (int)attr.Max : 255;
                        byte Channel() => (byte)Math.Clamp(rng.Next(lo, hi + 1), 0, 255);
                        field.SetValue(this, new Color(Channel(), Channel(), Channel()));
                        break;
                    }
                }
            }
            return GetTweakValues().ToDictionary(v => v.FieldName, v => v.CSharpLiteral);
        }

        internal static object? ParseTweakLiteral(Type type, string literal)
        {
            string text = literal.Trim();
            var inv = CultureInfo.InvariantCulture;
            if (type == typeof(float))
                return float.TryParse(text.TrimEnd('f', 'F'), NumberStyles.Float, inv, out var f) ? f : null;
            if (type == typeof(double))
                return double.TryParse(text.TrimEnd('d', 'D'), NumberStyles.Float, inv, out var d) ? d : null;
            if (type == typeof(int))
                return int.TryParse(text, NumberStyles.Integer, inv, out var i) ? i : null;
            if (type == typeof(bool))
                return bool.TryParse(text, out var b) ? b : null;
            if (type == typeof(Color))
            {
                int open = text.IndexOf('('), close = text.LastIndexOf(')');
                if (open < 0 || close <= open)
                    return null;
                var parts = text[(open + 1)..close].Split(',');
                var channels = new List<byte>();
                foreach (var part in parts)
                {
                    if (!int.TryParse(part.Trim(), NumberStyles.Integer, inv, out var c))
                        return null;
                    channels.Add((byte)Math.Clamp(c, 0, 255));
                }
                return channels.Count switch
                {
                    3 => new Color(channels[0], channels[1], channels[2]),
                    4 => new Color(channels[0], channels[1], channels[2], channels[3]),
                    _ => null,
                };
            }
            return null;
        }

        /// <summary>
        /// Short but faithful enough for a slider: 4 significant digits
        /// (0.0025, 2.346, 512.3), plain integers from 1000 up -- never the
        /// 2.34567891 noise a dragged slider really holds.
        /// </summary>
        private static string FloatLiteral(double v)
        {
            if (double.IsNaN(v) || double.IsInfinity(v))
                v = 0;
            string s = Math.Abs(v) >= 1000
                ? Math.Round(v).ToString("0", CultureInfo.InvariantCulture)
                : v.ToString("G4", CultureInfo.InvariantCulture);
            return s == "-0" ? "0" : s;
        }
    }
}
