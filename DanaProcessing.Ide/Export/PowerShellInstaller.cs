using System;
using System.IO;
using System.Linq;
using System.Text;

namespace DanaProcessing.Ide.Export
{
    /// <summary>What an exported AttachedSketch does beyond the plain tray app.</summary>
    /// <param name="EachWindow">One instance of the sketch on every terminal window, whatever its Attach() says (marker file sketch.eachwindow).</param>
    /// <param name="PowerShellInstaller">Adds install.ps1 (and LEEME.txt): installs it per user like Abyss Tide does.</param>
    /// <param name="TerminalProfile">Its own Windows Terminal profile, like Abyss Tide: the sketch draws behind the terminal and only shows in that see-through profile (see TerminalProfile).</param>
    /// <param name="UseGpu">Frames go to screen through the GPU (DirectComposition, see DCompWindow), with the CPU presenter as fallback (marker file sketch.gpu).</param>
    public sealed record AttachedExportOptions(bool EachWindow, bool PowerShellInstaller, bool TerminalProfile = false, bool UseGpu = false);

    /// <summary>
    /// The install.ps1 that ships with an exported AttachedSketch, modelled on
    /// Abyss Tide's: run it once and the sketch is copied to
    /// %LOCALAPPDATA%\DanaProcessing\Sketches\&lt;id&gt;, starts with Windows
    /// (a shortcut in the Startup folder), gets the PowerShell signal hook
    /// (error/ok of the last command) and starts right away. -Uninstall
    /// undoes it. No administrator needed.
    ///
    /// The hook itself is installed by the exported exe
    /// (<c>--install-shell-signals</c>, see ExportedSketchRunner), so the
    /// snippet lives in one place (ShellSignals) and not twice.
    /// </summary>
    public static class PowerShellInstaller
    {
        public const string ScriptFileName = "install.ps1";
        public const string ReadmeFileName = "LEEME.txt";
        public const string ReadmeFileNameEnglish = "README.txt";

        /// <summary>The folder name under %LOCALAPPDATA%\DanaProcessing\Sketches: letters, digits, '-' and '_' only.</summary>
        public static string SafeId(string name)
        {
            var id = new string((name ?? "").Trim().Select(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' ? c : c == ' ' ? '-' : '\0')
                                                   .Where(c => c != '\0').ToArray()).Trim('-');
            return id.Length == 0 ? "MiSketch" : id;
        }

        /// <summary>
        /// The exe the installer starts: the one named <paramref name="preferred"/>
        /// if it's there, otherwise the only other .exe in the folder (createdump.exe,
        /// the runtime's crash helper, doesn't count). Null if it can't tell.
        /// </summary>
        public static string? FindExe(string exportFolder, string? preferred)
        {
            if (preferred != null && File.Exists(Path.Combine(exportFolder, preferred)))
                return preferred;
            var candidates = Directory.GetFiles(exportFolder, "*.exe")
                .Select(Path.GetFileName)
                .Where(n => !string.Equals(n, "createdump.exe", StringComparison.OrdinalIgnoreCase))
                .ToList();
            return candidates.Count == 1 ? candidates[0] : null;
        }

        /// <summary>Writes install.ps1 and LEEME.txt into <paramref name="exportFolder"/>.</summary>
        /// <param name="english">The script's and LEEME/README's language (the IDE's, at export time).</param>
        public static void Write(string exportFolder, string sketchName, string exeName, bool eachWindow, bool english = false, bool terminalProfile = false)
        {
            // UTF-8 *with* BOM and CRLF: Windows PowerShell 5.1 reads a BOM-less
            // script as ANSI and would mangle the accents.
            var utf8Bom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
            File.WriteAllText(Path.Combine(exportFolder, ScriptFileName), Build(sketchName, exeName, english).Replace("\r\n", "\n").Replace("\n", "\r\n"), utf8Bom);
            File.WriteAllText(Path.Combine(exportFolder, english ? ReadmeFileNameEnglish : ReadmeFileName), (english ? ReadmeEnglish(sketchName, eachWindow, terminalProfile) : Readme(sketchName, eachWindow, terminalProfile)).Replace("\r\n", "\n").Replace("\n", "\r\n"), utf8Bom);
        }

        /// <summary>A PowerShell single-quoted string literal ('' escapes a quote).</summary>
        internal static string Quote(string text) => "'" + text.Replace("'", "''") + "'";

        /// <summary>The script itself.</summary>
        public static string Build(string sketchName, string exeName, bool english = false)
        {
            string name = string.IsNullOrWhiteSpace(sketchName) ? "MiSketch" : sketchName.Trim();
            return (english ? TemplateEnglish : Template)
                .Replace("__NAME__", Quote(name))
                .Replace("__ID__", Quote(SafeId(name)))
                .Replace("__EXE__", Quote(exeName))
                .Replace("__TITLE__", name.Replace("#>", "# >"));
        }

        private const string Template = """
<#
  Instala __TITLE__ (un sketch de Dana que dibuja sobre Windows Terminal) para el
  usuario actual. No requiere administrador.

    .\install.ps1                    instala, arranca con Windows y lo arranca ya
    .\install.ps1 -NoAutostart       sin arrancar con Windows
    .\install.ps1 -NoShellHook       sin tocar tu perfil de PowerShell (no sabrá si un comando falló)
    .\install.ps1 -Uninstall         lo detiene y lo quita
    .\install.ps1 -Uninstall -RemoveShellHook
                                     también quita el hook del perfil (lo comparten todos los sketches de Dana)

  Si PowerShell bloquea el script:
    powershell -ExecutionPolicy Bypass -File .\install.ps1
#>
param([switch]$Uninstall, [switch]$NoAutostart, [switch]$NoShellHook, [switch]$RemoveShellHook)

$ErrorActionPreference = 'Stop'
$name     = __NAME__
$id       = __ID__
$exeName  = __EXE__
$here     = $PSScriptRoot
$appDir   = Join-Path $env:LOCALAPPDATA "DanaProcessing\Sketches\$id"
$exe      = Join-Path $appDir $exeName
$startLnk = Join-Path ([Environment]::GetFolderPath('Startup')) "$name (Dana).lnk"
$fragDir  = Join-Path $env:LOCALAPPDATA "Microsoft\Windows Terminal\Fragments\DanaSketch-$id"
$fragSrc  = Join-Path $appDir 'terminal-profile.json'

# Detiene la copia instalada si está corriendo (solo esa: otra copia del mismo exe en otra carpeta no se toca).
function Stop-Sketch {
    Get-Process -ErrorAction SilentlyContinue |
        Where-Object { try { $_.Path -and ($_.Path -ieq $exe) } catch { $false } } |
        ForEach-Object { Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue }
    Start-Sleep -Milliseconds 400
}

# El hook del prompt lo instala/quita el propio exe: así el texto vive en un solo sitio.
function Invoke-Sketch([string]$arg) {
    $p = Start-Process -FilePath $exe -ArgumentList $arg -WindowStyle Hidden -Wait -PassThru
    return $p.ExitCode
}

if ($Uninstall) {
    Stop-Sketch
    Remove-Item $startLnk -Force -ErrorAction SilentlyContinue
    Remove-Item $fragDir -Recurse -Force -ErrorAction SilentlyContinue
    if ($RemoveShellHook -and (Test-Path $exe)) { [void](Invoke-Sketch '--uninstall-shell-signals') }
    Remove-Item $appDir -Recurse -Force -ErrorAction SilentlyContinue
    Write-Host "$name desinstalado."
    if (-not $RemoveShellHook) { Write-Host '  El hook del prompt sigue en tu perfil (otros sketches de Dana lo usan). Para quitarlo: -Uninstall -RemoveShellHook' }
    return
}

if (-not (Test-Path (Join-Path $here $exeName))) { throw "No encuentro $exeName junto a install.ps1. Ejecútalo desde la carpeta exportada." }

Stop-Sketch
if ([IO.Path]::GetFullPath($here).TrimEnd('\') -ine [IO.Path]::GetFullPath($appDir).TrimEnd('\')) {
    if (Test-Path $appDir) { Remove-Item $appDir -Recurse -Force }   # una versión anterior: se reemplaza entera
    New-Item -ItemType Directory -Force -Path $appDir | Out-Null
    Copy-Item -Path (Join-Path $here '*') -Destination $appDir -Recurse -Force
}
# Archivos bajados de internet o sacados de un zip: sin esto Windows puede bloquear las DLL.
Get-ChildItem $appDir -Recurse -File | Unblock-File -ErrorAction SilentlyContinue

# Windows Terminal oculta un perfil de fragmento que ya vio una vez pero que no está en
# settings.json (lo toma por "borrado por el usuario"). Lo anotamos ahí como lo haría
# la propia terminal; la terminal recarga settings.json sola. Copia en settings.json.bak-dana.
function Register-TerminalProfile {
    try {
        $p = (Get-Content $fragSrc -Raw -Encoding UTF8 | ConvertFrom-Json).profiles[0]
        $stub = [ordered]@{ guid = $p.guid; hidden = $false; name = $p.name; source = "DanaSketch-$id" } | ConvertTo-Json -Compress
        foreach ($dir in 'Packages\Microsoft.WindowsTerminal_8wekyb3d8bbwe\LocalState', 'Packages\Microsoft.WindowsTerminalPreview_8wekyb3d8bbwe\LocalState', 'Microsoft\Windows Terminal') {
            $settings = Join-Path $env:LOCALAPPDATA "$dir\settings.json"
            if (-not (Test-Path $settings)) { continue }
            $text = [IO.File]::ReadAllText($settings)
            if ($text.IndexOf($p.guid, [StringComparison]::OrdinalIgnoreCase) -ge 0) { continue }
            $m = [regex]::Match($text, '"list"\s*:\s*\[')
            if (-not $m.Success) { $m = [regex]::Match($text, '"profiles"\s*:\s*\[') }
            if (-not $m.Success) { continue }
            $at = $m.Index + $m.Length
            $comma = if ($text.Substring($at) -match '^\s*\]') { '' } else { ',' }
            Copy-Item $settings "$settings.bak-dana" -Force
            [IO.File]::WriteAllText($settings, $text.Insert($at, "`r`n            " + $stub + $comma), (New-Object Text.UTF8Encoding $false))
        }
    } catch {
        Write-Warning "No se pudo anotar el perfil en settings.json de Windows Terminal: $($_.Exception.Message)"
    }
}

# Su propio perfil de Windows Terminal (como Abyss Tide): un fragmento + su entrada en settings.json.
$hasProfile = Test-Path $fragSrc
if ($hasProfile) {
    New-Item -ItemType Directory -Force -Path $fragDir | Out-Null
    Copy-Item $fragSrc (Join-Path $fragDir "dana-$id.json") -Force
    Register-TerminalProfile
} else {
    Remove-Item $fragDir -Recurse -Force -ErrorAction SilentlyContinue
}

if (-not $NoShellHook) {
    if ((Invoke-Sketch '--install-shell-signals') -ne 0) { Write-Warning 'No se pudo instalar el hook del prompt; el sketch funciona igual, sin saber si un comando falló.' }
}

if ($NoAutostart) {
    Remove-Item $startLnk -Force -ErrorAction SilentlyContinue
} else {
    $sh = New-Object -ComObject WScript.Shell
    $lnk = $sh.CreateShortcut($startLnk)
    $lnk.TargetPath = $exe
    $lnk.WorkingDirectory = $appDir
    $lnk.Description = "$name — sketch de Dana sobre Windows Terminal"
    $lnk.Save()
}

Start-Process -FilePath $exe -WorkingDirectory $appDir

Write-Host ''
Write-Host "$name instalado." -ForegroundColor Cyan
Write-Host "  Carpeta: $appDir"
if ($hasProfile) {
    Write-Host "  Reinicia Windows Terminal y abre el perfil ""PowerShell · $name"": el sketch solo se ve en ese perfil (es translúcido; los demás lo tapan)."
} else {
    Write-Host '  Abre Windows Terminal: el sketch se dibuja detrás del texto; solo se ve si el perfil es translúcido ("opacity" por debajo de 100).'
}
Write-Host '  El icono de la bandeja permite pausarlo o salir.'
if (-not $NoAutostart) { Write-Host '  Arranca solo al iniciar sesión en Windows.' }
if (-not $NoShellHook) { Write-Host '  Abre una terminal nueva para que el sketch sepa si un comando falló.' }
Write-Host '  Para quitarlo: .\install.ps1 -Uninstall'
""";

        private const string TemplateEnglish = """
<#
  Installs __TITLE__ (a Dana sketch that draws on Windows Terminal) for the
  current user. No administrator needed.

    .\install.ps1                    install, start with Windows, start it now
    .\install.ps1 -NoAutostart       don't start with Windows
    .\install.ps1 -NoShellHook       leave your PowerShell profile alone (it won't know when a command fails)
    .\install.ps1 -Uninstall         stop it and remove it
    .\install.ps1 -Uninstall -RemoveShellHook
                                     also remove the profile hook (shared by every Dana sketch)

  If PowerShell blocks the script:
    powershell -ExecutionPolicy Bypass -File .\install.ps1
#>
param([switch]$Uninstall, [switch]$NoAutostart, [switch]$NoShellHook, [switch]$RemoveShellHook)

$ErrorActionPreference = 'Stop'
$name     = __NAME__
$id       = __ID__
$exeName  = __EXE__
$here     = $PSScriptRoot
$appDir   = Join-Path $env:LOCALAPPDATA "DanaProcessing\Sketches\$id"
$exe      = Join-Path $appDir $exeName
$startLnk = Join-Path ([Environment]::GetFolderPath('Startup')) "$name (Dana).lnk"
$fragDir  = Join-Path $env:LOCALAPPDATA "Microsoft\Windows Terminal\Fragments\DanaSketch-$id"
$fragSrc  = Join-Path $appDir 'terminal-profile.json'

# Stops the installed copy if it's running (only that one: the same exe in another folder is left alone).
function Stop-Sketch {
    Get-Process -ErrorAction SilentlyContinue |
        Where-Object { try { $_.Path -and ($_.Path -ieq $exe) } catch { $false } } |
        ForEach-Object { Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue }
    Start-Sleep -Milliseconds 400
}

# The exe itself installs/removes the prompt hook, so its text lives in one place.
function Invoke-Sketch([string]$arg) {
    $p = Start-Process -FilePath $exe -ArgumentList $arg -WindowStyle Hidden -Wait -PassThru
    return $p.ExitCode
}

if ($Uninstall) {
    Stop-Sketch
    Remove-Item $startLnk -Force -ErrorAction SilentlyContinue
    Remove-Item $fragDir -Recurse -Force -ErrorAction SilentlyContinue
    if ($RemoveShellHook -and (Test-Path $exe)) { [void](Invoke-Sketch '--uninstall-shell-signals') }
    Remove-Item $appDir -Recurse -Force -ErrorAction SilentlyContinue
    Write-Host "$name uninstalled."
    if (-not $RemoveShellHook) { Write-Host '  The prompt hook stays in your profile (other Dana sketches use it). To remove it: -Uninstall -RemoveShellHook' }
    return
}

if (-not (Test-Path (Join-Path $here $exeName))) { throw "Can't find $exeName next to install.ps1. Run it from the exported folder." }

Stop-Sketch
if ([IO.Path]::GetFullPath($here).TrimEnd('\') -ine [IO.Path]::GetFullPath($appDir).TrimEnd('\')) {
    if (Test-Path $appDir) { Remove-Item $appDir -Recurse -Force }   # an older version: replaced entirely
    New-Item -ItemType Directory -Force -Path $appDir | Out-Null
    Copy-Item -Path (Join-Path $here '*') -Destination $appDir -Recurse -Force
}
# Files downloaded or taken out of a zip: without this Windows may block the DLLs.
Get-ChildItem $appDir -Recurse -File | Unblock-File -ErrorAction SilentlyContinue

# Windows Terminal hides a fragment profile it has seen once but can't find in
# settings.json (it takes it as "deleted by the user"). List it there, as the terminal
# itself would; the terminal reloads settings.json by itself. Backup: settings.json.bak-dana.
function Register-TerminalProfile {
    try {
        $p = (Get-Content $fragSrc -Raw -Encoding UTF8 | ConvertFrom-Json).profiles[0]
        $stub = [ordered]@{ guid = $p.guid; hidden = $false; name = $p.name; source = "DanaSketch-$id" } | ConvertTo-Json -Compress
        foreach ($dir in 'Packages\Microsoft.WindowsTerminal_8wekyb3d8bbwe\LocalState', 'Packages\Microsoft.WindowsTerminalPreview_8wekyb3d8bbwe\LocalState', 'Microsoft\Windows Terminal') {
            $settings = Join-Path $env:LOCALAPPDATA "$dir\settings.json"
            if (-not (Test-Path $settings)) { continue }
            $text = [IO.File]::ReadAllText($settings)
            if ($text.IndexOf($p.guid, [StringComparison]::OrdinalIgnoreCase) -ge 0) { continue }
            $m = [regex]::Match($text, '"list"\s*:\s*\[')
            if (-not $m.Success) { $m = [regex]::Match($text, '"profiles"\s*:\s*\[') }
            if (-not $m.Success) { continue }
            $at = $m.Index + $m.Length
            $comma = if ($text.Substring($at) -match '^\s*\]') { '' } else { ',' }
            Copy-Item $settings "$settings.bak-dana" -Force
            [IO.File]::WriteAllText($settings, $text.Insert($at, "`r`n            " + $stub + $comma), (New-Object Text.UTF8Encoding $false))
        }
    } catch {
        Write-Warning "Could not list the profile in the Windows Terminal settings.json: $($_.Exception.Message)"
    }
}

# Its own Windows Terminal profile (like Abyss Tide): a fragment + its entry in settings.json.
$hasProfile = Test-Path $fragSrc
if ($hasProfile) {
    New-Item -ItemType Directory -Force -Path $fragDir | Out-Null
    Copy-Item $fragSrc (Join-Path $fragDir "dana-$id.json") -Force
    Register-TerminalProfile
} else {
    Remove-Item $fragDir -Recurse -Force -ErrorAction SilentlyContinue
}

if (-not $NoShellHook) {
    if ((Invoke-Sketch '--install-shell-signals') -ne 0) { Write-Warning 'Could not install the prompt hook; the sketch works anyway, without knowing when a command fails.' }
}

if ($NoAutostart) {
    Remove-Item $startLnk -Force -ErrorAction SilentlyContinue
} else {
    $sh = New-Object -ComObject WScript.Shell
    $lnk = $sh.CreateShortcut($startLnk)
    $lnk.TargetPath = $exe
    $lnk.WorkingDirectory = $appDir
    $lnk.Description = "$name — a Dana sketch on Windows Terminal"
    $lnk.Save()
}

Start-Process -FilePath $exe -WorkingDirectory $appDir

Write-Host ''
Write-Host "$name installed." -ForegroundColor Cyan
Write-Host "  Folder: $appDir"
if ($hasProfile) {
    Write-Host "  Restart Windows Terminal and open the ""PowerShell · $name"" profile: the sketch only shows in that profile (it's see-through; the others cover it)."
} else {
    Write-Host '  Open Windows Terminal: the sketch draws behind the text; it only shows if the profile is see-through ("opacity" below 100).'
}
Write-Host '  The tray icon can pause it or quit.'
if (-not $NoAutostart) { Write-Host '  It starts by itself when you sign in to Windows.' }
if (-not $NoShellHook) { Write-Host '  Open a new terminal so the sketch knows when a command fails.' }
Write-Host '  To remove it: .\install.ps1 -Uninstall'
""";

        private static string Readme(string sketchName, bool eachWindow, bool terminalProfile) => $"""
{sketchName} — un sketch de Dana que dibuja sobre Windows Terminal
{(eachWindow ? "Corre una copia del sketch en cada ventana de Windows Terminal." : "Corre sobre la ventana de Windows Terminal que está al frente.")}
{(terminalProfile ? $"Solo se ve en su propio perfil, \"{TerminalProfile.ProfileName(sketchName)}\" (translúcido): reinicia Windows Terminal después de instalar." : "")}

Instalar (para tu usuario, sin administrador):
  1. Clic derecho en install.ps1 → "Ejecutar con PowerShell"
     o en una terminal:  powershell -ExecutionPolicy Bypass -File .\install.ps1
  2. Abre Windows Terminal.

Opciones:  -NoAutostart  -NoShellHook  -Uninstall  (-Uninstall -RemoveShellHook)

Se instala en %LOCALAPPDATA%\DanaProcessing\Sketches\{SafeId(sketchName)}
y arranca con Windows (acceso directo en la carpeta Inicio).
Sin instalar también funciona: doble clic en el .exe.
""";

        private static string ReadmeEnglish(string sketchName, bool eachWindow, bool terminalProfile) => $"""
{sketchName} — a Dana sketch that draws on Windows Terminal
{(eachWindow ? "Runs a copy of the sketch on every Windows Terminal window." : "Runs on the Windows Terminal window in front.")}
{(terminalProfile ? $"It only shows in its own (see-through) profile, \"{TerminalProfile.ProfileName(sketchName)}\": restart Windows Terminal after installing." : "")}

Install (for your user, no administrator needed):
  1. Right click install.ps1 → "Run with PowerShell"
     or in a terminal:  powershell -ExecutionPolicy Bypass -File .\install.ps1
  2. Open Windows Terminal.

Options:  -NoAutostart  -NoShellHook  -Uninstall  (-Uninstall -RemoveShellHook)

It installs to %LOCALAPPDATA%\DanaProcessing\Sketches\{SafeId(sketchName)}
and starts with Windows (a shortcut in the Startup folder).
It also works without installing: double-click the .exe.
""";
    }
}
