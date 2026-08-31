using System;
using Avalonia.Media;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using DanaProcessing.Ide.Compilation;

namespace DanaProcessing.Ide.Editor
{
    /// <summary>
    /// One row in the completion popup. Holds only what's needed to render
    /// (Text/Content) plus enough to re-resolve the real Roslyn text edit on
    /// commit — see the remark on <see cref="RoslynCompletionEngine.ResolveCommitAsync"/>
    /// for why we don't just insert <see cref="Text"/> directly.
    /// </summary>
    public sealed class SketchCompletionData : ICompletionData
    {
        private readonly RoslynCompletionEngine _engine;
        private readonly int _caretOffsetAtOpen;

        public SketchCompletionData(RoslynCompletionEngine engine, CompletionCandidate candidate, int caretOffsetAtOpen)
        {
            _engine = engine;
            _caretOffsetAtOpen = caretOffsetAtOpen;
            Text = candidate.DisplayText;
            Content = candidate.DisplayText;
            Description = string.IsNullOrEmpty(candidate.Kind) ? candidate.DisplayText : $"{candidate.DisplayText} ({candidate.Kind})";
        }

        public IImage? Image => null;
        public string Text { get; }
        public object Content { get; }
        public object Description { get; }

        // Roslyn already sorts ItemsList by relevance; keep every row equal here
        // so AvaloniaEdit doesn't re-sort on top of that ordering.
        public double Priority => 0;

        public async void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
        {
            var change = await _engine.ResolveCommitAsync(Text, _caretOffsetAtOpen);
            if (change is { } c)
            {
                // The resolved edit's span was computed against the text as it
                // stood when the window opened. If the user kept typing (narrowing
                // the filter) the completion segment AvaloniaEdit tracked is the
                // up-to-date replacement target, so prefer it when the lengths
                // disagree — otherwise fall back to Roslyn's own span.
                textArea.Document.Replace(completionSegment.Offset, completionSegment.Length, c.NewText);
            }
            else
            {
                textArea.Document.Replace(completionSegment, Text);
            }
        }
    }
}