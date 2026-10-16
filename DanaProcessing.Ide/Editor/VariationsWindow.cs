using System.IO;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using DanaProcessing.Ide.Localization;
using DanaProcessing.Ide.Theme;
using SkiaSharp;

namespace DanaProcessing.Ide.Editor
{
    /// <summary>
    /// The variations sheet: the active sketch drawn 9 or 16 times side by side,
    /// each with its own seed and/or its own [Tweak] values (see
    /// VariationRenderer). Click the one you like: the IDE locks its seed, puts
    /// its [Tweak] values back and runs it -- then "Apply tweaks to code" keeps
    /// them for good.
    /// </summary>
    public sealed class VariationsWindow : Window
    {
        /// <summary>What a click on a cell hands back to the IDE.</summary>
        public sealed record Pick(int Seed, IReadOnlyDictionary<string, string> Tweaks);

        /// <summary>A compiled sketch to explore (Factory null + Errors when it doesn't compile).</summary>
        public sealed record Source(Func<Sketch>? Factory, IReadOnlyList<string> Errors);

        private readonly Func<Task<Source>> _load;
        private readonly Func<IReadOnlyDictionary<string, string>> _currentTweaks;
        private readonly Action<Pick> _onPick;
        private int _baseSeed;

        private readonly ComboBox _size;
        private readonly ComboBox _mode;
        private readonly ComboBox _duration;
        private readonly Button _generate;
        private readonly TextBlock _status;
        private readonly UniformGrid _grid;
        private CancellationTokenSource? _cts;

        public VariationsWindow(Func<Task<Source>> load, int baseSeed,
                                Func<IReadOnlyDictionary<string, string>> currentTweaks, Action<Pick> onPick)
        {
            _load = load;
            _baseSeed = baseSeed;
            _currentTweaks = currentTweaks;
            _onPick = onPick;

            Title = Loc.Tr("Hoja de variaciones", "Variations sheet");
            Width = 1040;
            Height = 820;
            Background = ClayTheme.Base;
            Styles.AddRange(ClayTheme.ButtonEffectStyles());

            _size = Combo(new[] { "3 × 3", "4 × 4" }, 0);
            _mode = Combo(new[]
            {
                Loc.Tr("Semillas distintas", "Different seeds"),
                Loc.Tr("Valores de [Tweak] distintos", "Different [Tweak] values"),
                Loc.Tr("Las dos cosas", "Both"),
            }, 0);
            _duration = Combo(new[]
            {
                Loc.Tr("Primer frame", "First frame"),
                Loc.Tr("Después de 1 s", "After 1 s"),
                Loc.Tr("Después de 3 s", "After 3 s"),
            }, 1);

            _generate = new Button
            {
                Content = Loc.Tr("Generar", "Generate"),
                Classes = { "clay-run" },
                Padding = new Thickness(16, 8),
            };
            _generate.Click += (_, _) => _ = GenerateAsync(newSeed: false);

            var reroll = new Button
            {
                Content = "🎲",
                Classes = { "clay-secondary" },
                Padding = new Thickness(12, 8),
            };
            ToolTip.SetTip(reroll, Loc.Tr("Otra tanda de semillas", "Another batch of seeds"));
            reroll.Click += (_, _) => _ = GenerateAsync(newSeed: true);

            _status = new TextBlock
            {
                Foreground = ClayTheme.TextMuted,
                FontFamily = ClayTheme.FontBody,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(8, 0, 0, 0),
            };

            var toolbar = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Margin = new Thickness(20, 14, 20, 10),
                Children = { _size, _mode, _duration, _generate, reroll, _status },
            };

            _grid = new UniformGrid { Margin = new Thickness(14, 0, 14, 14) };
            var root = new DockPanel { LastChildFill = true };
            DockPanel.SetDock(toolbar, Dock.Top);
            root.Children.Add(toolbar);
            root.Children.Add(new ScrollViewer { Content = _grid });

            ClayChrome.Apply(this, root);

            Opened += (_, _) => _ = GenerateAsync(newSeed: false);
            Closed += (_, _) => _cts?.Cancel();
        }

        private static ComboBox Combo(string[] items, int selected) => new()
        {
            ItemsSource = items,
            SelectedIndex = selected,
            MinWidth = 120,
            FontFamily = ClayTheme.FontBody,
            FontSize = 12.5,
            VerticalAlignment = VerticalAlignment.Center,
        };

        private async Task GenerateAsync(bool newSeed)
        {
            _cts?.Cancel();
            var cts = _cts = new CancellationTokenSource();

            if (newSeed)
                _baseSeed = Random.Shared.Next(1, 1_000_000);

            int side = _size.SelectedIndex == 1 ? 4 : 3;
            var mode = (VariationMode)Math.Max(0, _mode.SelectedIndex);
            int seconds = _duration.SelectedIndex switch { 0 => 0, 2 => 3, _ => 1 };

            _status.Text = Loc.Tr("Compilando...", "Compiling...");
            _generate.IsEnabled = false;
            Source source;
            try
            {
                source = await _load();
            }
            finally
            {
                _generate.IsEnabled = true;
            }
            if (source.Factory is not { } factory)
            {
                _status.Text = source.Errors.Count > 0 ? source.Errors[0] : Loc.Tr("No hay sketch para explorar", "No sketch to explore");
                return;
            }

            var current = _currentTweaks();
            var cells = new Border[side * side];
            _grid.Children.Clear();
            _grid.Columns = side;
            _grid.Rows = side;
            for (int i = 0; i < cells.Length; i++)
            {
                cells[i] = new Border
                {
                    Background = ClayTheme.Surface,
                    CornerRadius = ClayTheme.RadiusMedium,
                    Margin = new Thickness(6),
                    MinHeight = 120,
                    Child = new TextBlock
                    {
                        Text = "…",
                        Foreground = ClayTheme.TextMuted,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                    },
                };
                _grid.Children.Add(cells[i]);
            }

            int done = 0;
            _status.Text = Loc.Tr("Dibujando variaciones...", "Drawing variations...");
            try
            {
                await Task.Run(() =>
                {
                    var plan = VariationRenderer.Plan(factory, cells.Length, mode, _baseSeed, current);
                    int frames = Math.Max(1, seconds * 60);
                    var options = new ParallelOptions
                    {
                        CancellationToken = cts.Token,
                        MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount / 2),
                    };
                    Parallel.For(0, plan.Count, options, i =>
                    {
                        var v = VariationRenderer.Render(factory, i, plan[i].Seed, plan[i].Tweaks, frames, 360, cts.Token);
                        var png = EncodePng(v.Thumbnail);
                        v.Thumbnail?.Dispose();
                        Dispatcher.UIThread.Post(() =>
                        {
                            if (cts.IsCancellationRequested)
                                return;
                            cells[i].Child = BuildCell(v, png, isCurrent: i == 0);
                            done++;
                            _status.Text = done == plan.Count
                                ? Loc.Tr("Hacé clic en la que más te guste: se ejecuta con su semilla y sus valores",
                                         "Click the one you like best: it runs with its seed and values")
                                : Loc.Tr($"Dibujando... {done}/{plan.Count}", $"Drawing... {done}/{plan.Count}");
                        });
                    });
                }, cts.Token);
            }
            catch (OperationCanceledException)
            {
                // Regenerated or closed: the newer request owns the grid now.
            }
        }

        private static byte[]? EncodePng(SKImage? image)
        {
            if (image == null)
                return null;
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            return data?.ToArray();
        }

        private Control BuildCell(Variation v, byte[]? png, bool isCurrent)
        {
            var caption = new TextBlock
            {
                Text = (isCurrent ? Loc.Tr("★ actual  ·  ", "★ current  ·  ") : "") + Loc.Tr($"semilla {v.Seed}", $"seed {v.Seed}"),
                Foreground = ClayTheme.TextSecondary,
                FontFamily = ClayTheme.FontBody,
                FontSize = 11.5,
                Margin = new Thickness(4, 6, 4, 0),
                TextTrimming = TextTrimming.CharacterEllipsis,
            };
            if (v.Tweaks.Count > 0 && !isCurrent)
                ToolTip.SetTip(caption, string.Join("\n", v.Tweaks.Select(t => $"{t.Key} = {t.Value}")));

            Control picture;
            if (png != null)
            {
                using var stream = new MemoryStream(png);
                picture = new Image { Source = new Bitmap(stream), Stretch = Stretch.Uniform };
            }
            else
            {
                picture = new TextBlock
                {
                    Text = v.Error ?? "?",
                    Foreground = ClayTheme.Danger,
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 11.5,
                    Margin = new Thickness(8),
                };
            }

            var panel = new DockPanel();
            DockPanel.SetDock(caption, Dock.Bottom);
            panel.Children.Add(caption);
            panel.Children.Add(picture);

            var button = new Button
            {
                Content = panel,
                Classes = { "clay-secondary" },
                Padding = new Thickness(6),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Stretch,
                IsEnabled = v.Error == null,
            };
            button.Click += (_, _) =>
            {
                _onPick(new Pick(v.Seed, v.Tweaks));
                _status.Text = Loc.Tr($"Ejecutando la semilla {v.Seed}...", $"Running seed {v.Seed}...");
            };
            return button;
        }
    }
}
