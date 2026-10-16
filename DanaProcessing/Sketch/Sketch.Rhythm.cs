using DanaProcessing.Audio;

namespace DanaProcessing
{
    public abstract partial class Sketch
    {
        // =====================================================================
        // Musical time for visuals -- Beat(), Bar(), Phase(), Pulse(), OnBeat()
        // and the easings in Easing. While sound is playing they read the audio
        // engine's beat clock at what you HEAR (AudibleNow), and live loops that
        // start from silence put their first downbeat on a bar line, so an
        // animation written with these stays locked to the music. With no sound
        // they follow Millis() at the current Bpm instead, so they work in any
        // sketch (and in reels, which drive Millis() in video time).
        //
        // The clock is read once per frame, right before Draw(): every call in
        // the same Draw() sees the same instant.
        // =====================================================================

        private double _frameBeat;
        private double _previousFrameBeat;
        private bool _beatSampled;

        // No-sound clock: beats advance with Millis() at the current Bpm,
        // re-anchored when the tempo changes so the count never jumps.
        private double _fallbackAnchorMillis;
        private double _fallbackAnchorBeats;
        private double _fallbackBpm;

        /// <summary>Reads the beat clock for this frame (RenderFrame calls it before Draw()).</summary>
        internal void SampleBeatClock()
        {
            double now = CurrentBeats();
            _previousFrameBeat = _beatSampled ? _frameBeat : now;
            _frameBeat = now;
            _beatSampled = true;
        }

        private double CurrentBeats()
        {
            // A reel drives the sketch in video time (VirtualMillis): follow the
            // engine only if it's the reel's own offline one, never the music
            // that happens to be playing in the IDE meanwhile.
            var engine = AudioEngine.SharedIfCreated;
            if (engine != null && engine.IsRunning && (VirtualMillis is null || engine.Offline != null))
                return engine.BeatsAt(engine.AudibleNow);

            double bpm = engine?.Bpm ?? 60;
            // _clock is null for a sketch made without its constructor (tests).
            double millis = VirtualMillis ?? (_clock is null ? 0 : _clock.Elapsed.TotalMilliseconds);
            if (bpm != _fallbackBpm)
            {
                if (_fallbackBpm > 0)
                    _fallbackAnchorBeats += (millis - _fallbackAnchorMillis) / 60000.0 * _fallbackBpm;
                _fallbackAnchorMillis = millis;
                _fallbackBpm = bpm;
            }
            return _fallbackAnchorBeats + (millis - _fallbackAnchorMillis) / 60000.0 * bpm;
        }

        private double FrameBeat
        {
            get
            {
                if (!_beatSampled)
                    SampleBeatClock(); // called from Setup(), before the first frame
                return _frameBeat;
            }
        }

        /// <summary>
        /// Where the music is, in beats: 0, then 1 one beat later, 2.5 halfway
        /// through the third... Continuous, so it's ready for animation:
        /// <c>Rotate((float)Beat() * QUARTER_PI)</c> turns 45° per beat.
        /// </summary>
        public double Beat() => FrameBeat;

        /// <summary>Where the music is, in bars of <paramref name="beatsPerBar"/> beats (continuous, like Beat()).</summary>
        public double Bar(int beatsPerBar = 4) => FrameBeat / Math.Max(1, beatsPerBar);

        /// <summary>
        /// A ramp from 0 to 1 that restarts every <paramref name="beats"/> beats:
        /// 0 exactly on the beat, 0.5 halfway to the next one. Phase(4) ramps
        /// once per bar, Phase(0.5) twice per beat.
        /// </summary>
        public float Phase(double beats = 1)
        {
            if (beats <= 0)
                return 0f;
            double p = FrameBeat / beats;
            return (float)(p - Math.Floor(p));
        }

        /// <summary>
        /// 1 on the beat, decaying towards 0 before the next one -- a flash, a
        /// kick of the scale. <paramref name="sharpness"/> sets how fast it dies
        /// out (higher = snappier).
        /// </summary>
        public float Pulse(double beats = 1, float sharpness = 6f) => MathF.Exp(-Math.Max(0f, sharpness) * Phase(beats));

        /// <summary>0 → 1 → 0 over <paramref name="beats"/> beats: a smooth back-and-forth in time.</summary>
        public float PingPong(double beats = 2)
        {
            float p = Phase(beats);
            return p < 0.5f ? p * 2f : 2f - p * 2f;
        }

        /// <summary>
        /// True on the one frame where a new beat (or every <paramref name="beats"/>
        /// beats: OnBeat(4) = each bar, OnBeat(0.5) = each eighth) starts.
        /// </summary>
        public bool OnBeat(double beats = 1)
        {
            if (beats <= 0 || !_beatSampled)
                return false;
            return Math.Floor(_frameBeat / beats + 1e-9) > Math.Floor(_previousFrameBeat / beats + 1e-9);
        }

        /// <summary>
        /// Phase(<paramref name="beats"/>) shaped by an easing curve: a move that
        /// starts on the beat and lands exactly on the next one.
        /// <c>Lerp(100, 400, BeatEase(Ease.OutBack))</c>.
        /// </summary>
        public float BeatEase(Ease ease, double beats = 1) => Easing.Apply(ease, Phase(beats));

        /// <summary>
        /// Goes from <paramref name="from"/> to <paramref name="to"/> once every
        /// <paramref name="beats"/> beats along <paramref name="ease"/> -- the
        /// whole tween in one call, already in time with the music.
        /// </summary>
        public float BeatTween(float from, float to, Ease ease = Ease.InOutCubic, double beats = 1) =>
            from + (to - from) * Easing.Apply(ease, Phase(beats));
    }

    /// <summary>Shapes for <see cref="Easing.Apply"/>, <see cref="Sketch.BeatEase"/> and <see cref="Sketch.BeatTween"/>.</summary>
    public enum Ease
    {
        Linear,
        InSine, OutSine, InOutSine,
        InQuad, OutQuad, InOutQuad,
        InCubic, OutCubic, InOutCubic,
        InExpo, OutExpo, InOutExpo,
        InBack, OutBack, InOutBack,
        InElastic, OutElastic,
        OutBounce,
    }

    /// <summary>
    /// The classic easing curves (Robert Penner's): each takes t from 0 to 1 and
    /// returns 0 at t = 0 and 1 at t = 1 -- Back and Elastic overshoot in
    /// between on purpose. Use them on Phase() for moves locked to the beat, or
    /// on any 0..1 progress of your own.
    /// </summary>
    public static class Easing
    {
        private const float C1 = 1.70158f;
        private const float C2 = C1 * 1.525f;
        private const float C3 = C1 + 1f;
        private const float C4 = 2f * MathF.PI / 3f;

        public static float Apply(Ease ease, float t)
        {
            t = Math.Clamp(t, 0f, 1f);
            return ease switch
            {
                Ease.Linear => t,
                Ease.InSine => InSine(t),
                Ease.OutSine => OutSine(t),
                Ease.InOutSine => InOutSine(t),
                Ease.InQuad => InQuad(t),
                Ease.OutQuad => OutQuad(t),
                Ease.InOutQuad => InOutQuad(t),
                Ease.InCubic => InCubic(t),
                Ease.OutCubic => OutCubic(t),
                Ease.InOutCubic => InOutCubic(t),
                Ease.InExpo => InExpo(t),
                Ease.OutExpo => OutExpo(t),
                Ease.InOutExpo => InOutExpo(t),
                Ease.InBack => InBack(t),
                Ease.OutBack => OutBack(t),
                Ease.InOutBack => InOutBack(t),
                Ease.InElastic => InElastic(t),
                Ease.OutElastic => OutElastic(t),
                Ease.OutBounce => OutBounce(t),
                _ => t,
            };
        }

        public static float InSine(float t) => 1f - MathF.Cos(t * MathF.PI / 2f);
        public static float OutSine(float t) => MathF.Sin(t * MathF.PI / 2f);
        public static float InOutSine(float t) => -(MathF.Cos(MathF.PI * t) - 1f) / 2f;

        public static float InQuad(float t) => t * t;
        public static float OutQuad(float t) => 1f - (1f - t) * (1f - t);
        public static float InOutQuad(float t) => t < 0.5f ? 2f * t * t : 1f - MathF.Pow(-2f * t + 2f, 2f) / 2f;

        public static float InCubic(float t) => t * t * t;
        public static float OutCubic(float t) => 1f - MathF.Pow(1f - t, 3f);
        public static float InOutCubic(float t) => t < 0.5f ? 4f * t * t * t : 1f - MathF.Pow(-2f * t + 2f, 3f) / 2f;

        public static float InExpo(float t) => t <= 0f ? 0f : MathF.Pow(2f, 10f * t - 10f);
        public static float OutExpo(float t) => t >= 1f ? 1f : 1f - MathF.Pow(2f, -10f * t);
        public static float InOutExpo(float t) =>
            t <= 0f ? 0f : t >= 1f ? 1f
            : t < 0.5f ? MathF.Pow(2f, 20f * t - 10f) / 2f
            : (2f - MathF.Pow(2f, -20f * t + 10f)) / 2f;

        public static float InBack(float t) => C3 * t * t * t - C1 * t * t;
        public static float OutBack(float t) => 1f + C3 * MathF.Pow(t - 1f, 3f) + C1 * MathF.Pow(t - 1f, 2f);
        public static float InOutBack(float t) =>
            t < 0.5f
                ? MathF.Pow(2f * t, 2f) * ((C2 + 1f) * 2f * t - C2) / 2f
                : (MathF.Pow(2f * t - 2f, 2f) * ((C2 + 1f) * (t * 2f - 2f) + C2) + 2f) / 2f;

        public static float InElastic(float t) =>
            t <= 0f ? 0f : t >= 1f ? 1f : -MathF.Pow(2f, 10f * t - 10f) * MathF.Sin((t * 10f - 10.75f) * C4);
        public static float OutElastic(float t) =>
            t <= 0f ? 0f : t >= 1f ? 1f : MathF.Pow(2f, -10f * t) * MathF.Sin((t * 10f - 0.75f) * C4) + 1f;

        public static float OutBounce(float t)
        {
            const float n1 = 7.5625f, d1 = 2.75f;
            if (t < 1f / d1)
                return n1 * t * t;
            if (t < 2f / d1)
                return n1 * (t -= 1.5f / d1) * t + 0.75f;
            if (t < 2.5f / d1)
                return n1 * (t -= 2.25f / d1) * t + 0.9375f;
            return n1 * (t -= 2.625f / d1) * t + 0.984375f;
        }
    }
}
