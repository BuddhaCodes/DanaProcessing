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
