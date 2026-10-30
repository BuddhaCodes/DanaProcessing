using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using DanaProcessing.Ide.Localization;

namespace DanaProcessing.Ide.Compilation.PackageManagement
{
    /// <summary>One resolved package's chosen version and the assembly files it
    /// contributes for the current target framework (empty for a metapackage
    /// that only carries transitive dependencies, e.g. no lib/ of its own).</summary>
    /// <summary>
    /// One resolved package: its managed assemblies (lib/, or the
    /// runtimes/&lt;rid&gt;/lib/ build for this OS when the package has one) and,
    /// when it ships native code for this OS, the folder those native
    /// libraries were extracted to (see SketchCompiler's native resolver).
    /// </summary>
    public sealed record ResolvedNuGetPackage(string Id, string Version, IReadOnlyList<string> AssemblyPaths, string? NativeDirectory = null);

    /// <summary>Everything a sketch's `// nuget:` directives resolved to: every
    /// assembly to add as a Roslyn reference and make loadable at runtime, plus
    /// any per-package failures (not found, no network, bad version) to show the
    /// user directly instead of a confusing downstream "type not found" error.</summary>
    public sealed record NuGetResolutionResult(IReadOnlyList<ResolvedNuGetPackage> Packages, IReadOnlyList<string> Errors)
    {
        public bool Success => Errors.Count == 0;
        public IEnumerable<string> AllAssemblyPaths => Packages.SelectMany(p => p.AssemblyPaths);
        public IEnumerable<string> AllNativeDirectories => Packages.Select(p => p.NativeDirectory).OfType<string>();
    }

    /// <summary>One nuget.org search hit — enough to render a Visual-Studio-style
    /// result row (title/id/author/downloads/short description) without yet
    /// committing to a specific version; see <see cref="NuGetPackageResolver.GetVersionsAsync"/>
    /// for that once the user actually selects a result.</summary>
    public sealed record PackageSearchResult(
        string Id, string LatestVersion, string? Title, string? Description, string? Authors, long? DownloadCount);

    /// <summary>
    /// Resolves `// nuget:` directives against nuget.org and extracts the
    /// matching net10.0 (or nearest compatible TFM — same resolution rule the
    /// SDK itself uses) assemblies into a local on-disk cache, so a sketch can
    /// reference third-party packages with neither a project file nor the .NET
    /// SDK installed. That second part matters: the published IDE is a
    /// self-contained single-file exe (see build-release.yml) precisely so it
    /// runs on a machine that only has the IDE, not `dotnet` — shelling out to
    /// `dotnet restore` would silently fail there, so this talks to the NuGet
    /// v3 API directly via the same client libraries the SDK itself uses.
    ///
    /// Deliberately simpler than a full `dotnet restore`: nuget.org plus one
    /// optional local folder feed (<see cref="LocalFeedDirectory"/>, checked
    /// first) and no nuget.config / private feeds, a simple "direct dependency
    /// wins, otherwise the highest version anyone asked for wins" rule instead
    /// of a real SAT-based dependency resolver, and a small denylist of
    /// netstandard2.0 compatibility-shim packages net10.0 already provides
    /// (downloading a differently-versioned copy of one of those would only
    /// risk shadowing the runtime's own). Good enough for the kind of single
    /// small utility package a sketch typically wants; not a general-purpose
    /// package manager.
    /// </summary>
    public static class NuGetPackageResolver
    {
        private const string SourceUrl = "https://api.nuget.org/v3/index.json";
        private static readonly NuGetFramework TargetFramework = NuGetFramework.ParseFolder("net10.0");
        private static readonly FrameworkReducer Reducer = new();
        private static readonly SourceRepository SourceRepo = Repository.Factory.GetCoreV3(SourceUrl);

        /// <summary>
        /// Carpeta con .nupkg propios (p. ej. KludgeLight.Core) que se consulta ANTES que
        /// nuget.org. Por defecto %AppData%/DanaProcessingIde/local-feed; la variable de
        /// entorno DANA_LOCAL_NUGET la sustituye. Si no existe, todo sigue como antes.
        /// </summary>
        public static string LocalFeedDirectory
        {
            get
            {
                var env = Environment.GetEnvironmentVariable("DANA_LOCAL_NUGET");
                return !string.IsNullOrWhiteSpace(env)
                    ? env
                    : Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "DanaProcessingIde", "local-feed");
            }
        }

        private static async Task<FindPackageByIdResource?> GetLocalFindResourceAsync(CancellationToken ct)
        {
            var dir = LocalFeedDirectory;
            if (!Directory.Exists(dir) || Directory.GetFiles(dir, "*.nupkg", SearchOption.AllDirectories).Length == 0)
                return null;
            try
            {
                return await Repository.Factory.GetCoreV3(dir).GetResourceAsync<FindPackageByIdResource>(ct);
            }
            catch
            {
                return null; // un feed local roto no debe impedir usar nuget.org
            }
        }

        /// <summary>
        /// Sufijo de caché para un paquete del feed local: la fecha del .nupkg. Así, al volver a
        /// empaquetar la misma versión (lo normal mientras se desarrolla) se extrae de nuevo en
        /// lugar de reutilizar la copia vieja.
        /// </summary>
        private static string LocalCacheSuffix(string id, NuGetVersion version)
        {
            var name = $"{id}.{version.ToNormalizedString()}.nupkg";
            var file = Directory.GetFiles(LocalFeedDirectory, "*.nupkg", SearchOption.AllDirectories)
                .FirstOrDefault(f => string.Equals(Path.GetFileName(f), name, StringComparison.OrdinalIgnoreCase));
            return file is null ? "-local" : $"-local-{File.GetLastWriteTimeUtc(file).Ticks}";
        }

        /// <summary>
        /// Runtime identifiers to look under runtimes/ for, most specific first
        /// (the usual RID fallback: win-x64 -> win, linux-x64 -> linux -> unix,
        /// osx-arm64 -> osx -> unix). A package's runtimes/&lt;rid&gt;/lib build
        /// replaces its lib/ one (that's how System.IO.Ports ships its real
        /// implementation), and runtimes/&lt;rid&gt;/native holds its native
        /// libraries (ONNX Runtime, SQLite, OpenCV...).
        /// </summary>
        private static readonly string[] RidChain = BuildRidChain();

        private static string[] BuildRidChain()
        {
            string arch = RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.X86 => "x86",
                Architecture.Arm64 => "arm64",
                Architecture.Arm => "arm",
                _ => "x64",
            };
            if (OperatingSystem.IsWindows())
                return new[] { $"win-{arch}", "win" };
            if (OperatingSystem.IsMacOS())
                return new[] { $"osx-{arch}", "osx", "unix" };
            if (OperatingSystem.IsLinux())
                return new[] { $"linux-{arch}", "linux", "unix" };
            return Array.Empty<string>();
        }

        private static readonly HashSet<string> SkipIds = new(StringComparer.OrdinalIgnoreCase)
        {
            "NETStandard.Library",
            "Microsoft.NETCore.Platforms",
            "Microsoft.NETCore.Targets",
            "Microsoft.NETFramework.ReferenceAssemblies",
            "System.Buffers",
            "System.Memory",
            "System.Numerics.Vectors",
            "System.Runtime.CompilerServices.Unsafe",
            "System.Threading.Tasks.Extensions",
            "System.ValueTuple",
        };

        private static string CacheRoot
        {
            get
            {
                var dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "DanaProcessingIde", "nuget-packages");
                Directory.CreateDirectory(dir);
                return dir;
            }
        }

        public static async Task<NuGetResolutionResult> ResolveAsync(
            IReadOnlyList<PackageDirective> directives,
            IProgress<string>? progress = null,
            CancellationToken ct = default)
        {
            var packages = new List<ResolvedNuGetPackage>();
            var errors = new List<string>();
            // id -> version currently chosen, and where its entry sits in `packages`.
            var chosen = new Dictionary<string, (NuGetVersion Version, int Index)>(StringComparer.OrdinalIgnoreCase);
            // The sketch's own directives: what the user wrote is never overridden.
            var direct = new HashSet<string>(directives.Select(d => d.Id), StringComparer.OrdinalIgnoreCase);
            // Ids that already failed once: reported once, not again for every package depending on them.
            var failed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            using var cache = new SourceCacheContext();
            var logger = NullLogger.Instance;
            var findResource = await SourceRepo.GetResourceAsync<FindPackageByIdResource>(ct);
            if (findResource is null)
            {
                errors.Add(Loc.Tr("No se pudo inicializar el cliente de NuGet.org.", "Could not initialize the NuGet.org client."));
                return new NuGetResolutionResult(packages, errors);
            }

            var localFind = await GetLocalFindResourceAsync(ct);

            var queue = new Queue<(string Id, string? VersionSpec)>();
            foreach (var d in directives)
                queue.Enqueue((d.Id, d.Version));

            while (queue.Count > 0)
            {
                var (id, versionSpec) = queue.Dequeue();
                if (SkipIds.Contains(id) || failed.Contains(id))
                    continue;

                // Already have this package: keep it unless a dependency needs a
                // newer one than we picked (two packages sharing a dependency at
                // different minimums -- taking the first one seen could hand the
                // other a version that's missing methods it calls).
                bool upgrading = chosen.TryGetValue(id, out var current);
                if (upgrading && (direct.Contains(id) || SatisfiedBy(versionSpec, current.Version)))
                    continue;

                if (!upgrading)
                    progress?.Report(Loc.Tr($"Resolviendo {id}{(versionSpec is null ? "" : " " + versionSpec)}...", $"Resolving {id}{(versionSpec is null ? "" : " " + versionSpec)}..."));

                NuGetVersion? version = null;
                var source = findResource;
                string cacheSuffix = "";

                // Primero el feed local: los paquetes propios no están en nuget.org.
                if (localFind != null)
                {
                    try
                    {
                        var localVersion = await ResolveVersionAsync(localFind, cache, logger, id, versionSpec, ct);
                        if (localVersion != null && (await localFind.DoesPackageExistAsync(id, localVersion, cache, logger, ct)))
                        {
                            version = localVersion;
                            source = localFind;
                            cacheSuffix = LocalCacheSuffix(id, localVersion);
                        }
                    }
                    catch
                    {
                        // no está en el feed local: se busca en nuget.org
                    }
                }

                try
                {
                    version ??= await ResolveVersionAsync(findResource, cache, logger, id, versionSpec, ct);
                }
                catch (Exception ex)
                {
                    if (!upgrading) failed.Add(id);
                    errors.Add(Loc.Tr($"No se pudo conectar a NuGet.org para resolver '{id}': {ex.Message}", $"Could not connect to NuGet.org to resolve '{id}': {ex.Message}"));
                    continue;
                }

                if (version is null)
                {
                    if (upgrading)
                        continue; // keep the one we have; the mismatch may still work at runtime
                    failed.Add(id);
                    errors.Add(versionSpec is null
                        ? Loc.Tr($"No se encontro el paquete '{id}' en NuGet.org.", $"Package '{id}' was not found on NuGet.org.")
                        : Loc.Tr($"No se encontro una version de '{id}' que satisfaga '{versionSpec}' en NuGet.org.", $"No version of '{id}' satisfying '{versionSpec}' was found on NuGet.org."));
                    continue;
                }

                if (upgrading && version <= current.Version)
                    continue;

                try
                {
                    var (assemblyPaths, dependencies, nativeDirectory) = await GetOrDownloadAsync(source, cache, logger, id, version, progress, ct, cacheSuffix);
                    var entry = new ResolvedNuGetPackage(id, version.ToNormalizedString(), assemblyPaths, nativeDirectory);
                    if (upgrading)
                    {
                        packages[current.Index] = entry;
                        chosen[id] = (version, current.Index);
                    }
                    else
                    {
                        chosen[id] = (version, packages.Count);
                        packages.Add(entry);
                    }
                    foreach (var dep in dependencies)
                        queue.Enqueue((dep.Id, dep.VersionRange?.OriginalString));
                }
                catch (Exception ex)
                {
                    if (!upgrading) failed.Add(id);
                    errors.Add(Loc.Tr($"No se pudo descargar '{id}' {version.ToNormalizedString()}: {ex.Message}", $"Could not download '{id}' {version.ToNormalizedString()}: {ex.Message}"));
                }
            }

            return new NuGetResolutionResult(packages, errors);
        }

        /// <summary>Whether <paramref name="version"/> meets a dependency's version
        /// requirement (NuGet range syntax; a bare "1.2" means "1.2 or newer"; none
        /// means any version).</summary>
        internal static bool SatisfiedBy(string? versionSpec, NuGetVersion version) =>
            string.IsNullOrWhiteSpace(versionSpec)
            || !VersionRange.TryParse(versionSpec, out var range)
            || range.Satisfies(version);

        /// <summary>
        /// <paramref name="root"/> joined with a path that came from inside a
        /// downloaded package, refusing anything that would land outside
        /// <paramref name="root"/> ("../../.bashrc", an absolute path...). A
        /// .nupkg is just a zip someone else built: its entry names are data,
        /// not something to hand straight to the file system.
        /// </summary>
        internal static string CombineInside(string root, string relative)
        {
            var fullRoot = Path.GetFullPath(root);
            var full = Path.GetFullPath(Path.Combine(fullRoot, relative));
            var rootWithSep = fullRoot.EndsWith(Path.DirectorySeparatorChar) ? fullRoot : fullRoot + Path.DirectorySeparatorChar;
            var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (Path.IsPathRooted(relative) || !full.StartsWith(rootWithSep, comparison))
                throw new InvalidDataException(Loc.Tr(
                    $"El paquete contiene una ruta no permitida: '{relative}'.",
                    $"The package contains a disallowed path: '{relative}'."));
            return full;
        }

        /// <summary>Searches nuget.org the same way Visual Studio's "Buscar" tab
        /// does — a free-text query against the search endpoint, not an exact
        /// package id lookup. Stable releases only (no prerelease noise in a
        /// browse list a sketch author is casually scrolling through).</summary>
        public static async Task<IReadOnlyList<PackageSearchResult>> SearchAsync(
            string query, int take = 25, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(query))
                return Array.Empty<PackageSearchResult>();

            var searchResource = await SourceRepo.GetResourceAsync<PackageSearchResource>(ct);
            if (searchResource is null)
                return Array.Empty<PackageSearchResult>();

            var filter = new SearchFilter(includePrerelease: false);
            var results = await searchResource.SearchAsync(query, filter, skip: 0, take: take, NullLogger.Instance, ct);

            return results
                .Select(r => new PackageSearchResult(
                    r.Identity.Id,
                    r.Identity.Version.ToNormalizedString(),
                    r.Title,
                    string.IsNullOrWhiteSpace(r.Description) ? r.Summary : r.Description,
                    r.Authors,
                    r.DownloadCount))
                .ToList();
        }

        /// <summary>Every published version of <paramref name="id"/>, newest first —
        /// powers the version picker in the package details pane. Includes
        /// prerelease versions (unlike SearchAsync): once the user has committed to
        /// a specific package, hiding a legitimate prerelease they might actually
        /// want would be more surprising than useful.</summary>
        public static async Task<IReadOnlyList<string>> GetVersionsAsync(string id, CancellationToken ct = default)
        {
            using var cache = new SourceCacheContext();
            var findResource = await SourceRepo.GetResourceAsync<FindPackageByIdResource>(ct);
            if (findResource is null)
                return Array.Empty<string>();

            var versions = await findResource.GetAllVersionsAsync(id, cache, NullLogger.Instance, ct);
            return versions
                .OrderByDescending(v => v)
                .Select(v => v.ToNormalizedString())
                .ToList();
        }

        private static async Task<NuGetVersion?> ResolveVersionAsync(
            FindPackageByIdResource findResource, SourceCacheContext cache, ILogger logger,
            string id, string? versionSpec, CancellationToken ct)
        {
            if (versionSpec != null && NuGetVersion.TryParse(versionSpec, out var exact))
                return exact;

            var all = (await findResource.GetAllVersionsAsync(id, cache, logger, ct)).ToList();
            if (all.Count == 0)
                return null;

            if (versionSpec != null)
            {
                return VersionRange.TryParse(versionSpec, out var range)
                    ? range.FindBestMatch(all)
                    : null;
            }

            // No version pinned: latest stable, or latest prerelease if that's
            // genuinely all this package has ever published.
            return all.Where(v => !v.IsPrerelease).OrderByDescending(v => v).FirstOrDefault()
                ?? all.OrderByDescending(v => v).First();
        }

        /// <summary>Serves an already-extracted package straight from
        /// <see cref="CacheRoot"/> when present (no network at all), otherwise
        /// downloads the .nupkg, extracts its net10.0-nearest lib assemblies and
        /// records its net10.0-nearest dependency group for the caller to enqueue.</summary>
        private static async Task<(IReadOnlyList<string> AssemblyPaths, IReadOnlyList<PackageDependency> Dependencies, string? NativeDirectory)> GetOrDownloadAsync(
            FindPackageByIdResource findResource, SourceCacheContext cache, ILogger logger,
            string id, NuGetVersion version, IProgress<string>? progress, CancellationToken ct, string cacheSuffix = "")
        {
            var packageDir = CombineInside(CacheRoot, Path.Combine(id.ToLowerInvariant(), version.ToNormalizedString() + cacheSuffix));
            var libDir = Path.Combine(packageDir, "lib");
            var nativeDir = Path.Combine(packageDir, "native");
            // "-v2": packages cached before runtimes/ support had no native/ folder
            // and only the lib/ (possibly "not supported") build -- a new marker
            // name makes each of them extract once more, the right way.
            var completeMarker = Path.Combine(packageDir, ".complete-v2");
            var depsFile = Path.Combine(packageDir, "deps.txt");

            if (File.Exists(completeMarker))
            {
                var cachedAssemblies = Directory.Exists(libDir) ? Directory.GetFiles(libDir, "*.dll") : Array.Empty<string>();
                var cachedDeps = File.Exists(depsFile)
                    ? File.ReadAllLines(depsFile).Select(ParseDepLine).Where(d => d != null).Select(d => d!).ToList()
                    : new List<PackageDependency>();
                return (cachedAssemblies, cachedDeps, Directory.Exists(nativeDir) ? nativeDir : null);
            }

            progress?.Report(Loc.Tr($"Descargando {id} {version.ToNormalizedString()}...", $"Downloading {id} {version.ToNormalizedString()}..."));

            Directory.CreateDirectory(packageDir);
            var nupkgPath = Path.Combine(packageDir, $"{id}.{version.ToNormalizedString()}.nupkg");

            using (var fileStream = File.Create(nupkgPath))
            {
                var ok = await findResource.CopyNupkgToStreamAsync(id, version, fileStream, cache, logger, ct);
                if (!ok)
                    throw new InvalidOperationException(Loc.Tr("nuget.org no devolvio el paquete.", "nuget.org did not return the package."));
            }

            List<string> extracted;
            List<PackageDependency> dependencies;
            bool hasNative;
            using (var packageStream = File.OpenRead(nupkgPath))
            using (var reader = new PackageArchiveReader(packageStream))
            {
                var nuspec = await reader.GetNuspecReaderAsync(ct);
                var libGroups = (await reader.GetLibItemsAsync(ct)).ToList();
                var nearestLib = Reducer.GetNearest(TargetFramework, libGroups.Select(g => g.TargetFramework));
                var chosenLibGroup = libGroups.FirstOrDefault(g => g.TargetFramework.Equals(nearestLib));

                extracted = new List<string>();
                if (chosenLibGroup != null)
                {
                    Directory.CreateDirectory(libDir);
                    // Satellite culture resource assemblies (lib/<tfm>/<culture>/X.resources.dll)
                    // always carry the same simple name as every OTHER culture's copy of the same
                    // package's resources — fine for the .NET satellite-assembly-probing convention
                    // they were designed for, but useless (and actively colliding) as a plain
                    // simple-name -> path map like SketchCompiler.SetNuGetAssemblies keeps. A
                    // sketch's own code never references a resource-only assembly by type anyway,
                    // so skipping them costs nothing it could have used.
                    foreach (var item in chosenLibGroup.Items.Where(IsLoadableAssembly))
                    {
                        var destPath = Path.Combine(libDir, Path.GetFileName(item));
                        using var entryStream = reader.GetStream(item);
                        using var destStream = File.Create(destPath);
                        await entryStream.CopyToAsync(destStream, ct);
                        extracted.Add(destPath);
                    }
                }

                var allFiles = reader.GetFiles().ToList();
                ExtractRuntimeAssemblies(reader, allFiles, libDir, extracted);
                hasNative = ExtractNatives(reader, allFiles, nativeDir);

                var depGroups = nuspec.GetDependencyGroups().ToList();
                var nearestDeps = Reducer.GetNearest(TargetFramework, depGroups.Select(g => g.TargetFramework));
                var chosenDepGroup = depGroups.FirstOrDefault(g => g.TargetFramework.Equals(nearestDeps));
                dependencies = chosenDepGroup?.Packages.ToList() ?? new List<PackageDependency>();
            }

            // Only the extracted lib/ + the recorded dependency list need to
            // survive between runs — the raw .nupkg itself was just a means to
            // get there, and keeping it around would just double the cache's
            // disk usage for no benefit.
            File.Delete(nupkgPath);
            File.WriteAllLines(depsFile, dependencies.Select(d => $"{d.Id}|{d.VersionRange?.OriginalString}"));
            File.WriteAllText(completeMarker, "");

            return (extracted, dependencies, hasNative ? nativeDir : null);
        }

        /// <summary>
        /// runtimes/&lt;rid&gt;/lib/&lt;tfm&gt;/*.dll for the most specific RID that has
        /// any: these REPLACE same-named lib/ assemblies, which in packages like
        /// System.IO.Ports are only "not supported on this platform" stubs.
        /// </summary>
        private static void ExtractRuntimeAssemblies(PackageArchiveReader reader, List<string> allFiles, string libDir, List<string> extracted)
        {
            foreach (var rid in RidChain)
            {
                string prefix = $"runtimes/{rid}/lib/";
                var items = allFiles
                    .Where(f => f.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && IsLoadableAssembly(f))
                    .ToList();
                if (items.Count == 0)
                    continue;

                // tfm folder name -> parsed framework, so the folder actually chosen can be matched back.
                var folders = items
                    .Select(f => f.Substring(prefix.Length).Split('/')[0])
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(folder => folder, NuGetFramework.ParseFolder, StringComparer.OrdinalIgnoreCase);
                var nearest = Reducer.GetNearest(TargetFramework, folders.Values);
                if (nearest is null)
                    continue;
                string chosenFolder = folders.First(kv => kv.Value.Equals(nearest)).Key;
                string chosenPrefix = prefix + chosenFolder + "/";

                Directory.CreateDirectory(libDir);
                foreach (var item in items.Where(f => f.StartsWith(chosenPrefix, StringComparison.OrdinalIgnoreCase)
                                                   && f.IndexOf('/', chosenPrefix.Length) < 0)) // no culture subfolders
                {
                    var destPath = Path.Combine(libDir, Path.GetFileName(item));
                    using (var entryStream = reader.GetStream(item))
                    using (var destStream = File.Create(destPath))
                        entryStream.CopyTo(destStream);
                    extracted.RemoveAll(p => string.Equals(Path.GetFileName(p), Path.GetFileName(item), StringComparison.OrdinalIgnoreCase));
                    extracted.Add(destPath);
                }
                return;
            }
        }

        /// <summary>runtimes/&lt;rid&gt;/native/* for the most specific RID that has any, into <paramref name="nativeDir"/>.</summary>
        private static bool ExtractNatives(PackageArchiveReader reader, List<string> allFiles, string nativeDir)
        {
            foreach (var rid in RidChain)
            {
                string prefix = $"runtimes/{rid}/native/";
                var items = allFiles
                    .Where(f => f.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && !f.EndsWith("/", StringComparison.Ordinal))
                    .ToList();
                if (items.Count == 0)
                    continue;

                foreach (var item in items)
                {
                    var relative = item.Substring(prefix.Length).Replace('/', Path.DirectorySeparatorChar);
                    var destPath = CombineInside(nativeDir, relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
                    using var entryStream = reader.GetStream(item);
                    using var destStream = File.Create(destPath);
                    entryStream.CopyTo(destStream);
                }
                return true;
            }
            return false;
        }

        private static bool IsLoadableAssembly(string path) =>
            path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) &&
            !path.EndsWith(".resources.dll", StringComparison.OrdinalIgnoreCase);

        private static PackageDependency? ParseDepLine(string line)
        {
            var parts = line.Split('|', 2);
            if (parts.Length != 2 || parts[0].Length == 0)
                return null;
            return new PackageDependency(parts[0], VersionRange.TryParse(parts[1], out var r) ? r : null);
        }
    }
}
