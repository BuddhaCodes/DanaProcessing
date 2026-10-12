using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace DanaProcessing.Ide.Editor
{
    /// <summary>What was open when the IDE closed: restored on the next start.</summary>
    public sealed class EditorSession
    {
        public List<EditorSessionTab> Tabs { get; set; } = new();
        public int Active { get; set; }
    }

    public sealed class EditorSessionTab
    {
        /// <summary>Saved file, or null for an untitled tab.</summary>
        public string? FilePath { get; set; }
        /// <summary>The text itself -- only stored for untitled tabs and tabs with unsaved edits; saved files are re-read from disk.</summary>
        public string? Text { get; set; }
        public bool Dirty { get; set; }
        public int Caret { get; set; }
    }

    /// <summary>JSON under the IDE's AppData folder, same place as its other settings.</summary>
    public static class EditorSessionStore
    {
        private const int MaxStoredChars = 2_000_000;

        private static string FilePath
        {
            get
            {
                var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DanaProcessingIde");
                Directory.CreateDirectory(dir);
                return Path.Combine(dir, "session.json");
            }
        }

        public static EditorSession? Load()
        {
            try
            {
                return File.Exists(FilePath) ? JsonSerializer.Deserialize<EditorSession>(File.ReadAllText(FilePath)) : null;
            }
            catch
            {
                return null; // a damaged session file just means starting fresh
            }
        }

        public static void Save(EditorSession session)
        {
            try
            {
                foreach (var t in session.Tabs)
                    if (t.Text is { Length: > MaxStoredChars })
                        t.Text = null;
                File.WriteAllText(FilePath, JsonSerializer.Serialize(session));
            }
            catch
            {
                // Not being able to remember the tabs is never worth an error dialog on exit.
            }
        }
    }
}
