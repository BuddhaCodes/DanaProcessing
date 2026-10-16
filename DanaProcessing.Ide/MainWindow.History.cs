using DanaProcessing.Ide.Editor;
using DanaProcessing.Ide.Localization;

namespace DanaProcessing.Ide
{
    /// <summary>The code time machine: every Run is kept (CodeHistory) and browsable (CodeHistoryWindow).</summary>
    public partial class MainWindow
    {
        private string HistoryKey => CodeHistory.KeyFor(_editorView.ActiveTab?.FilePath);

        /// <summary>After a successful Run: keep the code, then -- once the sketch has drawn a little -- a picture of it.</summary>
        private async Task RecordRunInHistoryAsync(string code, int seed)
        {
            var version = await Task.Run(() => CodeHistory.Default.Record(HistoryKey, code, seed));
            if (version == null || version.ThumbnailPath != null)
                return; // couldn't write, or the same code is already pictured

            await Task.Delay(1500); // let animations get going: the first frame is often empty
            var capture = _canvas.CaptureFrameAsync(320);
            if (await Task.WhenAny(capture, Task.Delay(3000)) == capture && await capture is { } jpeg)
                await Task.Run(() => CodeHistory.Default.AttachThumbnail(version, jpeg));
        }

        private void OpenCodeHistory()
        {
            var tab = _editorView.ActiveTab;
            if (tab == null)
                return;
            var versions = CodeHistory.Default.List(HistoryKey);
            new CodeHistoryWindow(tab.Title.TrimEnd(' ', '*'), versions,
                () => tab.Document.Text,
                version => RestoreVersion(tab, version),
                version => _editorView.AddNewTab(null, version.ReadCode())).Show(this);
        }

        private void RestoreVersion(EditorTab tab, CodeVersion version)
        {
            var code = version.ReadCode();
            var doc = tab.Document;
            using (doc.RunUpdate())
                doc.Replace(0, doc.TextLength, code); // one Ctrl+Z puts your code back

            if (version.Seed != 0)
            {
                _seed = version.Seed;
                _seedLocked = true;
                _useSeedOnce = true;
                UpdateSeedLockMenuItem();
            }
            ShowTransientStatus(Loc.Tr(
                $"Código de {version.TimeUtc.ToLocalTime():HH:mm} restaurado (semilla {version.Seed} fija) -- Ctrl+Z para volver, ▶ para verlo",
                $"Code from {version.TimeUtc.ToLocalTime():HH:mm} restored (seed {version.Seed} locked) -- Ctrl+Z to go back, ▶ to see it"));
        }
    }
}
