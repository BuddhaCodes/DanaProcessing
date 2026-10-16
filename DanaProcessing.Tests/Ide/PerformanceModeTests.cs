using DanaProcessing.Ide;
using Xunit;

namespace DanaProcessing.Tests.Ide;

/// <summary>Performance mode's decisions: which screen, and the text size / backdrop steps.</summary>
public class PerformanceModeTests
{
    [Fact]
    public void A_second_screen_wins_over_the_main_one()
    {
        Assert.Equal(1, PerformanceMode.PickScreen(new[] { true, false }));     // laptop + projector
        Assert.Equal(0, PerformanceMode.PickScreen(new[] { false, true }));     // projector listed first
        Assert.Equal(0, PerformanceMode.PickScreen(new[] { true }));            // just the laptop
        Assert.Equal(0, PerformanceMode.PickScreen(new[] { false }));           // one screen, not flagged primary
        Assert.Equal(-1, PerformanceMode.PickScreen(Array.Empty<bool>()));
    }

    [Fact]
    public void Text_size_steps_by_two_and_stays_readable()
    {
        Assert.Equal(26, PerformanceMode.StepFontSize(24, +1));
        Assert.Equal(22, PerformanceMode.StepFontSize(24, -1));
        Assert.Equal(PerformanceMode.MaxFontSize, PerformanceMode.StepFontSize(PerformanceMode.MaxFontSize, +1));
        Assert.Equal(PerformanceMode.MinFontSize, PerformanceMode.StepFontSize(PerformanceMode.MinFontSize, -1));
    }

    [Fact]
    public void The_backdrop_never_hides_the_visuals_completely()
    {
        Assert.Equal(0.55, PerformanceMode.StepOpacity(0.45, +1), 3);
        Assert.Equal(PerformanceMode.MaxOpacity, PerformanceMode.StepOpacity(0.9, +1), 3);
        Assert.Equal(0, PerformanceMode.StepOpacity(0.05, -1), 3);
    }
}
