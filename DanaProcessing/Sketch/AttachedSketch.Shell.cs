namespace DanaProcessing
{
    /// <summary>What a shell signal says.</summary>
    public enum ShellSignalKind
    {
        /// <summary>The last command succeeded.</summary>
        Ok,
        /// <summary>The last command failed.</summary>
        Error,
        /// <summary>A named signal sent by hand from the shell (<c>dana-signal party</c>).</summary>
        Custom,
    }

    /// <summary>One message from a shell (see <see cref="ShellSignals"/>).</summary>
    /// <param name="Kind">Ok, Error or Custom.</param>
    /// <param name="ExitCode">The command's exit code (0 for Ok, usually 1 or more for Error; 0 for Custom).</param>
    /// <param name="Name">For Custom: the signal's name, lower-case. Empty otherwise.</param>
    /// <param name="ProcessId">The shell's process id, or 0.</param>
    public readonly record struct ShellSignal(ShellSignalKind Kind, int ExitCode, string Name, int ProcessId)
    {
        /// <summary>
        /// Parses one line written by the profile snippet: "ok 0 1234",
        /// "error 1 1234" or "signal party 1234" (kind, exit code or name,
        /// shell pid; everything after the kind is optional). Null for
        /// anything else.
        /// </summary>
        public static ShellSignal? Parse(string? line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return null;
            var parts = line.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            string kind = parts[0].ToLowerInvariant();
            int pid = parts.Length > 2 && int.TryParse(parts[2], out var p) ? p : 0;
            switch (kind)
            {
                case "ok":
                    return new ShellSignal(ShellSignalKind.Ok, 0, "", pid);
                case "error":
                    int code = parts.Length > 1 && int.TryParse(parts[1], out var c) ? c : 1;
                    return new ShellSignal(ShellSignalKind.Error, code == 0 ? 1 : code, "", pid);
                case "signal" when parts.Length > 1:
                    return new ShellSignal(ShellSignalKind.Custom, 0, parts[1].ToLowerInvariant(), pid);
                default:
                    return null;
            }
        }

        /// <summary>The line the snippet writes for this signal (the inverse of Parse).</summary>
        public override string ToString() => Kind switch
        {
            ShellSignalKind.Ok => $"ok 0 {ProcessId}",
            ShellSignalKind.Error => $"error {ExitCode} {ProcessId}",
            _ => $"signal {Name} {ProcessId}",
        };
    }

    /// <summary>
    /// How a shell tells attached sketches that a command finished: a few lines
    /// in the shell's prompt write "ok"/"error" (and the exit code) to a small
    /// file every time a command ends; the sketch's host watches that file.
    /// Nothing is sent anywhere else, and the command line itself is never written.
    /// </summary>
    public static class ShellSignals
    {
        /// <summary>The file the snippet writes: %LOCALAPPDATA%\DanaProcessing\shell-signal.txt.</summary>
        public static string SignalFile =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DanaProcessing", "shell-signal.txt");

        /// <summary>
        /// Abyss Tide's own signal file (%LOCALAPPDATA%\AbyssTide\signal.txt):
        /// its prompt hook writes "ok" or "error" there after each command.
        /// Sketches listen to it too, so with Abyss Tide's hook installed they
        /// get commands without installing anything else (no exit code: a
        /// failure from there arrives as exit code 1).
        /// </summary>
        public static string AbyssTideSignalFile =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AbyssTide", "signal.txt");

        /// <summary>
        /// Writes a signal as if a shell had sent it -- to check that a sketch
        /// reacts without touching the shell (the IDE's Tools menu and the tray
        /// menu use it). It goes to the terminal that was in front most recently.
        /// </summary>
        public static void SendTest(ShellSignal signal) => SendTest(signal, SignalFile);

        internal static void SendTest(ShellSignal signal, string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, signal.ToString());
        }

        private const string BeginMarker = "# >>> DanaProcessing shell signals >>>";
        private const string EndMarker = "# <<< DanaProcessing shell signals <<<";

        /// <summary>
        /// The PowerShell lines to add to a profile ($PROFILE). They wrap the
        /// active prompt: after each command (not on an empty Enter) they
        /// write "ok 0 pid" or "error code pid". They also add
        /// <c>dana-signal name</c> to send a custom signal by hand.
        ///
        /// Same approach as Abyss Tide's hook, which works with oh-my-posh:
        /// prompt themes (oh-my-posh, starship, posh-git) replace the prompt
        /// function when they load -- often later, or in a profile that runs
        /// after this one -- so the wrapping runs right away and again every
        /// time the shell goes idle (PowerShell.OnIdle), whenever something
        /// replaced the prompt. The wrapper hands the prompt under it the $?
        /// and $LASTEXITCODE it would have seen (starship's trick), so the
        /// theme still shows the error.
        ///
        /// Several tools wrap the prompt this way (Abyss Tide does too), and
        /// two earlier designs could hang the console:
        /// - A wrapper that finds its inner prompt in a global variable at
        ///   call time forms a cycle as soon as it re-wraps something that
        ///   already wraps it (A calls D, D's variable now points at A...):
        ///   endless recursion on the next prompt. So each wrapper captures its
        ///   inner prompt in a closure (GetNewClosure): it can only point at a
        ///   prompt that existed before it, so the chain can never loop.
        /// - Two wrappers that each only check "am I the outermost prompt?"
        ///   wrap each other one more level on every idle. So each carries
        ///   "# __PromptWrapper:inner" (the closure variable holding what it
        ///   wraps), and the guard walks down the chain -- also through Abyss
        ///   Tide's and older Dana hooks -- and only wraps if it isn't in it.
        /// Belt and braces: if the wrapper is ever entered again while it's
        /// already running, it returns a plain prompt instead of recursing.
        /// </summary>
        public static string PowerShellSnippet { get; } = BeginMarker + @"
# Lets DanaProcessing sketches attached to this terminal react to commands.
# Works with oh-my-posh, starship, Abyss Tide and custom prompts: it wraps whatever
# prompt is active, wraps it again whenever something replaces it, and keeps $? and
# $LASTEXITCODE for the prompt underneath. Check: Get-EventSubscriber shows PowerShell.OnIdle.
if (-not (Test-Path variable:global:__danaWrap)) {
    $global:__danaSignalFile = Join-Path $env:LOCALAPPDATA 'DanaProcessing\shell-signal.txt'
    New-Item -ItemType Directory -Force (Split-Path $global:__danaSignalFile) | Out-Null
    $global:__danaLastHistoryId = (Get-History -Count 1).Id
    $global:__danaInPrompt = $false
    function global:dana-signal([string]$name) {
        [IO.File]::WriteAllText($global:__danaSignalFile, ""signal $name $PID"")
    }
    $global:__danaWrap = {
        # Already in the prompt chain? Walk down through the wrappers (ours, Abyss Tide's, others
        # that follow the '# __PromptWrapper:<variable>' convention) looking for this one.
        $p = $function:prompt
        for ($i = 0; $p -and $i -lt 30; $i++) {
            $t = ""$p""
            if ($t -match '__DanaShellSignals|__danaSignalFile') { return }   # this hook (or an older copy)
            if ($t -match '# __PromptWrapper:(\w+)') { $name = $Matches[1] }
            elseif ($t -match '__AbyssTideMarker') { $name = '__AbyssTidePrompt' }
            else { break }
            $inner = $null
            if ($p.Module) { $inner = $p.Module.SessionState.PSVariable.GetValue($name) }
            if (-not $inner) { $inner = Get-Variable -Scope Global -Name $name -ValueOnly -ErrorAction Ignore }
            $p = $inner
        }
        $inner = $function:prompt
        $function:global:prompt = {
            # __PromptWrapper:inner
            # __DanaShellSignals
            $ok = $?; $code = $global:LASTEXITCODE
            if ($global:__danaInPrompt) { return ""PS $($executionContext.SessionState.Path.CurrentLocation)> "" }
            $global:__danaInPrompt = $true
            try {
                try {
                    $last = Get-History -Count 1
                    if ($last -and $last.Id -ne $global:__danaLastHistoryId) {
                        $global:__danaLastHistoryId = $last.Id
                        $line = if ($ok) { ""ok 0 $PID"" } elseif ($code) { ""error $code $PID"" } else { ""error 1 $PID"" }
                        [IO.File]::WriteAllText($global:__danaSignalFile, $line)
                    }
                } catch { }
                # The prompt underneath reads $? and $LASTEXITCODE too: hand them back.
                $global:LASTEXITCODE = $code
                if (-not $ok) { Write-Error '' -ErrorAction Ignore }
                & $inner
            } finally {
                $global:__danaInPrompt = $false
            }
        }.GetNewClosure()
    }
    & $global:__danaWrap
    $null = Register-EngineEvent -SourceIdentifier PowerShell.OnIdle -Action { & $global:__danaWrap }
}
" + EndMarker;

        private static IReadOnlyList<string>? s_profiles;

        /// <summary>
        /// The profiles InstallForPowerShell() writes to: $PROFILE (the current
        /// user's profile for the console host, the one that loads last and
        /// where prompt themes usually live) of Windows PowerShell and, if
        /// installed, PowerShell 7. Asked to the shells themselves, so a
        /// moved or redirected Documents folder doesn't matter; falls back to
        /// Documents\WindowsPowerShell\Microsoft.PowerShell_profile.ps1. Cached.
        /// </summary>
        public static IReadOnlyList<string> PowerShellProfiles()
        {
            if (s_profiles != null)
                return s_profiles;
            var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var list = new List<string>();
            var windows = AskProfilePath("powershell.exe")
                ?? Path.Combine(docs, "WindowsPowerShell", "Microsoft.PowerShell_profile.ps1");
            list.Add(windows);
            if (AskProfilePath("pwsh.exe") is { } core)
                list.Add(core);
            return s_profiles = list;
        }

        /// <summary>
        /// Where the first version put the snippet (the all-hosts profile.ps1
        /// next to each $PROFILE). It loads BEFORE $PROFILE, so a theme set up
        /// in $PROFILE replaced the wrapper -- InstallForPowerShell() removes it
        /// from there.
        /// </summary>
        public static IReadOnlyList<string> LegacyProfiles() =>
            PowerShellProfiles()
                .Select(p => Path.Combine(Path.GetDirectoryName(p) ?? "", "profile.ps1"))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

        /// <summary>Runs a shell (no profile) to ask for its $PROFILE path. Null if it isn't installed or doesn't answer in time.</summary>
        private static string? AskProfilePath(string exe)
        {
            if (!OperatingSystem.IsWindows())
                return null;
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo(exe,
                    "-NoProfile -NoLogo -NonInteractive -Command \"$PROFILE.CurrentUserCurrentHost\"")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                using var p = System.Diagnostics.Process.Start(psi);
                if (p == null)
                    return null;
                var output = p.StandardOutput.ReadToEndAsync();
                if (!p.WaitForExit(10_000))
                {
                    try { p.Kill(); } catch { }
                    return null;
                }
                return ParseProfilePath(output.Result);
            }
            catch
            {
                return null; // not installed (pwsh is optional)
            }
        }

        /// <summary>The profile path in a shell's answer: its last non-empty line, if it's a rooted .ps1 path.</summary>
        internal static string? ParseProfilePath(string? output)
        {
            var line = output?.Split('\n').Select(l => l.Trim()).LastOrDefault(l => l.Length > 0);
            return line != null && line.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase) && Path.IsPathRooted(line)
                ? line
                : null;
        }

        /// <summary>
        /// Adds the snippet to <paramref name="profilePath"/> (creating it if
        /// needed), or replaces the copy already there -- running it twice never
        /// adds it twice. Returns true if the file changed. Takes effect in new
        /// shells (or after <c>. $PROFILE</c>).
        /// </summary>
        public static bool Install(string profilePath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(profilePath);
            string existing = File.Exists(profilePath) ? File.ReadAllText(profilePath) : "";
            string updated = WithSnippet(existing);
            if (updated == existing)
                return false;
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(profilePath))!);
            File.WriteAllText(profilePath, updated);
            return true;
        }

        /// <summary>
        /// Install() into $PROFILE of Windows PowerShell (and PowerShell 7 if
        /// present), and removes the first version's copy from the all-hosts
        /// profile.ps1. Returns the files that changed.
        /// </summary>
        public static IReadOnlyList<string> InstallForPowerShell() =>
            InstallInto(PowerShellProfiles(), LegacyProfiles());

        /// <summary>The testable part of InstallForPowerShell().</summary>
        internal static IReadOnlyList<string> InstallInto(IEnumerable<string> profiles, IEnumerable<string> legacy)
        {
            var targets = profiles.ToList();
            var changed = new List<string>();
            foreach (var old in legacy)
                if (!targets.Contains(old, StringComparer.OrdinalIgnoreCase) && Uninstall(old))
                    changed.Add(old);
            foreach (var profile in targets)
                if (Install(profile))
                    changed.Add(profile);
            return changed;
        }

        /// <summary>Removes the snippet from <paramref name="profilePath"/>. Returns true if it was there.</summary>
        public static bool Uninstall(string profilePath)
        {
            if (!File.Exists(profilePath))
                return false;
            string existing = File.ReadAllText(profilePath);
            string updated = WithoutSnippet(existing);
            if (updated == existing)
                return false;
            File.WriteAllText(profilePath, updated);
            return true;
        }

        /// <summary>True if the profile has this version of the snippet (an older copy counts as not installed: Install() updates it).</summary>
        public static bool IsInstalled(string profilePath) =>
            File.Exists(profilePath) && HasCurrentSnippet(File.ReadAllText(profilePath));

        internal static bool HasCurrentSnippet(string profile) =>
            profile.Replace("\r\n", "\n").Contains(PowerShellSnippet.Replace("\r\n", "\n"), StringComparison.Ordinal);

        internal static string WithSnippet(string profile)
        {
            string without = WithoutSnippet(profile).TrimEnd();
            string nl = profile.Contains("\r\n") || profile.Length == 0 ? "\r\n" : "\n";
            string snippet = PowerShellSnippet.Replace("\r\n", "\n").Replace("\n", nl);
            return (without.Length == 0 ? "" : without + nl + nl) + snippet + nl;
        }

        internal static string WithoutSnippet(string profile)
        {
            int start = profile.IndexOf(BeginMarker, StringComparison.Ordinal);
            if (start < 0)
                return profile;
            int end = profile.IndexOf(EndMarker, start, StringComparison.Ordinal);
            if (end < 0)
                return profile;
            end += EndMarker.Length;
            string before = profile[..start].TrimEnd();
            string after = profile[end..].TrimStart('\r', '\n');
            string nl = profile.Contains("\r\n") ? "\r\n" : "\n";
            if (before.Length == 0)
                return after;
            return after.Length == 0 ? before + nl : before + nl + nl + after;
        }
    }

    public abstract partial class AttachedSketch
    {
        /// <summary>True after ListenToShell().</summary>
        public bool ListensToShell { get; private set; }

        /// <summary>
        /// Commands that finish in the target's shell reach CommandFinished(),
        /// and hand-sent signals (<c>dana-signal party</c>) reach OnShellSignal().
        /// Needs the PowerShell profile snippet (see ShellSignals.InstallForPowerShell()
        /// or the reference page). With several terminals, a signal goes to the
        /// one in front.
        /// </summary>
        public void ListenToShell(bool listen = true) => ListensToShell = listen;

        /// <summary>True if the last command succeeded (true before the first one).</summary>
        public bool LastCommandOk { get; private set; } = true;

        /// <summary>The last command's exit code (0 before the first one).</summary>
        public int LastExitCode { get; private set; }

        /// <summary>How many command results have arrived.</summary>
        public int CommandCount { get; private set; }

        /// <summary>A command finished in the target's shell: read LastCommandOk / LastExitCode.</summary>
        public virtual void CommandFinished() { }

        /// <summary>A custom signal arrived (<c>dana-signal name</c> in the shell): name in lower-case.</summary>
        public virtual void OnShellSignal(string name) { }

        /// <summary>Host hook: one signal from the shell. Ignored unless the sketch called ListenToShell().</summary>
        internal void DispatchShellSignal(ShellSignal signal, Action<Action, string> run)
        {
            if (!ListensToShell)
                return;
            if (signal.Kind == ShellSignalKind.Custom)
            {
                run(() => OnShellSignal(signal.Name), nameof(OnShellSignal));
                return;
            }
            LastCommandOk = signal.Kind == ShellSignalKind.Ok;
            LastExitCode = signal.ExitCode;
            CommandCount++;
            run(CommandFinished, nameof(CommandFinished));
        }
    }
}
