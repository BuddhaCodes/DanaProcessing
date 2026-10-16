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
    /// The rewind bar under the canvas: pause/resume, one frame back/forward
    /// and a slider over the recorded frames (see AvaloniaSketchCanvas's
    /// Freeze/StepBack/StepForward/ScrubTo and FrameHistory). Polls the canvas
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
                Children = { _back, _playPause, _forward },
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

        private void Refresh()
        {
            _updating = true;
            try
            {
                int count = _canvas.HistoryCount;
                bool frozen = _canvas.IsFrozen;
                int index = frozen ? Math.Clamp(_canvas.HistoryIndex, 0, Math.Max(0, count - 1)) : Math.Max(0, count - 1);

                _slider.Maximum = Math.Max(0, count - 1);
                _slider.Value = index;
                _slider.IsEnabled = count > 1;

                _playPause.Content = frozen ? "▶" : "⏸";
                ToolTip.SetTip(_playPause, frozen
                    ? Loc.Tr("Seguir corriendo", "Resume")
                    : Loc.Tr("Pausar y rebobinar", "Pause and rewind"));

                if (!frozen)
                {
                    _label.Text = count == 0
                        ? ""
                        : Loc.Tr($"{count} cuadros para rebobinar", $"{count} frames to rewind");
                }
                else
                {
                    int frame = _canvas.HistoryFrameCount(index);
                    int behind = Math.Max(0, count - 1 - index);
                    _label.Text = behind == 0
                        ? Loc.Tr($"cuadro {frame} · pausado", $"frame {frame} · paused")
                        : Loc.Tr($"cuadro {frame} · {behind} atrás", $"frame {frame} · {behind} back");
                }
            }
            finally
            {
                _updating = false;
            }
        }
    }
}
