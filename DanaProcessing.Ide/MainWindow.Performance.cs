using Avalonia.Controls;
using DanaProcessing.Ide.Localization;
using DanaProcessing.Ide.Theme;

namespace DanaProcessing.Ide
{
    /// <summary>Performance mode (F11): the running sketch full screen with its code on top -- see PerformanceWindow.</summary>
    public partial class MainWindow
    {
        private PerformanceWindow? _performanceWindow;

        private async Task TogglePerformanceModeAsync()
        {
            if (_performanceWindow != null)
            {
                _performanceWindow.Close();
                return;
            }

            var tab = _editorView.ActiveTab;
            if (tab == null)
                return;

            // The stage shows the code you're looking at: run it first if it isn't the one running.
            if (_runningTab != tab)
                await RunCurrentSketchAsync();
            if (_runningTab != tab)
                return; // didn't compile: the errors are already in the Run tab

            _canvasScroll.Content = null; // the canvas moves to the stage, still running
            _canvas.IsPaused = false;

            var stage = new PerformanceWindow(_canvas, tab.Document, HotReloadOrRunAsync, RunCurrentSketchAsync, ReadStageStatus, _canvas.ToggleTweaks);
            stage.Closed += (_, _) =>
            {
                _canvasScroll.Content = stage.ReleaseCanvas();
                _performanceWindow = null;
                Activate();
            };
            _performanceWindow = stage;
            stage.Show();
        }

        /// <summary>Ctrl+Enter on stage: keep the state when possible (Hot Reload), restart otherwise.</summary>
        private async Task HotReloadOrRunAsync()
        {
            if (_hotReloadButton.IsEnabled)
                await HotReloadCurrentSketchAsync();
            else
                await RunCurrentSketchAsync();
        }

        /// <summary>What the stage's status line shows: the first error when the last Run/Hot Reload failed, the status pill otherwise.</summary>
        private (string Text, bool IsError) ReadStageStatus()
        {
            bool error = ReferenceEquals(_runTabDot.Fill, ClayTheme.Danger);
            if (error)
            {
                var first = (_outputText.Text ?? "").Split('\n').FirstOrDefault(l => !string.IsNullOrWhiteSpace(l)) ?? "";
                return (first.Trim(), true);
            }
            return (_statusLabel.Text ?? "", false);
        }
    }
}
