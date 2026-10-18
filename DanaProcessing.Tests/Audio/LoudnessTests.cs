using DanaProcessing.Audio;
using DanaProcessing.Reels;
using Xunit;

namespace DanaProcessing.Tests.Audio;

/// <summary>
/// Loudness (ITU-R BS.1770, what YouTube and ffmpeg's ebur128 report) and the
/// normalization reel exports apply, so a video comes out at -14 LUFS
/// whatever level the sketch played at.
/// </summary>
public class LoudnessTests
{
    private const int Rate = 48000;

    /// <summary>Stereo sine, same in both channels.</summary>
    private static float[] Sine(double hz, float amplitude, double seconds, int rate = Rate)
    {
        int n = (int)(seconds * rate);
        var s = new float[n * 2];
        for (int i = 0; i < n; i++)
            s[2 * i] = s[2 * i + 1] = amplitude * (float)Math.Sin(2 * Math.PI * hz * i / rate);
        return s;
    }

    [Theory]
    [InlineData(0.1f, -20.0)]     // a 1 kHz sine in both channels reads its peak level in dB
    [InlineData(0.5f, -6.02)]
    [InlineData(0.01f, -40.0)]
    public void A_1kHz_tone_measures_like_the_standard_says(float amplitude, double expected)
    {
        Assert.InRange(Loudness.IntegratedLufs(Sine(997, amplitude, 3), Rate), expected - 0.15, expected + 0.15);
    }

    [Fact]
    public void Low_frequencies_count_less_and_silence_is_gated_out()
    {
        double tone = Loudness.IntegratedLufs(Sine(997, 0.1f, 3), Rate);
        double bass = Loudness.IntegratedLufs(Sine(30, 0.1f, 3), Rate);
        Assert.True(bass < tone - 1, $"30 Hz {bass:0.0} should read quieter than 1 kHz {tone:0.0} (K-weighting)");

        // Ten seconds of silence after the tone don't make it "quieter" (only the
        // blocks straddling the end of the tone count, as the standard says).
        var withSilence = Sine(997, 0.1f, 3).Concat(new float[Rate * 2 * 10]).ToArray();
        double padded = Loudness.IntegratedLufs(withSilence, Rate);
        Assert.True(Math.Abs(padded - tone) < 0.3, $"tone {tone:0.00}, with silence {padded:0.00}");

        Assert.Equal(double.NegativeInfinity, Loudness.IntegratedLufs(new float[Rate * 2 * 2], Rate));
    }

    [Fact]
    public void Normalizing_reaches_the_target_and_keeps_peaks_under_the_ceiling()
    {
        // Quiet and peaky, like a soft pad with a few sharp hits.
        var s = Sine(220, 0.03f, 6);
        for (int hit = 0; hit < 6; hit++)
            for (int i = 0; i < 400; i++)
            {
                int k = (hit * Rate + 1000 + i) * 2;
                float v = 0.25f * (float)Math.Exp(-i / 80.0) * (i % 2 == 0 ? 1 : -1);
                s[k] += v;
                s[k + 1] += v;
            }

        var (measured, gain) = Loudness.Normalize(s, Rate, targetLufs: -14, ceilingDb: -1.5);
        Assert.True(measured < -25, $"started quiet ({measured:0.0} LUFS)");
        Assert.True(gain > 10);
        double after = Loudness.IntegratedLufs(s, Rate);
        Assert.True(after > -14.5 && after < -13.5, $"after {after:0.00} (measured {measured:0.00}, gain {gain:0.0})");
        Assert.True(Loudness.SamplePeak(s) <= Math.Pow(10, -1.5 / 20) + 1e-4, $"peak {Loudness.SamplePeak(s)}");
    }

    [Fact]
    public void Silence_is_left_alone()
    {
        var silence = new float[Rate * 2];
        var (measured, gain) = Loudness.Normalize(silence, Rate);
        Assert.Equal(double.NegativeInfinity, measured);
        Assert.Equal(0, gain);
        Assert.All(silence, v => Assert.Equal(0f, v));
    }

    [Theory]
    [InlineData("", -14.0)]
    [InlineData("// @reel loudness: -16", -16.0)]
    [InlineData("// @reel loudness: -16 LUFS", -16.0)]
    [InlineData("// @reel lufs: -23", -23.0)]
    [InlineData("// @reel loudness: off", null)]
    public void The_marker_sets_or_turns_off_the_target(string marker, double? expected)
    {
        var script = ReelScript.Parse(marker);
        Assert.Empty(script.Warnings);
        Assert.Equal(expected, script.Loudness);
    }

    public class Quiet : Sketch
    {
        public override void Setup() => Size(100, 100);
        public override void Draw() => Background(0);
        [LiveLoop] async Task Pad() { Play(57, amp: 0.05, sustain: 0.8, release: 0.1); await Sleep(1); }
    }

    private static float[] ReadWav16(string path)
    {
        var bytes = File.ReadAllBytes(path);
        int data = 44;
        var s = new float[(bytes.Length - data) / 2];
        for (int i = 0; i < s.Length; i++)
            s[i] = BitConverter.ToInt16(bytes, data + 2 * i) / 32767f;
        return s;
    }

    [Theory]
    [InlineData("", -14.0)]
    [InlineData("// @reel loudness: off\n", double.NaN)]
    public void An_export_comes_out_at_minus_14_unless_turned_off(string marker, double expected)
    {
        var dir = Path.Combine(Path.GetTempPath(), $"dana_loud_{Guid.NewGuid():N}");
        try
        {
            var script = ReelScript.Parse(marker + "// @reel format: square\n// @reel result: 3\n// @reel outro: off\n// @reel fps: 10\n[LiveLoop] async Task Pad() { }");
            Assert.True(script.RecordsAudio);
            ReelExporter.ExportAsync(script, () => new Quiet(), new ReelExportOptions { Kind = ReelExportKind.Png, OutputPath = dir })
                        .GetAwaiter().GetResult();
            var wav = ReadWav16(Path.Combine(dir, "audio.wav"));
            double lufs = Loudness.IntegratedLufs(wav, Rate);
            if (double.IsNaN(expected))
                Assert.True(lufs < -25, $"left at the sketch's own level, got {lufs:0.0}");
            else
                Assert.InRange(lufs, expected - 0.7, expected + 0.7);   // 16-bit WAV + short clip: a little slack
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }
}
