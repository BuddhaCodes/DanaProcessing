namespace DanaProcessing
{
    /// <summary>
    /// Marks a field of your sketch as tweakable: it gets a slider (or a
    /// checkbox, for a bool) in a panel drawn over the running sketch, and
    /// dragging it changes the field live -- no recompile, no restart. Works on
    /// <c>float</c>, <c>double</c>, <c>int</c>, <c>bool</c> and <c>Color</c>
    /// (one slider per R/G/B channel).
    /// <code>
    /// [Tweak(0, 20)] float speed = 4;
    /// [Tweak] int count = 12;          // no range: 0 .. 2x the starting value
    /// [Tweak] bool trails = true;
    /// [Tweak(Label = "Background")] Color bg = new Color(20, 20, 30);
    /// </code>
    /// The value lives in your own field, so ⚡ Hot Reload keeps whatever you
    /// tuned. Click the panel's header to fold it; HideTweaks() hides it.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
    public sealed class TweakAttribute : Attribute
    {
        /// <summary>No explicit range: the slider goes from 0 to twice the field's starting value (or -2x..2x for a negative one).</summary>
        public TweakAttribute() { }

        /// <summary>Slider from <paramref name="min"/> to <paramref name="max"/>.</summary>
        public TweakAttribute(float min, float max)
        {
            Min = min;
            Max = max;
            HasRange = true;
        }

        public float Min { get; }
        public float Max { get; }
        public bool HasRange { get; }

        /// <summary>Text shown next to the slider. Defaults to the field's name.</summary>
        public string? Label { get; set; }
    }
}
