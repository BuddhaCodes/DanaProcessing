using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace DanaProcessing.Ide.Compilation
{
    /// <summary>
    /// One completion candidate. Deliberately has no AvaloniaEdit types in it —
    /// this class is the boundary between "Roslyn knows this" and "the editor
    /// UI shows this", so the engine stays testable without an Avalonia app.
    /// </summary>
    public sealed record CompletionCandidate(string DisplayText, string SortText, string Kind);

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
        private DocumentId _documentId;

        public RoslynCompletionEngine()
        {
            var projectId = ProjectId.CreateNewId();

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
    }
}