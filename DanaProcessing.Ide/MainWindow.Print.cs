using Avalonia.Controls;
using Avalonia.Platform.Storage;
using DanaProcessing.Ide.Export;
using DanaProcessing.Ide.Localization;
using DanaProcessing.Ide.Theme;

namespace DanaProcessing.Ide
{
    /// <summary>File > Export for print: a big PNG or an SVG (plain or for a plotter) -- see PrintExport.</summary>
    public partial class MainWindow
    {
        private async Task ExportForPrintAsync()
        {
            if (_isRunning)
                return;

            bool running = _runningTab != null && _runningTab == _editorView.ActiveTab;
            var options = await PrintExportWindow.ShowAsync(this,
                running ? _canvas.SketchWidth : 600, running ? _canvas.SketchHeight : 400,
                running ? _canvas.SketchFrameCount : 1);
            if (options is null)
                return;

            var source = await LoadVariationSourceAsync();
            if (source.Factory is not { } factory)
            {
                _outputText.Text = string.Join(Environment.NewLine, source.Errors);
                SetBottomTab(BottomTab.Run);
                RefreshBottomPanelVisibility();
                return;
            }

            bool png = options.Format == PrintExportWindow.Format.Png;
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.StorageProvider is null)
                return;
            int seed = _seed != 0 ? _seed : Random.Shared.Next(1, 1_000_000);
            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = Loc.Tr("Guardar para imprimir", "Save for print"),
                SuggestedFileName = $"{_editorView.SuggestedExportName}-{seed}{(png ? $"-{options.Scale}x.png" : ".svg")}",
                DefaultExtension = png ? "png" : "svg",
                FileTypeChoices = new[] { png ? new FilePickerFileType("PNG") { Patterns = new[] { "*.png" } } : new FilePickerFileType("SVG") { Patterns = new[] { "*.svg" } } },
            });
            if (file is null)
                return;
            string path = file.Path.LocalPath;
            var tweaks = CurrentTweakLiterals();

            _statusDot.Fill = ClayTheme.Accent;
            _statusLabel.Text = Loc.Tr("Exportando para imprimir...", "Exporting for print...");
            ((Border)_statusPill).Background = ClayTheme.SurfaceHigher;
            StartLoadingSpinner();
            try
            {
                var progress = new Progress<double>(p => _statusLabel.Text = Loc.Tr($"Exportando para imprimir... {p:P0}", $"Exporting for print... {p:P0}"));
                var result = await Task.Run(() => options.Format switch
                {
                    PrintExportWindow.Format.Png => PrintExport.SavePng(factory, seed, tweaks, options.Frames, options.Scale, path, progress),
                    PrintExportWindow.Format.Svg => PrintExport.SaveSvg(factory, seed, tweaks, options.Frames, path),
                    _ => PrintExport.SaveSvg(factory, seed, tweaks, options.Frames, path, new PlotterOptions()),
                });
                _statusDot.Fill = ClayTheme.Success;
                _statusLabel.Text = Loc.Tr($"Exportado: {result.Width} × {result.Height}", $"Exported: {result.Width} × {result.Height}");
                ((Border)_statusPill).Background = ClayTheme.SuccessSurface;
                ShowTransientStatus((result.Warning ?? Loc.Tr($"Guardado en {path}", $"Saved to {path}")));
            }
            catch (Exception ex)
            {
                _statusDot.Fill = ClayTheme.Danger;
                _statusLabel.Text = Loc.Tr("Error exportando", "Export error");
                ((Border)_statusPill).Background = ClayTheme.DangerSurface;
                _outputText.Text = ex.Message;
                SetBottomTab(BottomTab.Run);
                RefreshBottomPanelVisibility();
            }
            finally
            {
                StopLoadingSpinner();
            }
        }
    }
}
