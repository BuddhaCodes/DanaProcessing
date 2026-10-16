using DanaProcessing.Ide;
using Xunit;

namespace DanaProcessing.Tests.Ide;

/// <summary>The Run button is a switch: ▶ Run when stopped, ■ Stop · m:ss while a sketch is live.</summary>
public class RunToggleTests
{
    [Fact]
    public void Stopped_it_offers_Run_and_live_it_offers_Stop_with_the_time()
    {
        Assert.Contains("▶", RunToggle.Label(false, TimeSpan.FromSeconds(30)));
        Assert.DoesNotContain(":", RunToggle.Label(false, TimeSpan.FromSeconds(30)));

        var live = RunToggle.Label(true, TimeSpan.FromSeconds(102));
        Assert.Contains("■", live);
        Assert.EndsWith("1:42", live);
    }

    [Fact]
    public void The_tooltip_says_what_a_click_does()
    {
        Assert.Contains("Shift+F5", RunToggle.Tooltip(true));
        Assert.Contains("Ctrl+R", RunToggle.Tooltip(false));
    }

    [Theory]
    [InlineData(0, "0:00")]
    [InlineData(7, "0:07")]
    [InlineData(102, "1:42")]
    [InlineData(725, "12:05")]
    [InlineData(3723, "1:02:03")]
    [InlineData(-5, "0:00")]
    public void Elapsed_time_reads_like_a_clock(int seconds, string expected)
    {
        Assert.Equal(expected, RunToggle.FormatElapsed(TimeSpan.FromSeconds(seconds)));
    }
}
