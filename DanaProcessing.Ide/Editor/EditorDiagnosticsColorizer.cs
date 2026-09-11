using System;
using System.Collections.Generic;
using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using DanaProcessing.Ide.Compilation;

namespace DanaProcessing.Ide.Editor
{
    /// <summary>
    /// A live Roslyn diagnostic (see RoslynCompletionEngine.GetDiagnosticsAsync) already
    /// resolved to a 1-based line/column, for display in MainWindow's "Errores en vivo"
    /// tab. SketchDiagnostic itself only carries a raw character offset — converting that
    /// to line/column needs the TextDocument, which lives in SketchEditorView, not here.
    /// </summary>
    public sealed record LiveDiagnosticInfo(int Offset, int Line, int Column, string Message, bool IsError);

    /// <summary>
    /// Underlines live Roslyn diagnostics (see RoslynCompletionEngine.GetDiagnosticsAsync)
    /// directly under the offending text — red for errors, amber for warnings. Same idea
    /// as the red/yellow squiggles in Visual Studio or VS Code.
    ///
    /// Before this existed, the only compiler feedback the IDE gave was the error panel
    /// SketchCompiler.Compile() fills in after pressing Run (see MainWindow.RunCurrentSketch)
    /// — a full emit, and only on demand. This repaints on a short debounce as you type
    /// instead (see SketchEditorView.RefreshDiagnosticsAsync), using the same cheap
    /// semantic-model-only diagnostics GetDiagnosticsAsync already computed for nothing
    /// until now.
    ///
    /// Diagnostics are recomputed wholesale on every refresh (no incremental tracking), so
    /// this only needs to hold the latest snapshot — it doesn't need to survive edits the
    /// way a TextSegmentCollection would.
    /// </summary>
    public sealed class EditorDiagnosticsColorizer : DocumentColorizingTransformer
    {
        private static readonly IBrush ErrorBrush = new SolidColorBrush(Avalonia.Media.Color.Parse("#E86A6A"));
        private static readonly IBrush WarningBrush = new SolidColorBrush(Avalonia.Media.Color.Parse("#D9A63E"));

        private static readonly TextDecorationCollection ErrorUnderline = new()
        {
            new TextDecoration { Location = TextDecorationLocation.Underline, Stroke = ErrorBrush, StrokeThickness = 1.4 }
        };

        private static readonly TextDecorationCollection WarningUnderline = new()
        {
            new TextDecoration { Location = TextDecorationLocation.Underline, Stroke = WarningBrush, StrokeThickness = 1.4 }
        };

        private List<SketchDiagnostic> _diagnostics = new();

        /// <summary>Latest snapshot, for hit-testing under the caret (see
        /// SketchEditorView's inline diagnostic banner).</summary>
        public IReadOnlyList<SketchDiagnostic> Current => _diagnostics;

        public void SetDiagnostics(IReadOnlyList<SketchDiagnostic> diagnostics)
        {
            _diagnostics = diagnostics is List<SketchDiagnostic> list ? list : new List<SketchDiagnostic>(diagnostics);
        }

        protected override void ColorizeLine(DocumentLine line)
        {
            if (_diagnostics.Count == 0)
                return;

            var lineStart = line.Offset;
            var lineEnd = line.EndOffset;

            foreach (var d in _diagnostics)
            {
                var start = d.Start;
                var end = d.Start + Math.Max(d.Length, 1);
                if (end <= lineStart || start >= lineEnd)
                    continue;

                var segStart = Math.Max(start, lineStart);
                var segEnd = Math.Min(end, lineEnd);
                if (segEnd <= segStart)
                    continue;

                // Errors win visually over warnings when both land on the same run —
                // ChangeLinePart below just applies whichever decoration this call uses,
                // and errors are typically reported after (and override) related warnings.
                var decoration = d.IsError ? ErrorUnderline : WarningUnderline;
                ChangeLinePart(segStart, segEnd, element =>
                    element.TextRunProperties.SetTextDecorations(decoration));
            }
        }

        /// <summary>The diagnostic (preferring an error over a warning) whose span contains
        /// <paramref name="offset"/>, or null if none does.</summary>
        public SketchDiagnostic? FindAt(int offset)
        {
            SketchDiagnostic? best = null;
            foreach (var d in _diagnostics)
            {
                var end = d.Start + Math.Max(d.Length, 1);
                if (offset < d.Start || offset > end)
                    continue;
                if (best is null || (d.IsError && !best.IsError))
                    best = d;
            }
            return best;
        }
    }
}