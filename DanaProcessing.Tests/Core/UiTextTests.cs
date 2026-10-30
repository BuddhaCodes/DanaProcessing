using System.Globalization;
using DanaProcessing.AvaloniaHost;
using DanaProcessing.AvaloniaHost.Attach;
using DanaProcessing.Ide.Localization;
using Xunit;

namespace DanaProcessing.Tests.Core;

/// <summary>UiText is process-wide: these tests switch it, so nothing else runs alongside them.</summary>
[CollectionDefinition("UiText language", DisableParallelization = true)]
public class UiTextLanguageCollection { }

/// <summary>
/// The attached-sketch card, its badges, the speed line and the tray menu
/// follow the IDE's language (UiText, set by Loc.Initialize), and an exported
/// sketch on a machine without the IDE follows the system's.
/// </summary>
[Collection("UiText language")]
public class UiTextTests
{
    private static T InEnglish<T>(Func<T> f)
    {
        bool before = UiText.English;
        UiText.English = true;
        try { return f(); }
        finally { UiText.English = before; }
    }

    [Fact]
    public void Spanish_by_default_english_when_asked()
    {
        Assert.Equal("Pausar", UiText.Tr("Pausar", "Pause"));
        Assert.Equal("Pause", InEnglish(() => UiText.Tr("Pausar", "Pause")));
    }

    [Fact]
    public void Ide_language_sets_the_host_language()
    {
        var before = Loc.Current;
        try
        {
            Loc.Initialize(AppLanguage.English);
            Assert.True(UiText.English);
            Loc.Initialize(AppLanguage.Spanish);
            Assert.False(UiText.English);
        }
        finally { Loc.Initialize(before); }
    }

    [Fact]
    public void Badges_in_english()
    {
        var badges = InEnglish(() => AttachedSketchFleet.StatusBadges(AttachMode.EachWindow, 3, keyboard: true, mouse: true, shell: true, eco: true, paused: true));
        Assert.Equal(new[] { "PAUSED", "3 WINDOWS", "KEYBOARD", "MOUSE", "SHELL", "ECO" }, badges);
        Assert.Equal("1 WINDOW", InEnglish(() => AttachedSketchFleet.StatusBadges(AttachMode.EachWindow, 1, false, false, false, false, false))[0]);
        Assert.Equal("KEYBOARD", InEnglish(() => AttachedSketchFleet.KeyboardBadge));
    }

    [Fact]
    public void Speed_line_in_english()
    {
        var stats = new AttachedHostStats(60, 3.14, 0.9, 4, 2, 1);
        Assert.Equal("60 fps · draw 3.1 ms · present 0.9 ms · signal 4 ms · key 2 ms · move 1 ms", InEnglish(stats.Format));
        Assert.Equal("60 fps · dibujo 3.1 ms · presentar 0.9 ms · señal 4 ms · tecla 2 ms · mover 1 ms", stats.Format());
    }

    [Fact]
    public void Card_info_line_in_both_languages()
    {
        Assert.Equal("TerminalAudio · capa encima · cuadro 2868", AvaloniaSketchCanvas.AttachedCardInfoLine("TerminalAudio", AttachLayer.Above, 2868));
        Assert.Equal("TerminalAudio · layer behind · frame 7", InEnglish(() => AvaloniaSketchCanvas.AttachedCardInfoLine("TerminalAudio", AttachLayer.Behind, 7)));
    }

    [Theory]
    [InlineData(true, AppLanguage.Spanish, "en-US", AppLanguage.Spanish)]   // the IDE's setting wins
    [InlineData(true, AppLanguage.English, "es-MX", AppLanguage.English)]
    [InlineData(false, AppLanguage.Spanish, "es-ES", AppLanguage.Spanish)]  // no setting: the system's language
    [InlineData(false, AppLanguage.Spanish, "en-GB", AppLanguage.English)]
    [InlineData(false, AppLanguage.Spanish, "de-DE", AppLanguage.English)]
    public void Exported_sketch_language(bool hasSetting, AppLanguage saved, string culture, AppLanguage expected) =>
        Assert.Equal(expected, Loc.ForExport(hasSetting, saved, new CultureInfo(culture)));
}
