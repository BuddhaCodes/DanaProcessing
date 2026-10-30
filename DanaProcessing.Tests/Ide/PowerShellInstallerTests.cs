using System.Diagnostics;
using System.Text;
using DanaProcessing.AvaloniaHost.Attach;
using DanaProcessing.Ide.Export;
using Xunit;

namespace DanaProcessing.Tests.Ide;

/// <summary>
/// Exporting an AttachedSketch as an installable PowerShell script, like
/// Abyss Tide: install.ps1 + LEEME.txt, the "one sketch per terminal" marker,
/// the exe's --install-shell-signals job, and running only once.
/// </summary>
public class PowerShellInstallerTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "dana-installer-" + Guid.NewGuid().ToString("N"));

    public PowerShellInstallerTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    [Theory]
    [InlineData("Terminal Cloth", "Terminal-Cloth")]
    [InlineData("Tela sobre la terminal", "Tela-sobre-la-terminal")]
    [InlineData("Ñandú: ¡olé!", "and-ol")]
    [InlineData("   ", "MiSketch")]
    [InlineData("a/b\\c", "abc")]
    public void Safe_id_keeps_only_path_safe_characters(string name, string expected) =>
        Assert.Equal(expected, PowerShellInstaller.SafeId(name));

    [Fact]
    public void Script_has_the_name_exe_and_every_option_and_no_placeholders_left()
    {
        string script = PowerShellInstaller.Build("Tela de Jose's", "DanaProcessing.Ide.exe");
        Assert.DoesNotContain("__", script.Replace("__PromptWrapper", ""));
        Assert.Contains("$name     = 'Tela de Jose''s'", script);              // a quote in the name is escaped
        Assert.Contains("$id       = 'Tela-de-Joses'", script);
        Assert.Contains("$exeName  = 'DanaProcessing.Ide.exe'", script);
        Assert.Contains("param([switch]$Uninstall, [switch]$NoAutostart, [switch]$NoShellHook, [switch]$RemoveShellHook)", script);
        Assert.Contains(@"DanaProcessing\Sketches\$id", script);
        Assert.Contains("'--install-shell-signals'", script);
        Assert.Contains("'--uninstall-shell-signals'", script);
        Assert.Contains("GetFolderPath('Startup')", script);
        Assert.Contains("Unblock-File", script);
    }

    [Fact]
    public void Script_arguments_match_what_the_exe_understands()
    {
        string script = PowerShellInstaller.Build("x", "x.exe");
        Assert.Contains("'" + ExportedSketchRunner.InstallShellSignalsArg + "'", script);
        Assert.Contains("'" + ExportedSketchRunner.UninstallShellSignalsArg + "'", script);
    }

    [Fact]
    public void Files_are_utf8_with_bom_and_crlf_for_windows_powershell()
    {
        PowerShellInstaller.Write(_dir, "Blobs", "app.exe", eachWindow: true);
        foreach (var file in new[] { PowerShellInstaller.ScriptFileName, PowerShellInstaller.ReadmeFileName })
        {
            byte[] bytes = File.ReadAllBytes(Path.Combine(_dir, file));
            Assert.True(bytes.Length > 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF, file + " has a BOM");
            string text = Encoding.UTF8.GetString(bytes);
            Assert.DoesNotContain("\n", text.Replace("\r\n", ""));
        }
        Assert.Contains("cada ventana", File.ReadAllText(Path.Combine(_dir, PowerShellInstaller.ReadmeFileName)));
    }

    [Fact]
    public void English_installer_when_the_ide_is_in_english()
    {
        string es = PowerShellInstaller.Build("Blobs", "app.exe");
        string en = PowerShellInstaller.Build("Blobs", "app.exe", english: true);
        Assert.Contains("desinstalado", es);
        Assert.Contains("uninstalled", en);
        Assert.DoesNotContain("desinstalado", en);
        Assert.DoesNotContain("Abre Windows Terminal", en);
        // Same code, only the words differ: every option and step is in both.
        foreach (var piece in new[] { "param([switch]$Uninstall", "'--install-shell-signals'", "'--uninstall-shell-signals'", "GetFolderPath('Startup')", "Unblock-File", @"DanaProcessing\Sketches\$id" })
        {
            Assert.Contains(piece, es);
            Assert.Contains(piece, en);
        }
        Assert.Equal(es.Split('\n').Count(l => l.TrimStart().StartsWith("if ") || l.Contains("Remove-Item")),
                     en.Split('\n').Count(l => l.TrimStart().StartsWith("if ") || l.Contains("Remove-Item")));

        PowerShellInstaller.Write(_dir, "Blobs", "app.exe", eachWindow: true, english: true);
        Assert.True(File.Exists(Path.Combine(_dir, PowerShellInstaller.ReadmeFileNameEnglish)));
        Assert.Contains("every Windows Terminal window", File.ReadAllText(Path.Combine(_dir, PowerShellInstaller.ReadmeFileNameEnglish)));
        Assert.Contains("installed.", File.ReadAllText(Path.Combine(_dir, PowerShellInstaller.ScriptFileName)));
    }

    [Fact]
    public void Finds_the_exe_to_start()
    {
        File.WriteAllText(Path.Combine(_dir, "createdump.exe"), "");
        Assert.Null(PowerShellInstaller.FindExe(_dir, "Dana.exe"));
        File.WriteAllText(Path.Combine(_dir, "Sketch.exe"), "");
        Assert.Equal("Sketch.exe", PowerShellInstaller.FindExe(_dir, "Dana.exe"));   // the only real one
        File.WriteAllText(Path.Combine(_dir, "Dana.exe"), "");
        Assert.Equal("Dana.exe", PowerShellInstaller.FindExe(_dir, "Dana.exe"));     // the preferred one wins
        File.Delete(Path.Combine(_dir, "Dana.exe"));
        File.WriteAllText(Path.Combine(_dir, "Other.exe"), "");
        Assert.Null(PowerShellInstaller.FindExe(_dir, null));                         // two candidates: can't tell
    }

    /// <summary>Windows: the script must parse in Windows PowerShell (and pwsh if present). Elsewhere, when pwsh exists.</summary>
    [Fact]
    public void Script_parses_without_errors_in_powershell()
    {
        PowerShellInstaller.Write(_dir, "Tela 'de' prueba", "app.exe", eachWindow: false);
        var path = Path.Combine(_dir, PowerShellInstaller.ScriptFileName);
        foreach (var shell in new[] { "powershell.exe", "pwsh" })
        {
            string command = "$e=$null; [void][System.Management.Automation.Language.Parser]::ParseFile('" + path.Replace("'", "''") + "',[ref]$null,[ref]$e); $e.Count; $e | ForEach-Object { $_.Message }";
            var psi = new ProcessStartInfo(shell) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
            psi.ArgumentList.Add("-NoProfile");
            psi.ArgumentList.Add("-Command");
            psi.ArgumentList.Add(command);
            Process? p;
            try { p = Process.Start(psi); }
            catch { continue; }   // that shell isn't installed
            using (p!)
            {
                string output = p.StandardOutput.ReadToEnd();
                p.WaitForExit(60_000);
                Assert.True(output.Trim().StartsWith("0"), $"{shell}: {output}");
            }
        }
    }

    [Fact]
    public void Exe_does_the_installers_jobs_and_otherwise_runs()
    {
        int installs = 0, uninstalls = 0;
        IReadOnlyList<string> Install() { installs++; return Array.Empty<string>(); }
        void Uninstall() => uninstalls++;

        Assert.Null(ExportedSketchRunner.HandleCommandLine(Array.Empty<string>(), Install, Uninstall));
        Assert.Null(ExportedSketchRunner.HandleCommandLine(new[] { "danaide://run?code=x" }, Install, Uninstall));
        Assert.Equal(0, ExportedSketchRunner.HandleCommandLine(new[] { "--install-shell-signals" }, Install, Uninstall));
        Assert.Equal(0, ExportedSketchRunner.HandleCommandLine(new[] { "--UNINSTALL-shell-signals" }, Install, Uninstall));
        Assert.Equal((1, 1), (installs, uninstalls));
        Assert.Equal(1, ExportedSketchRunner.HandleCommandLine(new[] { "--install-shell-signals" }, () => throw new IOException("locked"), Uninstall));
    }

    [Fact]
    public void Runs_once_per_exe_path()
    {
        string exe = Path.Combine(_dir, "Sketch.exe");
        Assert.Equal(ExportedSketchRunner.SingleInstanceName(exe), ExportedSketchRunner.SingleInstanceName(exe.ToUpperInvariant()));
        Assert.NotEqual(ExportedSketchRunner.SingleInstanceName(exe), ExportedSketchRunner.SingleInstanceName(Path.Combine(_dir, "Other.exe")));

        // Claimed from another thread, as another process would hold it.
        bool first = false, second = true;
        System.Threading.Mutex? held = null;
        var claimed = new ManualResetEventSlim();
        var release = new ManualResetEventSlim();
        var owner = new Thread(() =>
        {
            first = ExportedSketchRunner.TryClaimSingleInstance(exe, out held);
            claimed.Set();
            release.Wait();
            held?.ReleaseMutex();
            held?.Dispose();
        });
        owner.Start();
        claimed.Wait();
        second = ExportedSketchRunner.TryClaimSingleInstance(exe, out var none);
        release.Set();
        owner.Join();
        Assert.True(first);
        Assert.False(second);
        Assert.Null(none);
    }

    [Theory]
    [InlineData(AttachMode.Front, null, AttachMode.Front)]
    [InlineData(AttachMode.EachWindow, null, AttachMode.EachWindow)]
    [InlineData(AttachMode.Front, AttachMode.EachWindow, AttachMode.EachWindow)]
    public void One_sketch_per_terminal_overrides_what_attach_asked_for(AttachMode sketch, AttachMode? forced, AttachMode expected) =>
        Assert.Equal(expected, AttachedSketchFleet.ResolveMode(sketch, forced));
}
