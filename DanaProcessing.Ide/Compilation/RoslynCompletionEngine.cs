using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Text;

namespace DanaProcessing.Ide.Compilation
{
    /// <summary>
    /// One completion candidate. Deliberately has no AvaloniaEdit types in it —
    /// this class is the boundary between "Roslyn knows this" and "the editor
    /// UI shows this", so the engine stays testable without an Avalonia app.
    /// </summary>
    public sealed record CompletionCandidate(string DisplayText, string SortText, string Kind);

    /// <summary>One compiler diagnostic mapped to plain offsets, so the editor UI
    /// doesn't need to know about TextSpan/Roslyn types.</summary>
    public sealed record SketchDiagnostic(int Start, int Length, string Message, bool IsError);

    /// <summary>Distinguishes the three outcomes a navigation query (Go to
    /// Definition/Implementation, Find All References) can have -- deliberately
    /// not just "found or null", since "there's a real symbol here but it lives
    /// in DanaProcessing's own compiled DLL, which carries no source" is a
    /// distinct, expected case that needs its own message, not a silent no-op.</summary>
    public enum NavigationKind { NoSymbol, NoSourceAvailable, Found }

    /// <summary>One navigable span mapped to plain offsets, same boundary as SketchDiagnostic.</summary>
    public sealed record NavigationLocation(int Start, int Length);

    /// <summary>Result of a Go to Definition/Implementation/Find-References query.
    /// SymbolDisplayName is populated whenever a real symbol was resolved (even
    /// with no navigable source), so the UI can name it in its feedback message.</summary>
    public sealed record NavigationResult(NavigationKind Kind, string? SymbolDisplayName, IReadOnlyList<NavigationLocation> Locations)
    {
        public static readonly NavigationResult None = new(NavigationKind.NoSymbol, null, Array.Empty<NavigationLocation>());
        public static NavigationResult NoSource(string displayName) => new(NavigationKind.NoSourceAvailable, displayName, Array.Empty<NavigationLocation>());
        public static NavigationResult Found(string displayName, IReadOnlyList<NavigationLocation> locations) => new(NavigationKind.Found, displayName, locations);
    }

    /// <summary>
    /// Wraps a single-document Roslyn <see cref="AdhocWorkspace"/> so the editor
    /// can ask "what's valid to type here" using the same CompletionService that
    /// powers IntelliSense in Visual Studio / OmniSharp — not a hand-rolled
    /// keyword or regex-based suggestion list.
    ///
    /// It intentionally reuses SketchCompiler's parse options, implicit global
    /// usings, and metadata references (<see cref="SketchCompiler.GetSharedReferences"/>),
    /// so a suggestion you accept here is guaranteed to be something that will
    /// also resolve when the user presses Run — the two never see a different
    /// picture of what "DanaProcessing" contains.
    ///
    /// One engine instance is shared across tabs (SketchEditorView owns exactly
    /// one), and its document text is swapped whenever the active tab changes —
    /// mirroring how the single shared AvaloniaEdit TextEditor swaps which
    /// TextDocument it points at.
    /// </summary>
    public sealed class RoslynCompletionEngine
    {
        private readonly AdhocWorkspace _workspace = new();
        private readonly ProjectId _projectId;
        private DocumentId _documentId;

        public RoslynCompletionEngine()
        {
            var projectId = ProjectId.CreateNewId();
            _projectId = projectId;

            var projectInfo = ProjectInfo.Create(
                projectId,
                VersionStamp.Create(),
                name: "Sketch",
                assemblyName: "DanaProcessing.Sketch.Completion",
                language: LanguageNames.CSharp,
                compilationOptions: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary),
                parseOptions: SketchCompiler.SharedParseOptions,
                metadataReferences: SketchCompiler.GetSharedReferences());

            var solution = _workspace.CurrentSolution.AddProject(projectInfo);

            // Same trick as SketchCompiler: a synthetic file carrying the
            // global usings, invisible to the user, so "Size(...)", "Fill(...)"
            // etc. resolve without them writing "using DanaProcessing;" themselves.
            var implicitUsingsId = DocumentId.CreateNewId(projectId);
            solution = solution.AddDocument(implicitUsingsId, "ImplicitUsings.cs", SketchCompiler.ImplicitUsingsSource);

            _documentId = DocumentId.CreateNewId(projectId);
            solution = solution.AddDocument(_documentId, "Sketch.cs", string.Empty);

            if (!_workspace.TryApplyChanges(solution))
                throw new InvalidOperationException("No se pudo inicializar el workspace de Roslyn para autocompletado.");
        }

        /// <summary>Compiler errors/warnings for the document's current text, like the
        /// red squiggles you'd see in Visual Studio while typing — independent of and
        /// much cheaper than a full SketchCompiler.Compile() (no emit, no assembly load).</summary>
        public async Task<IReadOnlyList<SketchDiagnostic>> GetDiagnosticsAsync(CancellationToken ct = default)
        {
            var document = _workspace.CurrentSolution.GetDocument(_documentId);
            if (document is null)
                return Array.Empty<SketchDiagnostic>();

            var semanticModel = await document.GetSemanticModelAsync(ct);
            if (semanticModel is null)
                return Array.Empty<SketchDiagnostic>();

            return semanticModel.GetDiagnostics(cancellationToken: ct)
                .Where(d => d.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning)
                // El árbol synthetic de ImplicitUsings.cs no es del usuario -- nunca
                // mostrarle un error ahí, sería indescifrable.
                .Where(d => d.Location.SourceTree?.FilePath == "Sketch.cs")
                .Select(d => new SketchDiagnostic(
                    d.Location.SourceSpan.Start,
                    d.Location.SourceSpan.Length,
                    d.GetMessage(),
                    d.Severity == DiagnosticSeverity.Error))
                .ToList();
        }

        /// <summary>
        /// Call this whenever the active tab's text changes (or right after
        /// switching tabs) so the workspace's copy of the source stays in sync
        /// with what's on screen before asking for completions.
        /// </summary>
        public void UpdateText(string text)
        {
            var solution = _workspace.CurrentSolution.WithDocumentText(_documentId, SourceText.From(text));
            _workspace.TryApplyChanges(solution);
        }

        /// <summary>
        /// Adds resolved `// nuget:` package assemblies to the completion/diagnostics
        /// project, on top of the shared BCL + DanaProcessing references it was built
        /// with. Without this, a NuGet package's types compile and run fine via
        /// SketchCompiler.Compile() (which gets the same extra references passed
        /// directly) but keep showing as unresolved-symbol red squiggles here forever,
        /// since this engine's project references were otherwise fixed at construction
        /// time. Call it right after a successful NuGetPackageResolver.ResolveAsync(),
        /// same as SketchCompiler.SetNuGetAssemblies() already does for the compiler
        /// side of this exact problem.
        /// </summary>
        public void UpdateReferences(IReadOnlyList<MetadataReference> extraReferences)
        {
            var allReferences = SketchCompiler.GetSharedReferences().Concat(extraReferences);
            var solution = _workspace.CurrentSolution.WithProjectMetadataReferences(_projectId, allReferences);
            _workspace.TryApplyChanges(solution);
        }

        /// <summary>Semantic completions valid at <paramref name="caretOffset"/>, or empty if none apply there.</summary>
        public async Task<IReadOnlyList<CompletionCandidate>> GetCompletionsAsync(int caretOffset, CancellationToken ct = default)
        {
            var document = _workspace.CurrentSolution.GetDocument(_documentId);
            var service = document is null ? null : CompletionService.GetService(document);
            if (document is null || service is null)
                return Array.Empty<CompletionCandidate>();

            CompletionList? completions;
            try
            {
                completions = await service.GetCompletionsAsync(document, caretOffset, cancellationToken: ct);
            }
            catch (OperationCanceledException)
            {
                return Array.Empty<CompletionCandidate>();
            }

            if (completions is null)
                return Array.Empty<CompletionCandidate>();

            return completions.ItemsList
                .Select(item => new CompletionCandidate(item.DisplayText, item.SortText, item.Tags.FirstOrDefault() ?? ""))
                .ToList();
        }

        /// <summary>
        /// Roslyn completion items don't always insert their DisplayText verbatim
        /// (namespace imports, overrides, etc. can rewrite more than the caret
        /// word). This resolves the *real* text edit for the item the user
        /// picked, so accepting a suggestion behaves the same way it would in
        /// Visual Studio rather than just pasting a label in.
        /// Returns null if the item can no longer be found (text changed underneath it).
        /// </summary>
        public async Task<(int Start, int Length, string NewText)?> ResolveCommitAsync(
            string displayText, int caretOffset, CancellationToken ct = default)
        {
            var document = _workspace.CurrentSolution.GetDocument(_documentId);
            var service = document is null ? null : CompletionService.GetService(document);
            if (document is null || service is null)
                return null;

            var completions = await service.GetCompletionsAsync(document, caretOffset, cancellationToken: ct);
            var item = completions?.ItemsList.FirstOrDefault(i => i.DisplayText == displayText);
            if (item is null)
                return null;

            var change = await service.GetChangeAsync(document, item, cancellationToken: ct);
            var span = change.TextChange.Span;
            return (span.Start, span.Length, change.TextChange.NewText ?? "");
        }

        /// <summary>Resolves whatever symbol sits at (or just before) <paramref name="offset"/>,
        /// covering both "caret on a usage" (GetSymbolInfo) and "caret on a declaration itself"
        /// (GetDeclaredSymbol, e.g. standing on the method name in "public override void Draw()").</summary>
        private async Task<ISymbol?> ResolveSymbolAtAsync(int offset, CancellationToken ct)
        {
            var document = _workspace.CurrentSolution.GetDocument(_documentId);
            if (document is null)
                return null;

            var root = await document.GetSyntaxRootAsync(ct);
            var model = await document.GetSemanticModelAsync(ct);
            if (root is null || model is null)
                return null;

            var clamped = Math.Clamp(offset, 0, root.FullSpan.Length);
            var token = root.FindToken(clamped);
            // El caret justo despues de un identificador (el caso mas comun de
            // F12 con el cursor ahi parado) hace que FindToken devuelva el
            // SIGUIENTE token en vez del identificador mismo -- mirar uno atras
            // en ese caso.
            if (!IsNavigable(token) && clamped > 0)
            {
                var prev = root.FindToken(clamped - 1);
                if (IsNavigable(prev))
                    token = prev;
            }
            if (!IsNavigable(token))
                return null;

            var node = token.Parent;
            if (node is null)
                return null;

            return model.GetDeclaredSymbol(node, ct)
                ?? model.GetSymbolInfo(node, ct).Symbol
                ?? (node.Parent is null ? null : model.GetSymbolInfo(node.Parent, ct).Symbol);
        }

        private static bool IsNavigable(SyntaxToken token) =>
            token.IsKind(SyntaxKind.IdentifierToken) || SyntaxFacts.IsKeywordKind(token.Kind());

        /// <summary>Only the current document's own source counts as navigable --
        /// the synthetic ImplicitUsings.cs tree is invisible to the user, and any
        /// metadata symbol (DanaProcessing.dll's own API, BCL types) carries no
        /// source location at all, same filter GetDiagnosticsAsync already relies on.</summary>
        private static IReadOnlyList<NavigationLocation> SourceLocationsOf(ISymbol symbol) =>
            symbol.Locations
                .Where(l => l.IsInSource && l.SourceTree?.FilePath == "Sketch.cs")
                .Select(l => new NavigationLocation(l.SourceSpan.Start, l.SourceSpan.Length))
                .ToList();

        private async Task<NavigationResult> BuildDefinitionResultAsync(ISymbol symbol, CancellationToken ct)
        {
            var definition = await SymbolFinder.FindSourceDefinitionAsync(symbol, _workspace.CurrentSolution, ct) ?? symbol;
            var locations = SourceLocationsOf(definition);
            return locations.Count > 0
                ? NavigationResult.Found(definition.ToDisplayString(), locations)
                : NavigationResult.NoSource(definition.ToDisplayString());
        }

        /// <summary>Jumps to where the symbol under the caret is declared. For a symbol
        /// from DanaProcessing's own engine API (Circle, PVector, Sketch itself, ...) this
        /// resolves a real symbol but returns NoSourceAvailable -- that DLL carries no PDB,
        /// so there is nothing to navigate to (see SketchCompiler.GetSharedReferences).</summary>
        public async Task<NavigationResult> GoToDefinitionAsync(int caretOffset, CancellationToken ct = default)
        {
            var symbol = await ResolveSymbolAtAsync(caretOffset, ct);
            return symbol is null ? NavigationResult.None : await BuildDefinitionResultAsync(symbol, ct);
        }

        /// <summary>All references to the symbol under the caret, within this one document --
        /// there's nothing else in the workspace to search, since each sketch is its own
        /// independent single-file compilation.</summary>
        public async Task<NavigationResult> FindReferencesAsync(int caretOffset, CancellationToken ct = default)
        {
            var symbol = await ResolveSymbolAtAsync(caretOffset, ct);
            if (symbol is null)
                return NavigationResult.None;

            var definition = await SymbolFinder.FindSourceDefinitionAsync(symbol, _workspace.CurrentSolution, ct) ?? symbol;
            var declarationLocations = SourceLocationsOf(definition);
            if (declarationLocations.Count == 0)
                return NavigationResult.NoSource(definition.ToDisplayString());

            var referencedSymbols = await SymbolFinder.FindReferencesAsync(definition, _workspace.CurrentSolution, ct);
            var referenceLocations = referencedSymbols
                .SelectMany(rs => rs.Locations)
                .Select(rl => rl.Location)
                .Where(l => l.IsInSource && l.SourceTree?.FilePath == "Sketch.cs")
                .Select(l => new NavigationLocation(l.SourceSpan.Start, l.SourceSpan.Length));

            var locations = declarationLocations
                .Concat(referenceLocations)
                .Distinct()
                .OrderBy(l => l.Start)
                .ToList();

            return NavigationResult.Found(definition.ToDisplayString(), locations);
        }

        /// <summary>For a virtual/abstract/override member (e.g. the Sketch base class's
        /// Setup/Draw/MousePressed/... hooks), jumps to the user's own concrete override(s)
        /// in this document instead of dead-ending on the (metadata-only) base declaration.
        /// For anything else, behaves exactly like GoToDefinitionAsync -- the standard IDE
        /// convention for a non-overridable member.</summary>
        public async Task<NavigationResult> GoToImplementationAsync(int caretOffset, CancellationToken ct = default)
        {
            var symbol = await ResolveSymbolAtAsync(caretOffset, ct);
            if (symbol is null)
                return NavigationResult.None;

            if (symbol is not IMethodSymbol { } method || !(method.IsVirtual || method.IsAbstract || method.IsOverride))
                return await BuildDefinitionResultAsync(symbol, ct);

            var root = method;
            while (root.OverriddenMethod is { } baseMethod)
                root = baseMethod;

            var overrides = await SymbolFinder.FindOverridesAsync(root, _workspace.CurrentSolution, cancellationToken: ct);
            var locations = overrides
                .SelectMany(SourceLocationsOf)
                .Distinct()
                .OrderBy(l => l.Start)
                .ToList();

            // El caso mas comun es invocar esto parado YA en la unica override que
            // existe -- FindOverridesAsync busca overrides de "root" hacia abajo, asi
            // que no incluye a "symbol" mismo si symbol ya es esa override. Caer a
            // Go to Definition cubre ese caso en vez de reportar "no encontrado".
            return locations.Count > 0
                ? NavigationResult.Found(root.ToDisplayString(), locations)
                : await BuildDefinitionResultAsync(symbol, ct);
        }
    }
}