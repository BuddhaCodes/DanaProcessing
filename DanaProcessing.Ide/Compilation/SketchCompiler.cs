using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using DanaProcessing;

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
        private const string ImplicitUsingsSource = "global using DanaProcessing;";

        public static CompileResult Compile(string sourceCode)
        {
            var userTree = CSharpSyntaxTree.ParseText(sourceCode, ParseOptions);
            var implicitUsingsTree = CSharpSyntaxTree.ParseText(ImplicitUsingsSource, ParseOptions, path: "ImplicitUsings.cs");

            var compilation = CSharpCompilation.Create(
                assemblyName: "DanaProcessing.Sketch." + Guid.NewGuid().ToString("N"),
                syntaxTrees: new[] { userTree, implicitUsingsTree },
                references: GetReferences(),
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
        private static List<MetadataReference> GetReferences()
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

            references.Add(MetadataReference.CreateFromFile(typeof(Sketch).Assembly.Location));

            return references;
        }
    }
}