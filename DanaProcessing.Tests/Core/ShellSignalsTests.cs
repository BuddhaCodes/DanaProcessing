using DanaProcessing.AvaloniaHost.Attach;
using Xunit;

namespace DanaProcessing.Tests.Core;

/// <summary>Shell signals: the line format, the PowerShell profile snippet, and how a sketch receives them.</summary>
public class ShellSignalsTests
{
    [Theory]
    [InlineData("ok 0 1234", ShellSignalKind.Ok, 0, "", 1234)]
    [InlineData("error 3 77", ShellSignalKind.Error, 3, "", 77)]
    [InlineData("ERROR", ShellSignalKind.Error, 1, "", 0)]      // no code: still an error
    [InlineData("error 0 5", ShellSignalKind.Error, 1, "", 5)]  // an error is never exit code 0
    [InlineData("  ok  ", ShellSignalKind.Ok, 0, "", 0)]
    [InlineData("signal Party 9", ShellSignalKind.Custom, 0, "party", 9)]
    public void Parses_what_the_snippet_writes(string line, ShellSignalKind kind, int code, string name, int pid)
    {
        Assert.Equal(new ShellSignal(kind, code, name, pid), ShellSignal.Parse(line));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("hello world")]
    [InlineData("signal")]   // a custom signal needs a name
    public void Ignores_anything_else(string? line)
    {
        Assert.Null(ShellSignal.Parse(line));
    }

    [Fact]
    public void ToString_round_trips()
    {
        foreach (var s in new[] { new ShellSignal(ShellSignalKind.Ok, 0, "", 1), new ShellSignal(ShellSignalKind.Error, 2, "", 3), new ShellSignal(ShellSignalKind.Custom, 0, "reset", 4) })
            Assert.Equal(s, ShellSignal.Parse(s.ToString()));
    }

    [Fact]
    public void The_snippet_wraps_the_prompt_and_only_writes_the_signal_file()
    {
        var snippet = ShellSignals.PowerShellSnippet;
        Assert.Contains("$function:global:prompt = {", snippet);
        Assert.Contains("& $inner", snippet);                         // the prompt underneath still runs
        Assert.Contains("Get-History -Count 1", snippet);            // no signal on an empty Enter
        Assert.Contains("DanaProcessing\\shell-signal.txt", snippet);
        Assert.Contains("function global:dana-signal", snippet);
        Assert.DoesNotContain("CommandLine", snippet);               // never writes what you typed
        Assert.EndsWith("shell-signal.txt", ShellSignals.SignalFile);
        Assert.Contains("if (-not (Test-Path variable:global:__danaWrap))", snippet); // loading the profile twice is harmless
    }

    [Fact]
    public void The_snippet_rewraps_whenever_a_theme_replaces_the_prompt()
    {
        // Like Abyss Tide's hook (which works with oh-my-posh): wrap now, and
        // again on every idle if something replaced the prompt -- not just once.
        var snippet = ShellSignals.PowerShellSnippet;
        Assert.Contains("& $global:__danaWrap\n", snippet.Replace("\r\n", "\n"));
        Assert.Contains("Register-EngineEvent -SourceIdentifier PowerShell.OnIdle -Action { & $global:__danaWrap }", snippet);
        Assert.DoesNotContain("-MaxTriggerCount", snippet);
        Assert.DoesNotContain("-SupportEvent", snippet);              // Get-EventSubscriber shows it, for troubleshooting
    }

    [Fact]
    public void The_snippet_never_wraps_itself_twice_even_under_other_wrappers()
    {
        // Two hooks that both re-wrap on idle (Dana + Abyss Tide) would otherwise
        // wrap each other one more level every idle. Each wrapper says where it
        // keeps the prompt it wraps; the guard walks down that chain looking for
        // itself -- through Abyss Tide's hook and older Dana copies too.
        var snippet = ShellSignals.PowerShellSnippet;
        int guard = snippet.IndexOf("if ($t -match '__DanaShellSignals|__danaSignalFile') { return }", StringComparison.Ordinal);
        int abyss = snippet.IndexOf("elseif ($t -match '__AbyssTideMarker') { $name = '__AbyssTidePrompt' }", StringComparison.Ordinal);
        int closureVar = snippet.IndexOf("$p.Module.SessionState.PSVariable.GetValue($name)", StringComparison.Ordinal);
        int globalVar = snippet.IndexOf("Get-Variable -Scope Global -Name $name -ValueOnly -ErrorAction Ignore", StringComparison.Ordinal);
        int wrapper = snippet.IndexOf("$function:global:prompt = {", StringComparison.Ordinal);
        Assert.True(guard > 0 && abyss > guard && closureVar > abyss && globalVar > closureVar && wrapper > globalVar);
        string body = snippet[(wrapper + "$function:global:prompt = {".Length)..].TrimStart();
        Assert.StartsWith("# __PromptWrapper:inner", body);
        Assert.Contains("# __DanaShellSignals", body);
    }

    [Fact]
    public void The_chain_of_prompts_can_never_loop()
    {
        // The hang: a wrapper that read its inner prompt from a global variable
        // at call time formed a cycle when it re-wrapped a prompt that already
        // wrapped it (A -> D -> A -> ...). Now the inner prompt is captured in a
        // closure when the wrapper is made, so it can only point at an older prompt.
        var snippet = ShellSignals.PowerShellSnippet;
        int capture = snippet.IndexOf("$inner = $function:prompt\n", StringComparison.Ordinal) >= 0
            ? snippet.IndexOf("$inner = $function:prompt\n", StringComparison.Ordinal)
            : snippet.IndexOf("$inner = $function:prompt\r\n", StringComparison.Ordinal);
        int wrapper = snippet.IndexOf("$function:global:prompt = {", StringComparison.Ordinal);
        Assert.True(capture > 0 && wrapper > capture);
        Assert.Contains("}.GetNewClosure()", snippet);
        Assert.Contains("& $inner", snippet);
        Assert.DoesNotContain("$global:__danaPrompt", snippet);   // no shared, overwritable pointer
    }

    [Fact]
    public void Entering_the_wrapper_again_returns_a_plain_prompt_instead_of_recursing()
    {
        var snippet = ShellSignals.PowerShellSnippet;
        int check = snippet.IndexOf("if ($global:__danaInPrompt) { return", StringComparison.Ordinal);
        int set = snippet.IndexOf("$global:__danaInPrompt = $true", StringComparison.Ordinal);
        int call = snippet.IndexOf("& $inner", StringComparison.Ordinal);
        int reset = snippet.IndexOf("} finally {", StringComparison.Ordinal);
        Assert.True(check > 0 && set > check && call > set && reset > call);
        Assert.Contains("$global:__danaInPrompt = $false", snippet[reset..]);
        // $? is read before anything else runs, so the guard doesn't spoil it.
        Assert.True(snippet.IndexOf("$ok = $?; $code = $global:LASTEXITCODE", StringComparison.Ordinal) < check);
    }

    [Fact]
    public void A_test_signal_is_what_a_failed_command_writes()
    {
        var path = Path.Combine(Path.GetTempPath(), "dana-test-" + Guid.NewGuid().ToString("N"), "shell-signal.txt");
        ShellSignals.SendTest(new ShellSignal(ShellSignalKind.Error, 1, "", 0), path);
        Assert.Equal(new ShellSignal(ShellSignalKind.Error, 1, "", 0), ShellSignal.Parse(File.ReadAllText(path)));
    }

    [Fact]
    public void Abyss_Tides_signal_file_is_read_too()
    {
        Assert.EndsWith(Path.Combine("AbyssTide", "signal.txt"), ShellSignals.AbyssTideSignalFile);
        // Its hook writes a bare "ok"/"error".
        Assert.Equal(ShellSignalKind.Error, ShellSignal.Parse("error")!.Value.Kind);
        Assert.Equal(1, ShellSignal.Parse("error")!.Value.ExitCode);
        Assert.Equal(ShellSignalKind.Ok, ShellSignal.Parse("ok")!.Value.Kind);
    }

    [Theory]
    [InlineData("C:\\Users\\a\\Documents\\WindowsPowerShell\\Microsoft.PowerShell_profile.ps1\r\n", true)]
    [InlineData("warning: something\nD:\\Docs\\PowerShell\\Microsoft.PowerShell_profile.ps1", true)]
    [InlineData("", false)]
    [InlineData("not a path", false)]
    [InlineData("relative\\profile.ps1", false)]
    public void Reads_the_profile_path_a_shell_prints(string output, bool found)
    {
        if (!OperatingSystem.IsWindows() && found)
            output = output.Replace("C:\\", "/").Replace("D:\\", "/").Replace('\\', '/');
        var path = ShellSignals.ParseProfilePath(output);
        Assert.Equal(found, path != null);
    }

    [Fact]
    public void An_older_copy_counts_as_not_installed_and_install_upgrades_it()
    {
        var path = TempProfile("# >>> DanaProcessing shell signals >>>\nold wrapper\n# <<< DanaProcessing shell signals <<<\n");
        Assert.False(ShellSignals.IsInstalled(path));
        Assert.True(ShellSignals.Install(path));
        Assert.True(ShellSignals.IsInstalled(path));
        Assert.DoesNotContain("old wrapper", File.ReadAllText(path));
    }

    [Fact]
    public void Installing_moves_the_snippet_out_of_the_all_hosts_profile()
    {
        // v1 wrote profile.ps1, which loads before $PROFILE: a theme set up in
        // $PROFILE then replaced the wrapper. Now it goes in $PROFILE itself.
        var legacy = TempProfile("Set-Alias g git\n");
        Assert.True(ShellSignals.Install(legacy));
        var profile = Path.Combine(Path.GetDirectoryName(legacy)!, "Microsoft.PowerShell_profile.ps1");
        File.WriteAllText(profile, "oh-my-posh init powershell --config theme.omp.json | Invoke-Expression\n");

        var changed = ShellSignals.InstallInto(new[] { profile }, new[] { legacy });
        Assert.Equal(new[] { legacy, profile }, changed);
        Assert.False(ShellSignals.IsInstalled(legacy));
        Assert.Equal("Set-Alias g git\n", File.ReadAllText(legacy));
        string text = File.ReadAllText(profile);
        Assert.True(text.IndexOf("oh-my-posh init", StringComparison.Ordinal) < text.IndexOf("# >>> DanaProcessing", StringComparison.Ordinal),
            "the snippet goes after the theme's own line");
        Assert.Empty(ShellSignals.InstallInto(new[] { profile }, new[] { legacy })); // nothing left to do
    }

    // --- both hooks installed (Dana + Abyss Tide): one command, one signal ---------

    private static readonly ShellSignal DanaError = new(ShellSignalKind.Error, 3, "", 7);
    private static readonly ShellSignal AbyssError = new(ShellSignalKind.Error, 1, "", 0);
    private static readonly ShellSignal AbyssOk = new(ShellSignalKind.Ok, 0, "", 0);
    private static readonly DateTime Written = new(2026, 10, 25, 12, 0, 0, DateTimeKind.Utc);
    private const SignalSource Dana = SignalSource.Dana, Abyss = SignalSource.AbyssTide;

    private static ShellSignal[] Signals(IReadOnlyList<(ShellSignal Signal, DateTime Written)> items) =>
        items.Select(i => i.Signal).ToArray();

    [Fact]
    public void Dana_signals_go_out_at_once_with_when_they_were_written()
    {
        var m = new SignalMerger();
        var out1 = m.Offer(Dana, DanaError, Written, 10);
        Assert.Equal(new[] { DanaError }, Signals(out1));
        Assert.Equal(Written, out1[0].Written);
    }

    [Fact]
    public void Abyss_Tide_alone_arrives_after_a_short_wait()
    {
        var m = new SignalMerger();
        Assert.Empty(m.Offer(Abyss, AbyssError, Written, 10));
        Assert.True(m.HasPending);
        Assert.Empty(m.Tick(10.01));
        Assert.Equal(new[] { AbyssError }, Signals(m.Tick(10 + SignalMerger.Wait)));
        Assert.False(m.HasPending);
        Assert.True(SignalMerger.Wait <= 0.1, "the wait should be short now that file changes are noticed at once");
    }

    [Fact]
    public void The_same_command_from_both_hooks_counts_once_with_Danas_exit_code()
    {
        // Abyss Tide first, Dana's a moment later: only Dana's.
        var m = new SignalMerger();
        Assert.Empty(m.Offer(Abyss, AbyssError, Written, 10));
        Assert.Equal(new[] { DanaError }, Signals(m.Offer(Dana, DanaError, Written, 10.002)));
        Assert.Empty(m.Tick(10.5));

        // Dana's first, Abyss Tide's right after: dropped.
        m = new SignalMerger();
        Assert.Equal(new[] { DanaError }, Signals(m.Offer(Dana, DanaError, Written, 20)));
        Assert.Empty(m.Offer(Abyss, AbyssError, Written, 20.002));
        Assert.Empty(m.Tick(20.5));
    }

    [Fact]
    public void Different_results_from_the_two_hooks_both_count()
    {
        var m = new SignalMerger();
        m.Offer(Dana, DanaError, Written, 10);
        Assert.Empty(m.Offer(Abyss, AbyssOk, Written, 10.3)); // a later command that worked
        Assert.Equal(new[] { AbyssOk }, Signals(m.Tick(10.3 + SignalMerger.Wait)));
    }

    private static string TempProfile(string? content)
    {
        var dir = Path.Combine(Path.GetTempPath(), "dana-shell-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(dir, "WindowsPowerShell", "profile.ps1");
        if (content != null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
        }
        return path;
    }

    [Fact]
    public void Install_creates_the_profile_and_is_idempotent()
    {
        var path = TempProfile(null);
        Assert.False(ShellSignals.IsInstalled(path));
        Assert.True(ShellSignals.Install(path));
        Assert.True(ShellSignals.IsInstalled(path));
        string once = File.ReadAllText(path);
        Assert.False(ShellSignals.Install(path));               // nothing to change the second time
        Assert.Equal(once, File.ReadAllText(path));
    }

    [Fact]
    public void Install_keeps_the_rest_of_the_profile_and_uninstall_gives_it_back()
    {
        const string mine = "Set-Alias ll Get-ChildItem\r\nImport-Module posh-git\r\n";
        var path = TempProfile(mine);
        Assert.True(ShellSignals.Install(path));
        string installed = File.ReadAllText(path);
        Assert.StartsWith("Set-Alias ll Get-ChildItem\r\nImport-Module posh-git", installed);
        Assert.Contains("\r\n# >>> DanaProcessing", installed);  // keeps the file's CRLF line endings
        Assert.DoesNotContain("\r\r", installed);

        Assert.True(ShellSignals.Uninstall(path));
        Assert.Equal(mine.TrimEnd() + "\r\n", File.ReadAllText(path));
        Assert.False(ShellSignals.Uninstall(path));
    }

    [Fact]
    public void Reinstalling_replaces_an_old_copy_in_the_middle()
    {
        string old = "# before\n# >>> DanaProcessing shell signals >>>\nold stuff\n# <<< DanaProcessing shell signals <<<\n# after\n";
        string updated = ShellSignals.WithSnippet(old);
        Assert.DoesNotContain("old stuff", updated);
        Assert.Contains("# before", updated);
        Assert.Contains("# after", updated);
        Assert.Equal(1, CountOf(updated, "# >>> DanaProcessing"));
    }

    private static int CountOf(string s, string part)
    {
        int n = 0;
        for (int i = s.IndexOf(part, StringComparison.Ordinal); i >= 0; i = s.IndexOf(part, i + 1, StringComparison.Ordinal))
            n++;
        return n;
    }

    private class Listener : AttachedSketch
    {
        public readonly List<string> Log = new();
        public override void Draw() { }
        public override void CommandFinished() => Log.Add(LastCommandOk ? "ok" : $"error {LastExitCode}");
        public override void OnShellSignal(string name) => Log.Add("signal " + name);
    }

    private static readonly Action<Action, string> Run = (a, _) => a();

    [Fact]
    public void Signals_reach_the_sketch_only_after_ListenToShell()
    {
        var s = new Listener();
        s.DispatchShellSignal(new ShellSignal(ShellSignalKind.Error, 2, "", 0), Run);
        Assert.Empty(s.Log);
        Assert.True(s.LastCommandOk);

        s.ListenToShell();
        s.DispatchShellSignal(new ShellSignal(ShellSignalKind.Error, 2, "", 0), Run);
        s.DispatchShellSignal(new ShellSignal(ShellSignalKind.Ok, 0, "", 0), Run);
        s.DispatchShellSignal(new ShellSignal(ShellSignalKind.Custom, 0, "party", 0), Run);
        Assert.Equal(new[] { "error 2", "ok", "signal party" }, s.Log);
        Assert.True(s.LastCommandOk);
        Assert.Equal(0, s.LastExitCode);
        Assert.Equal(2, s.CommandCount); // custom signals aren't commands
    }
}
