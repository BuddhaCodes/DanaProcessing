using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using AvaloniaEdit.Document;

namespace DanaProcessing.Ide.Editor
{
    /// <summary>
    /// One open file inside the editor's tab strip. Holds its own TextDocument
    /// so switching tabs preserves undo history, caret position, and scroll
    /// offset — same behavior as Processing's per-tab editors, achieved here by
    /// swapping which TextDocument the single shared TextEditor points at.
    /// </summary>
    public class EditorTab : INotifyPropertyChanged
    {
        private string? _filePath;
        private bool _isDirty;

        public event PropertyChangedEventHandler? PropertyChanged;

        public TextDocument Document { get; }

        public string? FilePath
        {
            get => _filePath;
            set
            {
                if (_filePath == value)
                    return;
                _filePath = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Title));
            }
        }

        /// <summary>True once the document has changed since it was last saved.</summary>
        public bool IsDirty
        {
            get => _isDirty;
            set
            {
                if (_isDirty == value)
                    return;
                _isDirty = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Title));
            }
        }

        /// <summary>Text shown on the tab header: file name (or "Sin nombre") plus a dirty marker.</summary>
        public string Title =>
            (FilePath is null ? "Sin nombre" : Path.GetFileNameWithoutExtension(FilePath)) + (IsDirty ? " *" : "");

        public EditorTab(string? filePath, string initialText)
        {
            _filePath = filePath;
            Document = new TextDocument(initialText ?? string.Empty);

            // Any edit — including the very first keystroke on a fresh tab —
            // marks the tab dirty. MarkSaved() is the only way back to clean.
            Document.TextChanged += (_, _) => IsDirty = true;
        }

        public void MarkSaved() => IsDirty = false;

        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
