using System;

namespace DanaProcessing.Audio
{
    /// <summary>
    /// Everything needed to start one sound, passed by value from whichever
    /// thread called Play()/Sample() to the audio thread through a lock-free
    /// queue. A struct on purpose: dequeuing it allocates nothing.
    /// </summary>
    internal struct VoiceEvent
    {
        public long StartFrame;
        public Synth Synth;
        public SampleBuffer? Sample;
        public double Frequency;
        public double Amp;
        public double Pan;
        public double Attack, Decay, Sustain, Release, SustainLevel;
        public double Cutoff;      // Hz; <= 0 means "no filter"
        public double Resonance;   // 0..1
        public double Rate;        // sample playback speed
    }

    /// <summary>
    /// One playing sound. A fixed pool of these is allocated once when the
    /// engine starts and reused forever -- Render() runs on the audio thread
    /// and must never allocate (a GC pause there is an audible click).
    /// </summary>
    internal sealed class Voice
    {
        private const int PluckBufferSize = 4096;

        public bool Active;
        public long StartFrame;

        private VoiceEvent _e;
        private double _sr, _invSr;
        private double _phase;
        private double _total;          // envelope length, seconds
        private double _gainL, _gainR;
        private uint _rng = 0x9E3779B9;

        // TPT state-variable low-pass (Zavalishin)
        private bool _filter;
        private double _a1, _a2, _a3, _ic1, _ic2;

        // Karplus-Strong
        private readonly float[] _pluck = new float[PluckBufferSize];
        private int _pluckLen, _pluckIdx;

        // Drums
        private double _hpPrev, _hpOut;

        // Sample playback
        private double _samplePos, _sampleStep;

        public void Start(in VoiceEvent e, double sampleRate)
        {
            _e = e;
            _sr = sampleRate;
            _invSr = 1.0 / sampleRate;
            StartFrame = e.StartFrame;
            _phase = 0;
            _ic1 = _ic2 = 0;
            _hpPrev = _hpOut = 0;
            _pluckLen = 0;
            _rng ^= (uint)(e.StartFrame * 2654435761L) | 1u;

            double angle = (Math.Clamp(e.Pan, -1, 1) + 1) * Math.PI / 4;
            _gainL = Math.Cos(angle) * e.Amp;
            _gainR = Math.Sin(angle) * e.Amp;

            if (e.Sample != null)
            {
                _samplePos = 0;
                _sampleStep = Math.Max(0.01, e.Rate) * e.Sample.SampleRate / sampleRate;
                _total = double.MaxValue;
                _filter = false;
            }
            else
            {
                _total = e.Synth switch
                {
                    Synth.Kick => 0.6,
                    Synth.Snare => 0.35,
                    Synth.Hat => 0.15,
                    _ => Math.Max(0.001, e.Attack) + e.Decay + e.Sustain + Math.Max(0.005, e.Release),
                };

                _filter = e.Cutoff > 0 && e.Synth is not (Synth.Kick or Synth.Snare or Synth.Hat);
                if (_filter)
                {
                    double fc = Math.Min(e.Cutoff, sampleRate * 0.45);
                    double g = Math.Tan(Math.PI * fc / sampleRate);
                    double k = 2.0 - 1.95 * Math.Clamp(e.Resonance, 0, 1);
                    _a1 = 1.0 / (1.0 + g * (g + k));
                    _a2 = g * _a1;
                    _a3 = g * _a2;
                }
            }

            Active = true;
        }

        /// <summary>Mixes this voice into an interleaved stereo block starting at engine frame <paramref name="blockStart"/>.</summary>
        public void Render(float[] buffer, int frames, long blockStart)
        {
            long rel0 = blockStart - StartFrame;
            int i0 = rel0 < 0 ? (int)Math.Min(-rel0, frames) : 0;
            if (i0 >= frames)
                return; // scheduled for a later block

            if (_e.Sample != null)
            {
                RenderSample(buffer, frames, i0);
                return;
            }

            for (int i = i0; i < frames; i++)
            {
                long n = rel0 + i;
                double t = n * _invSr;
                if (t >= _total)
                {
                    Active = false;
                    return;
                }

                double s = _e.Synth switch
                {
                    Synth.Kick => Kick(t),
                    Synth.Snare => Snare(t),
                    Synth.Hat => Hat(t),
                    _ => Oscillator(n) * Envelope(t),
                };

                if (_filter)
                {
                    double v3 = s - _ic2;
                    double v1 = _a1 * _ic1 + _a2 * v3;
                    double v2 = _ic2 + _a2 * _ic1 + _a3 * v3;
                    _ic1 = 2 * v1 - _ic1;
                    _ic2 = 2 * v2 - _ic2;
                    s = v2;
                }

                buffer[2 * i] += (float)(s * _gainL);
                buffer[2 * i + 1] += (float)(s * _gainR);
            }
        }

        private void RenderSample(float[] buffer, int frames, int i0)
        {
            var smp = _e.Sample!;
            int last = smp.Frames - 1;
            double fadeFrames = 0.003 * smp.SampleRate; // 3 ms fade-out avoids a click at the end

            for (int i = i0; i < frames; i++)
            {
                if (_samplePos >= last)
                {
                    Active = false;
                    return;
                }

                int p = (int)_samplePos;
                double frac = _samplePos - p;
                double l = smp.Left[p] + (smp.Left[p + 1] - smp.Left[p]) * frac;
                double r = smp.Right[p] + (smp.Right[p + 1] - smp.Right[p]) * frac;

                double remaining = last - _samplePos;
                double g = remaining < fadeFrames ? remaining / fadeFrames : 1.0;

                buffer[2 * i] += (float)(l * g * _gainL * 1.41421356);
                buffer[2 * i + 1] += (float)(r * g * _gainR * 1.41421356);
                _samplePos += _sampleStep;
            }
        }

        // Sonic Pi style ADSR: 0 -> 1 over attack, 1 -> sustain_level over
        // decay, hold for sustain, then -> 0 over release. Linear segments.
        private double Envelope(double t)
        {
            double a = Math.Max(0.001, _e.Attack);
            if (t < a)
                return t / a;
            t -= a;

            double sl = _e.SustainLevel;
            if (t < _e.Decay)
                return 1.0 + (sl - 1.0) * (t / _e.Decay);
            t -= _e.Decay;

            if (t < _e.Sustain)
                return sl;
            t -= _e.Sustain;

            double r = Math.Max(0.005, _e.Release);
            return t < r ? sl * (1.0 - t / r) : 0.0;
        }

        private double Oscillator(long n)
        {
            double dt = _e.Frequency * _invSr;
            double s;
            switch (_e.Synth)
            {
                case Synth.Saw:
                    s = 2.0 * _phase - 1.0 - PolyBlep(_phase, dt);
                    break;
                case Synth.Square:
                    s = (_phase < 0.5 ? 1.0 : -1.0) + PolyBlep(_phase, dt) - PolyBlep((_phase + 0.5) % 1.0, dt);
                    break;
                case Synth.Triangle:
                    s = 4.0 * Math.Abs(_phase - 0.5) - 1.0;
                    break;
                case Synth.Noise:
                    s = NextNoise();
                    break;
                case Synth.Pluck:
                    return Pluck(n);
                default:
                    s = Math.Sin(2.0 * Math.PI * _phase);
                    break;
            }

            _phase += dt;
            if (_phase >= 1.0)
                _phase -= Math.Floor(_phase);
            return s;
        }

        private double Pluck(long n)
        {
            if (_pluckLen == 0)
            {
                _pluckLen = (int)Math.Clamp(_sr / Math.Max(1.0, _e.Frequency), 2, PluckBufferSize);
                for (int i = 0; i < _pluckLen; i++)
                    _pluck[i] = (float)NextNoise();
                _pluckIdx = 0;
            }

            int next = _pluckIdx + 1 == _pluckLen ? 0 : _pluckIdx + 1;
            double outSample = _pluck[_pluckIdx];
            _pluck[_pluckIdx] = (float)(0.5 * (_pluck[_pluckIdx] + _pluck[next]) * 0.996);
            _pluckIdx = next;
            return outSample;
        }

        private double Kick(double t)
        {
            // Sine with a fast downward pitch sweep (150 -> 45 Hz) and an
            // exponential body decay, plus a tiny click for attack.
            double freq = 45.0 + 105.0 * Math.Exp(-t * 35.0);
            _phase += freq * _invSr;
            if (_phase >= 1.0)
                _phase -= 1.0;
            double body = Math.Sin(2.0 * Math.PI * _phase) * Math.Exp(-t * 7.0);
            double click = t < 0.004 ? NextNoise() * 0.3 * (1.0 - t / 0.004) : 0.0;
            return (body + click) * Math.Min(1.0, t / 0.0015);
        }

        private double Snare(double t)
        {
            _phase += 185.0 * _invSr;
            if (_phase >= 1.0)
                _phase -= 1.0;
            double tone = Math.Sin(2.0 * Math.PI * _phase) * Math.Exp(-t * 30.0) * 0.5;
            double noise = HighPass(NextNoise()) * Math.Exp(-t * 18.0) * 0.8;
            return (tone + noise) * Math.Min(1.0, t / 0.001);
        }

        private double Hat(double t)
        {
            double noise = HighPass(HighPass2(NextNoise()));
            return noise * Math.Exp(-t * 60.0) * 0.6 * Math.Min(1.0, t / 0.0005);
        }

        // One-pole high-pass, ~1.5 kHz at 48 kHz.
        private double HighPass(double x)
        {
            _hpOut = 0.82 * (_hpOut + x - _hpPrev);
            _hpPrev = x;
            return _hpOut;
        }

        // Plain first difference: extra top-end tilt for the hat.
        private double _diffPrev;
        private double HighPass2(double x)
        {
            double y = x - _diffPrev;
            _diffPrev = x;
            return y * 0.5;
        }

        private double NextNoise()
        {
            // xorshift32 -- deterministic per voice, no allocation, no locks
            uint x = _rng;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            _rng = x;
            return (x / (double)uint.MaxValue) * 2.0 - 1.0;
        }

        private static double PolyBlep(double t, double dt)
        {
            if (t < dt)
            {
                t /= dt;
                return t + t - t * t - 1.0;
            }
            if (t > 1.0 - dt)
            {
                t = (t - 1.0) / dt;
                return t * t + t + t + 1.0;
            }
            return 0.0;
        }
    }
}
