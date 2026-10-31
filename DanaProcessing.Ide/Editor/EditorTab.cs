using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using AvaloniaEdit.Document;
using DanaProcessing.Ide.Blocks;
using DanaProcessing.Ide.Localization;

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

        /// <summary>Text shown on the tab header: file name (or "Sin nombre") plus a dirty marker; 🧩 for a blocks tab.</summary>
        public string Title =>
            (Blocks is null ? "" : "🧩 ")
            + (FilePath is not null ? Path.GetFileNameWithoutExtension(FilePath)
               : Blocks is not null ? Blocks.Name
               : Loc.Tr("Sin nombre", "Untitled"))
            + (IsDirty ? " *" : "");

        /// <summary>
        /// The block program, for a tab made with blocks (null for an ordinary
        /// code tab). Its <see cref="Document"/> then always holds the C# the
        /// blocks become -- read-only for the child, but it's what live
        /// diagnostics, "Pasar a código" and exporting see.
        /// </summary>
        public BlockProgram? Blocks { get; set; }

        /// <summary>A blocks tab showing <paramref name="program"/>.</summary>
        public EditorTab(string? filePath, BlockProgram program)
            : this(filePath, BlockCodeGenerator.Generate(program).Source)
        {
            Blocks = program;
        }

        /// <summary>Regenerates <see cref="Document"/> after the blocks changed (which marks the tab dirty).</summary>
        public void RefreshFromBlocks()
        {
            if (Blocks is null)
                return;
            var source = BlockCodeGenerator.Generate(Blocks).Source;
            if (Document.Text != source)
                Document.Text = source;
            else
                IsDirty = true; // moved a block without changing the code: still worth saving
            OnPropertyChanged(nameof(Title));
        }

        /// <summary>
        /// What Run compiles: the document for a code tab; for a blocks tab, the
        /// same program with the lines that light blocks up, plus the
        /// challenge's guide if it's a challenge.
        /// </summary>
        public string RunSource => Blocks is null
            ? Document.Text
            : BlockCodeGenerator.Generate(Blocks, instrument: true, scene: ChallengeCatalog.Get(Blocks.ChallengeId)?.Scene()).Source;

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
