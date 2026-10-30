using System.Text.Json;
using DanaProcessing.AvaloniaHost.Attach;
using DanaProcessing.Ide.Export;
using Xunit;

namespace DanaProcessing.Tests.Ide;

/// <summary>
/// Exporting into its own Windows Terminal profile, like Abyss Tide: a
/// see-through profile (a JSON fragment) and the sketch behind the terminal,
/// over a backdrop, so it only shows in that profile.
/// </summary>
public class TerminalProfileTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "dana-profile-" + Guid.NewGuid().ToString("N"));

    public TerminalProfileTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { }
    }

    [Fact]
    public void Fragment_has_a_see_through_profile_with_its_own_scheme()
    {
        using var doc = JsonDocument.Parse(TerminalProfile.BuildFragment("Tela \"sobre\" la terminal"));
        var scheme = doc.RootElement.GetProperty("schemes")[0];
        var profile = doc.RootElement.GetProperty("profiles")[0];
        Assert.Equal("Dana · Tela \"sobre\" la terminal", scheme.GetProperty("name").GetString());
        Assert.Equal(TerminalProfile.BackgroundHex, scheme.GetProperty("background").GetString());
        Assert.Equal("PowerShell · Tela \"sobre\" la terminal", profile.GetProperty("name").GetString());
        Assert.Equal(scheme.GetProperty("name").GetString(), profile.GetProperty("colorScheme").GetString());
        Assert.Equal(TerminalProfile.Opacity, profile.GetProperty("opacity").GetInt32());
        Assert.False(profile.GetProperty("useAcrylic").GetBoolean());
        Assert.Matches(@"^\{[0-9a-f-]{36}\}$", profile.GetProperty("guid").GetString());
        Assert.Contains("powershell.exe", profile.GetProperty("commandline").GetString());
        // Fixed tab title: the window is titled after it while this profile's tab is in front.
        Assert.Equal("Tela \"sobre\" la terminal", profile.GetProperty("tabTitle").GetString());
        Assert.True(profile.GetProperty("suppressApplicationTitle").GetBoolean());
    }

    [Fact]
    public void Profile_guid_is_stable_per_sketch()
    {
        Assert.Equal(TerminalProfile.ProfileGuid("Blobs"), TerminalProfile.ProfileGuid("Blobs"));
        Assert.Equal(TerminalProfile.ProfileGuid("Blobs"), TerminalProfile.ProfileGuid("blobs"));   // same install folder, same profile
        Assert.NotEqual(TerminalProfile.ProfileGuid("Blobs"), TerminalProfile.ProfileGuid("Tela"));
    }

    [Fact]
    public void Writes_the_fragment_and_the_backdrop_marker()
    {
        TerminalProfile.Write(_dir, "Blobs");
        Assert.True(File.Exists(Path.Combine(_dir, TerminalProfile.FragmentFileName)));
        string marker = File.ReadAllText(Path.Combine(_dir, ExportedSketchRunner.TerminalProfileMarkerFileName));
        Assert.Equal("#0B0F17E0\nBlobs\n", marker);
        var (backdrop, title) = ExportedSketchRunner.ParseProfileMarker(marker);
        Assert.Equal(TerminalProfile.DefaultBackdrop, backdrop);
        Assert.Equal("Blobs", title);
        Assert.Null(ExportedSketchRunner.ParseProfileMarker("#0B0F17E0").Title);   // an export from before the title
        Assert.Equal("Blobs", ExportedSketchRunner.ParseProfileMarker("#0B0F17E0\r\nBlobs\r\n").Title);
    }

    [Theory]
    [InlineData("#11223380", 0x11, 0x22, 0x33, 0x80)]
    [InlineData(" #112233 ", 0x11, 0x22, 0x33, 0xFF)]
    public void Backdrop_marker_is_css_order(string text, int r, int g, int b, int a)
    {
        var c = ExportedSketchRunner.ParseBackdrop(text);
        Assert.Equal((r, g, b, a), ((int)c.Red, (int)c.Green, (int)c.Blue, (int)c.Alpha));
    }

    [Fact]
    public void Bad_backdrop_falls_back_to_the_default() =>
        Assert.Equal(TerminalProfile.DefaultBackdrop, ExportedSketchRunner.ParseBackdrop("purple-ish"));

    [Fact]
    public void Profile_export_draws_behind_over_a_backdrop()
    {
        var plain = ExportedSketchRunner.BuildOverrides(eachWindow: false, profileBackdrop: null);
        Assert.Equal((null, null, null), (plain.Mode, plain.Layer, plain.Backdrop));

        var profile = ExportedSketchRunner.BuildOverrides(eachWindow: true, profileBackdrop: TerminalProfile.DefaultBackdrop, profileTitle: "Blobs");
        Assert.Equal("Blobs", profile.ProfileTitle);
        Assert.Null(ExportedSketchRunner.BuildOverrides(false, null, "Blobs").ProfileTitle);   // no profile, no gate
        Assert.Equal(AttachMode.EachWindow, profile.Mode);
        Assert.Equal(AttachLayer.Behind, profile.Layer);
        Assert.Equal(TerminalProfile.DefaultBackdrop, profile.Backdrop);
    }

    [Fact]
    public void Forcing_the_layer_overrides_attach_and_tells_the_host()
    {
        var sketch = new LayerSketch();
        sketch.Settings();
        Assert.Equal(AttachLayer.Above, sketch.Layer);
        int version = sketch.AttachVersion;
        sketch.ForceLayer(AttachLayer.Behind);
        Assert.Equal(AttachLayer.Behind, sketch.Layer);
        Assert.True(sketch.AttachVersion > version);
        sketch.ForceLayer(AttachLayer.Behind);   // same layer: nothing to do
        Assert.Equal(version + 1, sketch.AttachVersion);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Installer_installs_and_removes_the_profile(bool english)
    {
        string script = PowerShellInstaller.Build("Blobs", "app.exe", english);
        Assert.Contains(@"Microsoft\Windows Terminal\Fragments\DanaSketch-$id", script);
        Assert.Contains("'terminal-profile.json'", script);
        Assert.Contains("Copy-Item $fragSrc (Join-Path $fragDir \"dana-$id.json\") -Force", script);
        Assert.Equal(2, script.Split("Remove-Item $fragDir").Length - 1);   // on uninstall, and when reinstalled without a profile
        Assert.Contains("PowerShell · $name", script);
        // Terminal hides a fragment profile it saw before but can't find in settings.json:
        // the installer lists it there (with a backup), as Terminal itself would.
        Assert.Contains("function Register-TerminalProfile", script);
        Assert.Contains("    Register-TerminalProfile\r\n".Replace("\r\n", "\n"), script.Replace("\r\n", "\n"));
        Assert.Contains("source = \"DanaSketch-$id\"", script);
        Assert.Contains("settings.json.bak-dana", script);
        Assert.Contains(@"Packages\Microsoft.WindowsTerminal_8wekyb3d8bbwe\LocalState", script);
    }

    [Fact]
    public void Readme_names_the_profile()
    {
        PowerShellInstaller.Write(_dir, "Blobs", "app.exe", eachWindow: true, english: false, terminalProfile: true);
        Assert.Contains("\"PowerShell · Blobs\"", File.ReadAllText(Path.Combine(_dir, PowerShellInstaller.ReadmeFileName)));
    }

    private sealed class LayerSketch : AttachedSketch
    {
        // Not the terminal (a terminal sketch is always behind): a window that can be above.
        public override void Settings() => Attach(WindowTarget.Process("notepad"), AttachLayer.Above);
        public override void Draw() { }
    }
}
