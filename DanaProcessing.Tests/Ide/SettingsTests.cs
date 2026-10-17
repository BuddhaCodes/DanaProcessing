using DanaProcessing.Ide;
using DanaProcessing.Ide.Localization;
using DanaProcessing.Ide.Theme;
using Xunit;

namespace DanaProcessing.Tests.Ide;

/// <summary>
/// The simpler Settings window's logic: performance mode's code transparency
/// (kept between shows, always in range), the one "Roundness" slider, and
/// when Save needs a restart.
/// </summary>
public class SettingsTests : IDisposable
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), $"dana-perf-{Guid.NewGuid():N}.json");

    public SettingsTests() => PerformanceSettingsStore.PathOverride = _file;

    public void Dispose()
    {
        PerformanceSettingsStore.PathOverride = null;
        if (File.Exists(_file))
            File.Delete(_file);
    }

    [Fact]
    public void Performance_settings_start_solid_and_survive_a_restart()
    {
        var first = PerformanceSettingsStore.Load();
        Assert.Equal(1.0, first.CodeOpacity);                       // no file yet: solid code
        Assert.Equal(PerformanceMode.DefaultOpacity, first.BackdropOpacity);

        first.CodeOpacity = 0.4;
        first.BackdropOpacity = 0.2;
        first.FontSize = 32;
        PerformanceSettingsStore.Save(first);

        var again = PerformanceSettingsStore.Load();
        Assert.Equal(0.4, again.CodeOpacity, 3);
        Assert.Equal(0.2, again.BackdropOpacity, 3);
        Assert.Equal(32, again.FontSize);
    }

    [Fact]
    public void Out_of_range_or_broken_values_never_reach_the_stage()
    {
        File.WriteAllText(_file, "{\"CodeOpacity\": 0, \"BackdropOpacity\": 7, \"FontSize\": 900}");
        var s = PerformanceSettingsStore.Load();
        Assert.Equal(PerformanceSettings.MinCodeOpacity, s.CodeOpacity);  // never invisible
        Assert.Equal(PerformanceMode.MaxOpacity, s.BackdropOpacity);
        Assert.Equal(PerformanceMode.MaxFontSize, s.FontSize);

        File.WriteAllText(_file, "{ not json");
        Assert.Equal(1.0, PerformanceSettingsStore.Load().CodeOpacity); // unreadable file: defaults
    }

    [Fact]
    public void Ctrl_Shift_arrows_step_the_code_transparency_by_ten_percent_within_range()
    {
        Assert.Equal(0.9, PerformanceMode.StepCodeOpacity(1.0, -1), 3);
        Assert.Equal(1.0, PerformanceMode.StepCodeOpacity(1.0, +1), 3);
        Assert.Equal(PerformanceSettings.MinCodeOpacity, PerformanceMode.StepCodeOpacity(0.25, -1), 3);
    }

    [Fact]
    public void One_roundness_slider_scales_every_corner()
    {
        var defaults = ThemeSettings.Default();
        Assert.Equal(1.0, defaults.Roundness, 3);

        var square = defaults.WithRoundness(0);
        Assert.All(ThemeSettings.Radii, r => Assert.Equal(0, r.Get(square)));

        var doubled = defaults.WithRoundness(2);
        Assert.Equal(defaults.RadiusSmall * 2, doubled.RadiusSmall);
        Assert.Equal(40, doubled.RadiusLarge);                     // 2 x 20, right at its maximum
        Assert.Equal(48, doubled.RadiusCard);                      // 2 x 24 = 48, its maximum
        Assert.All(ThemeSettings.Radii, r => Assert.InRange(r.Get(doubled), 0, r.Max));
        Assert.Equal(defaults.AccentColor, doubled.AccentColor);  // only corners change
    }

    [Fact]
    public void Only_language_shapes_and_fonts_ask_for_a_restart()
    {
        var saved = ThemeSettings.Default();

        var colorsOnly = saved.Clone();
        colorsOnly.AccentColor = "#A8D0E6";
        Assert.False(SettingsWindow.NeedsRestart(saved, colorsOnly, AppLanguage.Spanish, AppLanguage.Spanish)); // colors are live

        Assert.True(SettingsWindow.NeedsRestart(saved, saved.WithRoundness(0.5), AppLanguage.Spanish, AppLanguage.Spanish));

        var font = saved.Clone();
        font.FontMono = "Fira Code";
        Assert.True(SettingsWindow.NeedsRestart(saved, font, AppLanguage.Spanish, AppLanguage.Spanish));

        Assert.True(SettingsWindow.NeedsRestart(saved, saved.Clone(), AppLanguage.Spanish, AppLanguage.English));
    }
}
