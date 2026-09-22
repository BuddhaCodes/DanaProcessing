using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using DanaProcessing.Ide.Compilation.PackageManagement;

namespace DanaProcessing.Ide.Export
{
    public sealed record ExportResult(bool Success, string? OutputFolder, IReadOnlyList<string> Errors);

    /// <summary>
    /// Packages a sketch as a standalone, double-clickable app, plus the sketch's
    /// source dropped in as a plain `sketch.cs` file next to the exe, plus any
    /// `// nuget:` packages the sketch declares, resolved once here and copied in
    /// as .dll files. See ExportedSketchRunner for what the exe does differently
    /// on startup when it finds that file sitting next to itself.
    ///
    /// Two ways to get that standalone build, tried in order:
    ///
    ///  1. `dotnet publish -r <rid> --self-contained --self-contained-single-file`
    ///     against this IDE's own .csproj, when it can find one — a genuine
    ///     single .exe. Only possible when running from a source checkout with
    ///     the .NET SDK on PATH (FindOwnProjectFile walks up from
    ///     AppContext.BaseDirectory looking for the standard bin/&lt;config&gt;/&lt;tfm&gt;/
    ///     shape `dotnet build`/`dotnet run` produce); a machine that only has
    ///     the distributed IDE binaries has no .csproj to publish, so this step
    ///     is skipped there, not attempted and failed.
    ///  2. Otherwise, copy this IDE's own currently-running install directory
    ///     wholesale (trimmed of files nothing at runtime needs — see
    ///     CopyDirectory) — many files instead of one, but needs nothing beyond
    ///     what's already running, so it always works as a fallback.
    /// </summary>
    public static class SketchExporter
    {
        public static async Task<ExportResult> ExportAsync(
            string source,
            string destinationParentFolder,
            string sketchName,
            IProgress<string>? progress = null,
            CancellationToken ct = default)
        {
            var installDir = AppContext.BaseDirectory;
            var exportFolder = Path.Combine(destinationParentFolder, SanitizeFolderName(sketchName));

            var fullExportPath = Path.GetFullPath(exportFolder);
            var fullInstallPath = Path.GetFullPath(installDir);
            if (fullExportPath.Equals(fullInstallPath, StringComparison.OrdinalIgnoreCase) ||
                fullExportPath.StartsWith(fullInstallPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                return new ExportResult(false, null,
                    new[] { "La carpeta de destino no puede estar dentro de la instalacion del IDE." });
            }

            var extraAssemblyPaths = new List<string>();
            var directives = PackageDirectiveParser.Parse(source);
            if (directives.Count > 0)
            {
                progress?.Report("Resolviendo paquetes NuGet...");
                var resolution = await NuGetPackageResolver.ResolveAsync(directives, progress, ct);
                if (!resolution.Success)
                    return new ExportResult(false, null, resolution.Errors);
                extraAssemblyPaths.AddRange(resolution.AllAssemblyPaths);
            }

            try
            {
                Directory.CreateDirectory(exportFolder);

                var published = false;
                var projectPath = FindOwnProjectFile();
                var rid = GetCurrentRid();
                if (projectPath != null && rid != null)
                {
                    progress?.Report("Compilando un build standalone de un solo archivo (puede tardar)...");
                    published = await TryPublishAsync(projectPath, exportFolder, rid, ct);
                    if (!published)
                    {
                        // A half-finished publish can leave stray files that
                        // don't belong in the plain-copy fallback below (it
                        // only overwrites what it actually knows about).
                        foreach (var leftover in Directory.GetFileSystemEntries(exportFolder))
                            DeleteEntry(leftover);
                        progress?.Report("No se pudo compilar un build de un solo archivo — copiando la instalacion actual en su lugar.");
                    }
                }

                if (!published)
                {
                    progress?.Report("Copiando el runtime...");
                    CopyDirectory(installDir, exportFolder, ct);
                }
                else
                {
                    // -p:DebugType=none above only covers OUR OWN assemblies'
                    // symbols -- a native dependency's .pdb bundled by its own
                    // NuGet package (SkiaSharp's libSkiaSharp.pdb alone was 81MB
                    // in testing, next to a ~140MB exe) rides along as a content
                    // item regardless of that flag. Sweep whatever's left the
                    // same way the copy-based path already excludes them.
                    foreach (var pdb in Directory.GetFiles(exportFolder, "*.pdb"))
                        DeleteEntry(pdb);
                }

                if (extraAssemblyPaths.Count > 0)
                {
                    progress?.Report("Copiando dependencias...");
                    foreach (var path in extraAssemblyPaths)
                        File.Copy(path, Path.Combine(exportFolder, Path.GetFileName(path)), overwrite: true);
                }

                progress?.Report("Escribiendo el sketch...");
                await File.WriteAllTextAsync(Path.Combine(exportFolder, ExportedSketchRunner.MarkerFileName), source, ct);
            }
            catch (Exception ex)
            {
                return new ExportResult(false, null, new[] { $"Error exportando: {ex.Message}" });
            }

            return new ExportResult(true, exportFolder, Array.Empty<string>());
        }

        /// <summary>Walks up from AppContext.BaseDirectory looking for
        /// DanaProcessing.Ide.csproj — present when running from a source
        /// checkout (`dotnet build`/`dotnet run`'s bin/&lt;config&gt;/&lt;tfm&gt;/ output
        /// sits exactly 3 levels under the project folder), absent for a
        /// standalone distributed copy of the IDE, which ships only compiled
        /// output. A handful of extra levels of slack covers a RID-specific
        /// build folder too, without walking arbitrarily far up the disk.</summary>
        private static string? FindOwnProjectFile()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            for (var i = 0; i < 6 && dir != null; i++)
            {
                var candidate = Path.Combine(dir.FullName, "DanaProcessing.Ide.csproj");
                if (File.Exists(candidate))
                    return candidate;
                dir = dir.Parent;
            }
            return null;
        }

        private static string? GetCurrentRid()
        {
            if (!OperatingSystem.IsWindows())
                return null; // this app only ships/runs on Windows today

            return RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.X64 => "win-x64",
                Architecture.X86 => "win-x86",
                Architecture.Arm64 => "win-arm64",
                _ => null,
            };
        }

        /// <summary>
        /// Same flags build-release.yml already publishes the IDE itself with
        /// (self-contained, single-file, native libs folded into that one file
        /// too) — just pointed at this export folder instead of a CI artifact.
        /// Deliberately no -p:PublishTrimmed: SketchCompiler/RoslynCompletionEngine
        /// load Roslyn types by reflection, which the trimmer can't see are used
        /// and would cut — that's the same reasoning build-release.yml documents
        /// for the IDE's own release build, and it applies identically here since
        /// the exported exe still compiles the sketch via that same Roslyn path.
        /// </summary>
        private static async Task<bool> TryPublishAsync(string projectPath, string exportFolder, string rid, CancellationToken ct)
        {
            var psi = new ProcessStartInfo("dotnet")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            psi.ArgumentList.Add("publish");
            psi.ArgumentList.Add(projectPath);
            psi.ArgumentList.Add("-c");
            psi.ArgumentList.Add("Release");
            psi.ArgumentList.Add("-r");
            psi.ArgumentList.Add(rid);
            psi.ArgumentList.Add("--self-contained");
            psi.ArgumentList.Add("true");
            psi.ArgumentList.Add("-p:PublishSingleFile=true");
            // NOT just IncludeNativeLibrariesForSelfExtract: this app compiles
            // the sketch via Roslyn at startup, and Roslyn needs real on-disk
            // files for MetadataReference.CreateFromFile (both the BCL's own
            // assemblies and DanaProcessing.dll itself, for Sketch/PVector/etc.).
            // A plain single-file bundle keeps everything in-memory inside the
            // exe -- Assembly.Location comes back "" and TRUSTED_PLATFORM_ASSEMBLIES
            // is a single empty entry, which crashed SketchCompiler outright in
            // testing. IncludeAllContentForSelfExtract=true makes the exe unpack
            // everything to a real (content-hash-cached, so only slow on the very
            // first launch) temp folder before running, which restores both to
            // proper file paths. See ExportedSketchRunner for the flip side of
            // this: BaseDirectory now points at that temp cache, not at the exe's
            // own folder, so `sketch.cs` has to be found via Environment.ProcessPath instead.
            psi.ArgumentList.Add("-p:IncludeAllContentForSelfExtract=true");
            psi.ArgumentList.Add("-p:DebugType=none");
            psi.ArgumentList.Add("-o");
            psi.ArgumentList.Add(exportFolder);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromMinutes(5));

            try
            {
                using var process = new Process { StartInfo = psi };
                process.Start();

                var stderrTask = process.StandardError.ReadToEndAsync(timeoutCts.Token);
                var stdoutTask = process.StandardOutput.ReadToEndAsync(timeoutCts.Token);
                await process.WaitForExitAsync(timeoutCts.Token);

                return process.ExitCode == 0;
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                return false; // our own 5-minute timeout, not the caller's cancellation
            }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or FileNotFoundException)
            {
                return false; // "dotnet" isn't on PATH -- no SDK installed
            }
        }

        private static void DeleteEntry(string path)
        {
            try
            {
                if (Directory.Exists(path))
                    Directory.Delete(path, recursive: true);
                else if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
                // Best-effort cleanup before the copy fallback overwrites what
                // it recognizes anyway -- a stray locked file here isn't worth
                // failing the whole export over.
            }
        }

        // "ref" and "refint" hold reference assemblies -- stripped-down stand-ins
        // other PROJECTS compile against, generated by `dotnet build` for that
        // purpose only. Nothing at runtime ever loads them (RegisterAssemblyDirectory
        // and CollectExternalReferences both work off the real assemblies sitting
        // in the install directory itself, same as GetSharedReferences' use of
        // TRUSTED_PLATFORM_ASSEMBLIES) -- carrying them into an export would just
        // be dead weight nobody's compiling against.
        private static readonly HashSet<string> SkipDirectoryNames =
            new(StringComparer.OrdinalIgnoreCase) { "ref", "refint" };

        private static void CopyDirectory(string sourceDir, string destDir, CancellationToken ct)
        {
            foreach (var file in Directory.GetFiles(sourceDir))
            {
                ct.ThrowIfCancellationRequested();

                var ext = Path.GetExtension(file);

                // Debug symbols: dead weight in something meant to be handed to
                // someone else, and no use to them without the matching source.
                if (ext.Equals(".pdb", StringComparison.OrdinalIgnoreCase))
                    continue;

                // IntelliSense doc comments for every assembly, including ones
                // several MB by itself for something like the BCL or Roslyn --
                // read by an IDE's completion engine, never by a compiled app.
                if (ext.Equals(".xml", StringComparison.OrdinalIgnoreCase))
                    continue;

                File.Copy(file, Path.Combine(destDir, Path.GetFileName(file)), overwrite: true);
            }

            // "runtimes/<rid>/native/..." ships one copy of every native
            // dependency (SkiaSharp, Avalonia's GL loader, etc.) for EVERY
            // platform those packages support -- win-x86/x64/arm64,
            // linux-x64/arm/arm64/musl-*/riscv64/loongarch64, osx, and more.
            // A framework-dependent `dotnet build` output (no -r flag) keeps
            // the whole set so the deps.json runtime resolver can pick the
            // right one wherever it ends up running; a real .NET publish for
            // a specific RID would already prune this automatically, but a
            // plain build never does. This was ~238MB of a ~299MB export in
            // testing — by far the single biggest win available here, and
            // this app only ever runs on Windows anyway (see the
            // OperatingSystem.IsWindows() guards throughout Program.cs).
            var isRuntimesFolder = Path.GetFileName(sourceDir).Equals("runtimes", StringComparison.OrdinalIgnoreCase);

            foreach (var subDir in Directory.GetDirectories(sourceDir))
            {
                var subDirName = Path.GetFileName(subDir);

                if (SkipDirectoryNames.Contains(subDirName))
                    continue;
                if (isRuntimesFolder && !subDirName.StartsWith("win", StringComparison.OrdinalIgnoreCase))
                    continue;

                var destSubDir = Path.Combine(destDir, subDirName);
                Directory.CreateDirectory(destSubDir);
                CopyDirectory(subDir, destSubDir, ct);
            }
        }

        private static string SanitizeFolderName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var cleaned = new string(name.Where(c => !invalid.Contains(c)).ToArray()).Trim();
            return cleaned.Length == 0 ? "MiSketch" : cleaned;
        }
    }
}
