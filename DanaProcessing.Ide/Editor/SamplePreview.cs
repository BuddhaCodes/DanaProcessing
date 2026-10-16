using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using DanaProcessing.AvaloniaHost;
using DanaProcessing.Ide.Compilation;
using DanaProcessing.Ide.Localization;
using DanaProcessing.Ide.Theme;

namespace DanaProcessing.Ide.Editor
{
    /// <summary>
    /// The selected sample, running live in the Examples window's details pane,
    /// scaled to fit -- you see what it does before opening it. Compiled off the
    /// UI thread a moment after the selection settles (arrow-key browsing
    /// doesn't compile every card it passes), and stopped (OnStop) as soon as
    /// the selection moves on or the window closes.
    ///
    /// Samples that would do something unexpected just by being looked at are
    /// shown without a preview: sound ([LiveLoop], Play, Sample), `// nuget:`
    /// downloads, and 3D/GPU (each preview would create a GL context).
    /// </summary>
    public sealed class SamplePreview : Border
    {
        private static readonly Regex NoPreview = new(
            @"//\s*nuget:|\[LiveLoop\]|\bPlay\s*\(|\bSample\s*\(|Renderer3D|CreateGpuParticles",
            RegexOptions.Compiled);

        private readonly Viewbox _box;
        private readonly TextBlock _note;
        private AvaloniaSketchCanvas? _canvas;
        private int _generation;
        private SketchSample? _current;

        public SamplePreview()
        {
            Height = 210;
            Margin = new Thickness(0, 12, 0, 0);
            CornerRadius = ClayTheme.RadiusSmall;
            ClipToBounds = true;
            Background = Brushes.Black;
            IsVisible = false;

            _box = new Viewbox { Stretch = Stretch.Uniform };
            _note = new TextBlock
            {
                Foreground = Brushes.Gray,
                FontFamily = ClayTheme.FontBody,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(16),
            };
            Child = new Panel { Children = { _box, _note } };
        }

        /// <summary>Shows <paramref name="sample"/> running (or why it can't), replacing whatever was previewed.</summary>
        public async void Show(SketchSample? sample)
        {
            if (ReferenceEquals(sample, _current))
                return; // re-selecting the same card (e.g. while filtering) keeps it running
            int generation = ++_generation;
            Stop();
            _current = sample;

            if (sample == null)
            {
                IsVisible = false;
                return;
            }
            IsVisible = true;

            if (NoPreview.IsMatch(sample.Source))
            {
                ShowNote(Loc.Tr("Sin vista previa: usa sonido, paquetes NuGet o 3D. Abrilo para verlo correr.",
                                "No preview: it uses sound, NuGet packages or 3D. Open it to see it run."));
                return;
            }

            ShowNote(Loc.Tr("Preparando la vista previa...", "Preparing the preview..."));
            await Task.Delay(250); // let arrow-key browsing settle first
            if (generation != _generation)
                return;

            CompileResult result;
            try
            {
                result = await Task.Run(() => SketchCompiler.Compile(sample.Source));
            }
            catch (Exception ex)
            {
                result = new CompileResult(null, new[] { ex.Message });
            }
            if (generation != _generation)
                return; // selection moved on; this sketch never ran Setup(), nothing to stop

            if (!result.Success)
            {
                ShowNote(Loc.Tr("Sin vista previa: no compila.", "No preview: it doesn't compile."));
                return;
            }

            var canvas = new AvaloniaSketchCanvas(result.Sketch!)
            {
                SupersampleScale = 1, // it's small anyway
                RecordHistory = false,
                ConnectsAudio = false, // never touch the live loops / OnNote() of the sketch running in the IDE
            };
            canvas.Crashed += (_, _) => Dispatcher.UIThread.Post(() =>
            {
                if (_canvas == canvas)
                    ShowNote(Loc.Tr("La vista previa se detuvo con un error. Abrilo para ver cuál.",
                                    "The preview stopped with an error. Open it to see which."));
            });

            _canvas = canvas;
            _box.Child = canvas;
            _note.IsVisible = false;
        }

        /// <summary>Stops the running preview, if any (its OnStop() runs).</summary>
        public void Stop()
        {
            _current = null;
            var canvas = _canvas;
            _canvas = null;
            _box.Child = null;
            canvas?.StopSketch();
        }

        private void ShowNote(string text)
        {
            _note.Text = text;
            _note.IsVisible = true;
        }
    }
}
