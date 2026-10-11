using DanaProcessing.Audio;
using Xunit;

namespace DanaProcessing.Tests.Audio;

/// <summary>
/// The OS audio backends. CI machines usually have no sound card, so these
/// accept either outcome -- the device opens and pulls audio, or opening
/// fails with a clean InvalidOperationException (which the engine turns into
/// the clock-only fallback) -- but never a crash or a hang.
/// </summary>
public class PlatformOutputTests
{
    private static void PlaysOrFailsCleanly(IAudioOutput output)
    {
        int blocks = 0;
        try
        {
            output.Start((buf, frames) =>
            {
                for (int i = 0; i < frames * 2; i++) buf[i] = 0.1f * MathF.Sin(i * 0.05f);
                Interlocked.Increment(ref blocks);
            });
        }
        catch (InvalidOperationException)
        {
            output.Dispose();
            return; // no device here: fine, the engine falls back
        }
        catch (DllNotFoundException)
        {
            output.Dispose();
            return; // e.g. a minimal Linux without libasound
        }
        Thread.Sleep(400);
        output.Dispose();
        Assert.True(Volatile.Read(ref blocks) > 0, $"{output.Description} opened but never asked for audio");
        Assert.True(output.SampleRate > 0);
    }

    [Fact]
    public void Linux_ALSA()
    {
        if (OperatingSystem.IsLinux()) PlaysOrFailsCleanly(new AlsaOutput());
    }

    [Fact]
    public void MacOS_CoreAudio()
    {
        if (OperatingSystem.IsMacOS()) PlaysOrFailsCleanly(new CoreAudioOutput());
    }

    [Fact]
    public void Windows_WinMM()
    {
        if (OperatingSystem.IsWindows()) PlaysOrFailsCleanly(new WinMmOutput());
    }

    [Fact]
    public void Engine_never_throws_when_starting_audio()
    {
        // Whatever this machine has, EnsureStarted() must end up with some
        // output -- the real one or the clock-only fallback.
        var engine = new AudioEngine(null);
        try
        {
            engine.EnsureStarted();
            Assert.True(engine.IsRunning);
            Assert.False(string.IsNullOrEmpty(engine.OutputDescription));
        }
        finally { engine.Dispose(); }
    }
}
