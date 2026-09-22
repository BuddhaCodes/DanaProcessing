using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using DanaProcessing;
using DanaProcessing.Ide.Compilation.PackageManagement;

namespace DanaProcessing.Ide.Compilation
{
    /// <summary>Result of compiling sketch source: either a ready-to-run Sketch, or a list of error messages.</summary>
    public record CompileResult(Sketch? Sketch, IReadOnlyList<string> Errors)
    {
        public bool Success => Sketch != null;
    }

    /// <summary>
    /// Compiles the text from the editor in memory (Roslyn) and instantiates the
    /// first class found that derives from Sketch. This is what pressing "Run" does —
    /// no files written to disk, no separate dotnet build process.
    /// </summary>
    public static class SketchCompiler
    {
        // C# 10+ is required for `global using` (used below to make
        // `using DanaProcessing;` implicit in every sketch). Pinning this
        // explicitly means both syntax trees we parse are guaranteed to
        // agree on language version, regardless of whatever Roslyn version
        // ends up referenced.
        private static readonly CSharpParseOptions ParseOptions =
            new CSharpParseOptions(LanguageVersion.CSharp10);

        /// <summary>
        /// A synthetic, invisible-to-the-user source file containing just the
        /// global usings every sketch should get "for free" — currently just
        /// DanaProcessing itself, so sketches can write `public class MySketch
        /// : Sketch` without their own `using DanaProcessing;` line.
        ///
        /// Deliberately NOT adding `using SkiaSharp;` here (and not
        /// referencing SkiaSharp.dll in GetReferences() below either): every
        /// DanaProcessing API that touches color exposes DanaColor instead of
        /// SkiaSharp's SKColor (see DanaColor.cs), specifically so sketch code
        /// never needs to know SkiaSharp exists. If a future API accidentally
        /// leaks an SKColor/SKPoint/etc. into a public method signature,
        /// that's the bug to fix — not a reason to add SkiaSharp back here.
        /// </summary>
        internal const string ImplicitUsingsSource = "global using DanaProcessing;";

        /// <summary>Language version pinned here too, so a completion-time syntax tree
        /// and a Run-time syntax tree never disagree about what's valid C#.</summary>
        internal static readonly CSharpParseOptions SharedParseOptions = ParseOptions;

        // simple assembly name -> file path, populated by SetNuGetAssemblies right
        // before each Compile() call that has `// nuget:` directives to satisfy.
        // Resolving is process-wide (AssemblyLoadContext.Default), not per-sketch,
        // because a byte[]-loaded sketch assembly resolves its dependencies through
        // the Default context regardless of which Compile() call produced it — see
        // the remark on SetNuGetAssemblies below for the one consequence of that.
        private static readonly Dictionary<string, string> NuGetAssemblyPaths = new(StringComparer.OrdinalIgnoreCase);

        static SketchCompiler()
        {
            AssemblyLoadContext.Default.Resolving += (context, name) =>
            {
                if (name.Name != null && NuGetAssemblyPaths.TryGetValue(name.Name, out var path))
                {
                    try { return context.LoadFromAssemblyPath(path); }
                    catch { return null; }
                }
                return null;
            };
        }

        /// <summary>
        /// Tells the runtime where to find each NuGet package assembly a sketch's
        /// `// nuget:` directives resolved to, so that when the compiled sketch
        /// assembly is instantiated and its code first touches a type from one of
        /// those packages, AssemblyLoadContext.Default.Resolving (wired up above)
        /// can load it on demand — Assembly.Load(byte[]) below runs the sketch in
        /// the Default context, same as any normally-referenced assembly.
        ///
        /// Caveat: once a given simple name (e.g. "Newtonsoft.Json") has actually
        /// been loaded into the Default context, the CLR won't ask to resolve it
        /// again — so switching a sketch to a *different* version of a package
        /// already loaded this way earlier in the session keeps using the old one
        /// until the IDE restarts. Acceptable for a creative-coding sketch host;
        /// not something a general-purpose plugin loader could get away with.
        /// </summary>
        public static void SetNuGetAssemblies(IEnumerable<ResolvedNuGetPackage> packages)
        {
            NuGetAssemblyPaths.Clear();
            foreach (var package in packages)
                foreach (var path in package.AssemblyPaths)
                    NuGetAssemblyPaths[Path.GetFileNameWithoutExtension(path)] = path;
        }

        public static CompileResult Compile(string sourceCode, IReadOnlyList<MetadataReference>? extraReferences = null)
        {
            var userTree = CSharpSyntaxTree.ParseText(sourceCode, ParseOptions);
            var implicitUsingsTree = CSharpSyntaxTree.ParseText(ImplicitUsingsSource, ParseOptions, path: "ImplicitUsings.cs");

            var references = GetSharedReferences();
            if (extraReferences != null)
                references.AddRange(extraReferences);

            var compilation = CSharpCompilation.Create(
                assemblyName: "DanaProcessing.Sketch." + Guid.NewGuid().ToString("N"),
                syntaxTrees: new[] { userTree, implicitUsingsTree },
                references: references,
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            using var ms = new MemoryStream();
            var emitResult = compilation.Emit(ms);

            if (!emitResult.Success)
            {
                var errors = emitResult.Diagnostics
                    .Where(d => d.Severity == DiagnosticSeverity.Error)
                    .Select(d => d.ToString())
                    .ToList();
                return new CompileResult(null, errors);
            }

            ms.Seek(0, SeekOrigin.Begin);
            var assembly = Assembly.Load(ms.ToArray());

            var sketchType = assembly.GetTypes()
                .FirstOrDefault(t => typeof(Sketch).IsAssignableFrom(t) && !t.IsAbstract);

            if (sketchType is null)
                return new CompileResult(null, new[] { "No se encontro ninguna clase publica que herede de Sketch." });

            var instance = (Sketch)Activator.CreateInstance(sketchType)!;
            return new CompileResult(instance, Array.Empty<string>());
        }

        /// <summary>
        /// Gathers metadata references: the full BCL from the assemblies this
        /// process already trusts (avoids needing a separate reference-assemblies
        /// NuGet package), plus DanaProcessing.dll so sketch code can see
        /// Sketch/PVector/DanaColor/etc. Note SkiaSharp.dll is deliberately
        /// NOT referenced here — see the remark on ImplicitUsingsSource above.
        /// </summary>
        internal static List<MetadataReference> GetSharedReferences()
        {
            var references = new List<MetadataReference>();

            var trustedAssembliesPaths =
                (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string)?.Split(Path.PathSeparator)
                ?? Array.Empty<string>();

            foreach (var path in trustedAssembliesPaths)
            {
                try
                { references.Add(MetadataReference.CreateFromFile(path)); }
                catch { /* skip anything that fails to load as metadata (rare, safe to ignore) */ }
            }

            // Assembly.Location comes back "" instead of throwing for an assembly
            // loaded from somewhere other than a plain file on disk (notably: a
            // single-file-published exe's own bundled managed assemblies, before
            // they've been extracted anywhere — see the exported-sketch runner's
            // remark on IncludeAllContentForSelfExtract for why that case doesn't
            // normally arise here). CreateFromFile("") throws ArgumentException
            // either way, so this stays defensive rather than trusting that flag
            // choice alone: if it ever does happen, a sketch simply can't see
            // Sketch/PVector/etc. (a clear, debuggable Roslyn compile error) is a
            // far better failure mode than crashing the whole process over it.
            try
            { references.Add(MetadataReference.CreateFromFile(typeof(Sketch).Assembly.Location)); }
            catch { /* see remark above */ }

            return references;
        }

        /// <summary>
        /// Registers every .dll sitting in <paramref name="directory"/> as a
        /// runtime-loadable assembly by simple name, via the same Resolving hook
        /// used for `// nuget:` packages during a normal Run (see SetNuGetAssemblies
        /// above). Reused for an exported standalone sketch's bundled dependencies —
        /// see DanaProcessing.Ide.Export.SketchExporter, which is what copies those
        /// .dll files in next to the exported exe in the first place.
        /// </summary>
        public static void RegisterAssemblyDirectory(string directory)
        {
            if (!Directory.Exists(directory))
                return;

            foreach (var dll in Directory.GetFiles(directory, "*.dll"))
                NuGetAssemblyPaths[Path.GetFileNameWithoutExtension(dll)] = dll;
        }

        /// <summary>
        /// Every .dll in <paramref name="directory"/> that ISN'T already one of the
        /// trusted platform assemblies, as compile-time Roslyn references. Trusted
        /// platform ones are skipped because GetSharedReferences() above already
        /// references them — adding the same simple-named assembly a second time
        /// under a separate MetadataReference would surface as an ambiguous-reference
        /// compile error instead of just being redundant.
        /// </summary>
        public static List<MetadataReference> CollectExternalReferences(string directory)
        {
            var references = new List<MetadataReference>();
            if (!Directory.Exists(directory))
                return references;

            var trustedNames = new HashSet<string>(
                (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string)?
                    .Split(Path.PathSeparator)
                    .Select(Path.GetFileNameWithoutExtension)
                    .OfType<string>() ?? Enumerable.Empty<string>(),
                StringComparer.OrdinalIgnoreCase);

            foreach (var dll in Directory.GetFiles(directory, "*.dll"))
            {
                if (trustedNames.Contains(Path.GetFileNameWithoutExtension(dll)))
                    continue;

                try { references.Add(MetadataReference.CreateFromFile(dll)); }
                catch { /* not a valid managed assembly (a native dependency, etc.) -- skip it */ }
            }

            return references;
        }
    }
}