using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using DanaProcessing.AvaloniaHost;
using DanaProcessing.Ide.Localization;
using DanaProcessing.Ide.Theme;

namespace DanaProcessing.Ide
{
    /// <summary>
    /// The rewind bar under the canvas: pause/resume, one frame back/forward,
    /// "continue from this frame" (real rewind: the sketch's state goes back
    /// too) and a slider over the recorded frames (see AvaloniaSketchCanvas's
    /// Freeze/StepBack/StepForward/ScrubTo/ResumeFromHere and FrameHistory). Polls the canvas
    /// a few times a second rather than listening to every frame -- the count
    /// of recorded frames changes 60 times a second and nobody needs the label
    /// to.
    /// </summary>
    public sealed class TimelineBar : Border
    {
        private readonly AvaloniaSketchCanvas _canvas;
        private readonly Button _playPause;
        private readonly Button _back;
        private readonly Button _forward;
        private readonly Button _resumeHere;
        private readonly Slider _slider;
        private readonly TextBlock _label;
        private bool _updating;

        public TimelineBar(AvaloniaSketchCanvas canvas)
        {
            _canvas = canvas;
            Background = ClayTheme.SurfaceHigher;
            Padding = new Thickness(8, 4);

            _back = MakeButton("⏮", Loc.Tr("Un cuadro atrás", "One frame back"), () => _canvas.StepBack());
            _playPause = MakeButton("⏸", Loc.Tr("Pausar y rebobinar", "Pause and rewind"), TogglePlay);
            _forward = MakeButton("⏭", Loc.Tr("Un cuadro adelante (en el último, corre un Draw() real)", "One frame forward (on the newest, runs one real Draw())"), () => _canvas.StepForward());
            _resumeHere = MakeButton("⤺", Loc.Tr(
                "Seguir desde este cuadro: el sketch vuelve al estado que tenía acá (sus campos, su azar) y sigue corriendo desde aquí",
                "Continue from this frame: the sketch goes back to the state it had here (its fields, its randomness) and runs on from here"),
                () => _canvas.ResumeFromHere());
            _resumeHere.IsVisible = false;

            _slider = new Slider
            {
                Minimum = 0,
                Maximum = 0,
                MinWidth = 80,
                Margin = new Thickness(8, 0),
                VerticalAlignment = VerticalAlignment.Center,
                IsEnabled = false,
            };
            _slider.PropertyChanged += (_, e) =>
            {
                if (e.Property == Slider.ValueProperty && !_updating)
                    _canvas.ScrubTo((int)Math.Round(_slider.Value));
            };

            _label = new TextBlock
            {
                Foreground = ClayTheme.TextMuted,
                FontFamily = ClayTheme.FontBody,
                FontSize = 11,
                MinWidth = 120,
                VerticalAlignment = VerticalAlignment.Center,
            };

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 2,
                VerticalAlignment = VerticalAlignment.Center,
                Children = { _back, _playPause, _forward, _resumeHere },
            };

            var layout = new DockPanel();
            DockPanel.SetDock(buttons, Dock.Left);
            DockPanel.SetDock(_label, Dock.Right);
            layout.Children.Add(buttons);
            layout.Children.Add(_label);
            layout.Children.Add(_slider);
            Child = layout;

            _canvas.HistoryChanged += Refresh;
            var poll = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            poll.Tick += (_, _) => Refresh();
            poll.Start();
            Refresh();
        }

        private static Button MakeButton(string icon, string tip, Action action)
        {
            var button = new Button
            {
                Content = icon,
                Padding = new Thickness(8, 2),
                Background = Avalonia.Media.Brushes.Transparent,
                Foreground = ClayTheme.TextSecondary,
                VerticalAlignment = VerticalAlignment.Center,
            };
            ToolTip.SetTip(button, tip);
            button.Click += (_, _) => action();
            return button;
        }

        private void TogglePlay()
        {
            if (_canvas.IsFrozen)
                _canvas.Resume();
            else
                _canvas.Freeze();
        }

        /// <summary>What the bar shows for a given canvas state (kept pure for the tests).</summary>
        public readonly record struct View(bool Enabled, bool SliderEnabled, bool Frozen, int Maximum, int Index, string Label);

        public static View Describe(int count, bool frozen, bool stopped, int historyIndex, Func<int, int> frameCountAt)
        {
            if (stopped)
                return new View(false, false, false, Math.Max(0, count - 1), Math.Max(0, count - 1),
                    Loc.Tr("Detenido", "Stopped"));

            int index = frozen ? Math.Clamp(historyIndex, 0, Math.Max(0, count - 1)) : Math.Max(0, count - 1);
            string label;
            if (!frozen)
            {
                label = count == 0 ? "" : Loc.Tr($"{count} cuadros para rebobinar", $"{count} frames to rewind");
            }
            else
            {
                int frame = frameCountAt(index);
                int behind = Math.Max(0, count - 1 - index);
                label = behind == 0
                    ? Loc.Tr($"cuadro {frame} · pausado", $"frame {frame} · paused")
                    : Loc.Tr($"cuadro {frame} · {behind} atrás", $"frame {frame} · {behind} back");
            }
            return new View(true, count > 1, frozen, Math.Max(0, count - 1), index, label);
        }

        private void Refresh()
        {
            _updating = true;
            try
            {
                var view = Describe(_canvas.HistoryCount, _canvas.IsFrozen, !_canvas.CanRewind, _canvas.HistoryIndex, _canvas.HistoryFrameCount);

                _slider.Maximum = view.Maximum;
                _slider.Value = view.Index;
                _slider.IsEnabled = view.SliderEnabled;

                // Greyed out as a whole while the sketch is stopped: still
                // readable, but nothing in it takes clicks or focus.
                if (Child is Control content)
                {
                    content.Opacity = view.Enabled ? 1.0 : 0.45;
                    content.IsHitTestVisible = view.Enabled;
                }
                _back.Focusable = _playPause.Focusable = _forward.Focusable = view.Enabled;
                ToolTip.SetTip(this, view.Enabled ? null : Loc.Tr(
                    "El sketch está detenido: corrélo (Run) para poder pausar y rebobinar",
                    "The sketch is stopped: Run it to pause and rewind"));

                _playPause.Content = view.Frozen ? "▶" : "⏸";
                _resumeHere.IsVisible = view.Frozen;
                _resumeHere.IsEnabled = view.Frozen && _canvas.CanResumeFromHere;
                ToolTip.SetTip(_playPause, view.Frozen
                    ? Loc.Tr("Seguir corriendo", "Resume")
                    : Loc.Tr("Pausar y rebobinar", "Pause and rewind"));
                _label.Text = view.Label;
            }
            finally
            {
                _updating = false;
            }
        }
    }
}
