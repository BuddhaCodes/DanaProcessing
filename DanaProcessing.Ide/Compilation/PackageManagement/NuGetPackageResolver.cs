using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace DanaProcessing.Ide.Compilation.PackageManagement
{
    /// <summary>One resolved package's chosen version and the assembly files it
    /// contributes for the current target framework (empty for a metapackage
    /// that only carries transitive dependencies, e.g. no lib/ of its own).</summary>
    public sealed record ResolvedNuGetPackage(string Id, string Version, IReadOnlyList<string> AssemblyPaths);

    /// <summary>Everything a sketch's `// nuget:` directives resolved to: every
    /// assembly to add as a Roslyn reference and make loadable at runtime, plus
    /// any per-package failures (not found, no network, bad version) to show the
    /// user directly instead of a confusing downstream "type not found" error.</summary>
    public sealed record NuGetResolutionResult(IReadOnlyList<ResolvedNuGetPackage> Packages, IReadOnlyList<string> Errors)
    {
        public bool Success => Errors.Count == 0;
        public IEnumerable<string> AllAssemblyPaths => Packages.SelectMany(p => p.AssemblyPaths);
    }

    /// <summary>One nuget.org search hit — enough to render a Visual-Studio-style
    /// result row (title/id/author/downloads/short description) without yet
    /// committing to a specific version; see <see cref="NuGetPackageResolver.GetVersionsAsync"/>
    /// for that once the user actually selects a result.</summary>
    public sealed record PackageSearchResult(
        string Id, string LatestVersion, string? Title, string? Description, string? Authors, long? DownloadCount);

    /// <summary>
    /// Resolves `// nuget:` directives against nuget.org and extracts the
    /// matching net8.0 (or nearest compatible TFM — same resolution rule the
    /// SDK itself uses) assemblies into a local on-disk cache, so a sketch can
    /// reference third-party packages with neither a project file nor the .NET
    /// SDK installed. That second part matters: the published IDE is a
    /// self-contained single-file exe (see build-release.yml) precisely so it
    /// runs on a machine that only has the IDE, not `dotnet` — shelling out to
    /// `dotnet restore` would silently fail there, so this talks to the NuGet
    /// v3 API directly via the same client libraries the SDK itself uses.
    ///
    /// Deliberately simpler than a full `dotnet restore`: nuget.org only (no
    /// nuget.config / private feeds), "first-resolved version wins" instead of
    /// a real SAT-based dependency resolver, and a small denylist of
    /// netstandard2.0 compatibility-shim packages net8.0 already provides
    /// (downloading a differently-versioned copy of one of those would only
    /// risk shadowing the runtime's own). Good enough for the kind of single
    /// small utility package a sketch typically wants; not a general-purpose
    /// package manager.
    /// </summary>
    public static class NuGetPackageResolver
    {
        private const string SourceUrl = "https://api.nuget.org/v3/index.json";
        private static readonly NuGetFramework TargetFramework = NuGetFramework.ParseFolder("net8.0");
        private static readonly FrameworkReducer Reducer = new();
        private static readonly SourceRepository SourceRepo = Repository.Factory.GetCoreV3(SourceUrl);

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
            var resolved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            using var cache = new SourceCacheContext();
            var logger = NullLogger.Instance;
            var findResource = await SourceRepo.GetResourceAsync<FindPackageByIdResource>(ct);
            if (findResource is null)
            {
                errors.Add("No se pudo inicializar el cliente de NuGet.org.");
                return new NuGetResolutionResult(packages, errors);
            }

            var queue = new Queue<(string Id, string? VersionSpec)>();
            foreach (var d in directives)
                queue.Enqueue((d.Id, d.Version));

            while (queue.Count > 0)
            {
                var (id, versionSpec) = queue.Dequeue();
                if (SkipIds.Contains(id) || !resolved.Add(id))
                    continue;

                progress?.Report($"Resolviendo {id}{(versionSpec is null ? "" : " " + versionSpec)}...");

                NuGetVersion? version;
                try
                {
                    version = await ResolveVersionAsync(findResource, cache, logger, id, versionSpec, ct);
                }
                catch (Exception ex)
                {
                    errors.Add($"No se pudo conectar a NuGet.org para resolver '{id}': {ex.Message}");
                    continue;
                }

                if (version is null)
                {
                    errors.Add(versionSpec is null
                        ? $"No se encontro el paquete '{id}' en NuGet.org."
                        : $"No se encontro una version de '{id}' que satisfaga '{versionSpec}' en NuGet.org.");
                    continue;
                }

                try
                {
                    var (assemblyPaths, dependencies) = await GetOrDownloadAsync(findResource, cache, logger, id, version, progress, ct);
                    packages.Add(new ResolvedNuGetPackage(id, version.ToNormalizedString(), assemblyPaths));
                    foreach (var dep in dependencies)
                        queue.Enqueue((dep.Id, dep.VersionRange?.OriginalString));
                }
                catch (Exception ex)
                {
                    errors.Add($"No se pudo descargar '{id}' {version.ToNormalizedString()}: {ex.Message}");
                }
            }

            return new NuGetResolutionResult(packages, errors);
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
        /// downloads the .nupkg, extracts its net8.0-nearest lib assemblies and
        /// records its net8.0-nearest dependency group for the caller to enqueue.</summary>
        private static async Task<(IReadOnlyList<string> AssemblyPaths, IReadOnlyList<PackageDependency> Dependencies)> GetOrDownloadAsync(
            FindPackageByIdResource findResource, SourceCacheContext cache, ILogger logger,
            string id, NuGetVersion version, IProgress<string>? progress, CancellationToken ct)
        {
            var packageDir = Path.Combine(CacheRoot, id.ToLowerInvariant(), version.ToNormalizedString());
            var libDir = Path.Combine(packageDir, "lib");
            var completeMarker = Path.Combine(packageDir, ".complete");
            var depsFile = Path.Combine(packageDir, "deps.txt");

            if (File.Exists(completeMarker))
            {
                var cachedAssemblies = Directory.Exists(libDir) ? Directory.GetFiles(libDir, "*.dll") : Array.Empty<string>();
                var cachedDeps = File.Exists(depsFile)
                    ? File.ReadAllLines(depsFile).Select(ParseDepLine).Where(d => d != null).Select(d => d!).ToList()
                    : new List<PackageDependency>();
                return (cachedAssemblies, cachedDeps);
            }

            progress?.Report($"Descargando {id} {version.ToNormalizedString()}...");

            Directory.CreateDirectory(packageDir);
            var nupkgPath = Path.Combine(packageDir, $"{id}.{version.ToNormalizedString()}.nupkg");

            using (var fileStream = File.Create(nupkgPath))
            {
                var ok = await findResource.CopyNupkgToStreamAsync(id, version, fileStream, cache, logger, ct);
                if (!ok)
                    throw new InvalidOperationException("nuget.org no devolvio el paquete.");
            }

            List<string> extracted;
            List<PackageDependency> dependencies;
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

            return (extracted, dependencies);
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
