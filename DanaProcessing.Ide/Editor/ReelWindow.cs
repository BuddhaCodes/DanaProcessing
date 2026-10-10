using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using DanaProcessing.AvaloniaHost;
using DanaProcessing.Ide.Localization;
using DanaProcessing.Ide.Theme;
using DanaProcessing.Reels;

namespace DanaProcessing.Ide
{
    /// <summary>
    /// "Create reel": previews the video the sketch's <c>// @reel</c> markers
    /// describe (title, typed code cards, captions, the running sketch with a
    /// simulated user, outro) and exports it as MP4, GIF or PNG frames.
    ///
    /// Not modal: you keep editing the code, then press "Update from code".
    /// The preview is the real thing at a smaller size -- export renders the
    /// same timeline frame by frame at full resolution.
    /// </summary>
    public class ReelWindow : Window
    {
        /// <summary>What the IDE hands over: the code, and a way to make fresh instances of the compiled sketch (null when it didn't compile).</summary>
        public sealed record ReelSource(string Source, Func<Sketch>? SketchFactory, IReadOnlyList<string> Errors);

        /// <summary>The markers a sketch needs to become a reel -- inserted at the top of the code by "Add reel markers".</summary>
        public const string MarkerTemplate =
@"using DanaProcessing.Reels;

// @reel title: My sketch
// @reel subtitle: Creative coding in C# with DanaProcessing
// @reel format: vertical
// @reel result: 6

// Then, above each part of the code you want to show:
//   // @reel scene ""What this part does"" effect=type
// Lines between // @reel hide and // @reel show are left out of the video,
// and a line ending in // @mark is highlighted.
";

        private static readonly IBrush Hairline = new SolidColorBrush(Avalonia.Media.Color.Parse("#E8E2DA"));
        private static readonly IBrush WarnBrush = new SolidColorBrush(Avalonia.Media.Color.Parse("#B45309"));

        private readonly Func<Task<ReelSource>> _load;
        private readonly Action<string> _insertAtTop;

        private ReelScript? _script;
        private Func<Sketch>? _factory;
        private ReelTimeline? _timeline;
        private ReelTimeline? _previousTimeline;
        private ReelPlayerSketch? _player;

        private readonly Border _previewHost;
        private AvaloniaSketchCanvas? _canvas;
        private readonly Button _playButton;
        private readonly Slider _scrubber;
        private readonly TextBlock _timeText;
        private bool _settingScrubber;

        private readonly TextBlock _summary;
        private readonly StackPanel _parts;
        private readonly StackPanel _messages;
        private readonly Border _howTo;

        private readonly ComboBox _formatPicker;
        private readonly Button _exportButton;
        private readonly Button _cancelButton;
        private readonly ProgressBar _progress;
        private readonly TextBlock _exportStatus;
        private readonly Button _revealButton;
        private readonly TextBlock _ffmpegText;
        private readonly Button _ffmpegButton;
        private readonly Button _ffmpegLink;
        private readonly Button _reloadButton;

        private CancellationTokenSource? _exportCts;
        private string? _lastExport;
        private readonly DispatcherTimer _uiTimer;

        public ReelWindow(Func<Task<ReelSource>> loadFromEditor, Action<string> insertAtTop)
        {
            _load = loadFromEditor;
            _insertAtTop = insertAtTop;

            Title = Loc.Tr("Crear reel", "Create reel");
            Width = 1060;
            Height = 820;
            MinWidth = 820;
            MinHeight = 600;
            CanResize = true;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = ClayTheme.Base;
            Styles.AddRange(ClayTheme.ButtonEffectStyles());

            // ===== Left: preview + transport =====
            _previewHost = new Border
            {
                Background = new SolidColorBrush(Avalonia.Media.Color.Parse("#1A1512")),
                CornerRadius = ClayTheme.RadiusMedium,
                ClipToBounds = true,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };

            _playButton = new Button { Content = "❚❚", Classes = { "clay-icon" }, Width = 40, Height = 36, VerticalAlignment = VerticalAlignment.Center };
            _playButton.Click += (_, _) => TogglePlay();
            ToolTip.SetTip(_playButton, Loc.Tr("Reproducir / pausar", "Play / pause"));

            _scrubber = new Slider { Minimum = 0, Maximum = 1, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0) };
            _scrubber.PropertyChanged += (_, e) =>
            {
                if (e.Property == RangeBase.ValueProperty && !_settingScrubber)
                    _player?.Seek(_scrubber.Value);
            };

            _timeText = new TextBlock
            {
                FontFamily = ClayTheme.FontMono,
                FontSize = 12,
                Foreground = ClayTheme.TextSecondary,
                VerticalAlignment = VerticalAlignment.Center,
                MinWidth = 96,
                TextAlignment = TextAlignment.Right,
            };

            var transport = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Margin = new Thickness(0, 12, 0, 0) };
            Grid.SetColumn(_playButton, 0);
            Grid.SetColumn(_scrubber, 1);
            Grid.SetColumn(_timeText, 2);
            transport.Children.Add(_playButton);
            transport.Children.Add(_scrubber);
            transport.Children.Add(_timeText);

            var previewArea = new Grid { RowDefinitions = new RowDefinitions("*,Auto") };
            Grid.SetRow(_previewHost, 0);
            Grid.SetRow(transport, 1);
            previewArea.Children.Add(_previewHost);
            previewArea.Children.Add(transport);

            var previewCard = new Border
            {
                Background = ClayTheme.Surface,
                CornerRadius = ClayTheme.RadiusMedium,
                Padding = new Thickness(18),
                Child = previewArea,
            };

            // ===== Right: script, messages, export =====
            _summary = new TextBlock { FontFamily = ClayTheme.FontBody, FontSize = 13, Foreground = ClayTheme.TextSecondary, TextWrapping = TextWrapping.Wrap };
            _parts = new StackPanel { Spacing = 2, Margin = new Thickness(0, 8, 0, 0) };
            _messages = new StackPanel { Spacing = 4, Margin = new Thickness(0, 8, 0, 0) };

            _reloadButton = new Button { Content = Loc.Tr("↻  Actualizar desde el código", "↻  Update from code"), Classes = { "clay-secondary" }, HorizontalAlignment = HorizontalAlignment.Stretch };
            _reloadButton.Click += async (_, _) => await ReloadAsync();

            var insertButton = new Button { Content = Loc.Tr("Agregar marcas de reel al código", "Add reel markers to the code"), Classes = { "clay-run" }, Margin = new Thickness(0, 10, 0, 0) };
            insertButton.Click += async (_, _) =>
            {
                _insertAtTop(MarkerTemplate + Environment.NewLine);
                await ReloadAsync();
            };
            _howTo = new Border
            {
                Background = ClayTheme.SurfaceHigher,
                BorderBrush = Hairline,
                BorderThickness = new Thickness(1),
                CornerRadius = ClayTheme.RadiusSmall,
                Padding = new Thickness(14, 12),
                Margin = new Thickness(0, 10, 0, 0),
                IsVisible = false,
                Child = new StackPanel
                {
                    Children =
                    {
                        Heading(Loc.Tr("Cómo se arma un reel", "How a reel is made")),
                        Body(Loc.Tr(
                            "Importá el módulo con using DanaProcessing.Reels; y marcá el código con comentarios // @reel. Cada // @reel scene \"texto\" abre una tarjeta nueva (efectos: type, lines, fade, instant; transiciones: slide, fade, zoom, wipe). Al final se muestra el sketch funcionando; si usa el mouse, se simula un usuario (o lo guiás con move / drag / click / key).",
                            "Import the module with using DanaProcessing.Reels; and mark up the code with // @reel comments. Each // @reel scene \"text\" starts a new card (effects: type, lines, fade, instant; transitions: slide, fade, zoom, wipe). At the end the sketch runs; if it uses the mouse, a user is simulated (or you direct it with move / drag / click / key).")),
                        insertButton,
                    },
                },
            };

            _formatPicker = new ComboBox
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                ItemsSource = new[]
                {
                    Loc.Tr("MP4 (H.264) — para Reels, Shorts, TikTok", "MP4 (H.264) — for Reels, Shorts, TikTok"),
                    Loc.Tr("GIF animado (sin ffmpeg)", "Animated GIF (no ffmpeg needed)"),
                    Loc.Tr("Secuencia de PNG", "PNG sequence"),
                },
                SelectedIndex = 0,
            };
            _formatPicker.SelectionChanged += (_, _) => RefreshFfmpegStatus();

            _exportButton = new Button { Content = Loc.Tr("Exportar…", "Export…"), Classes = { "clay-run" } };
            _exportButton.Click += async (_, _) => await ExportAsync();
            _cancelButton = new Button { Content = Loc.Tr("Cancelar", "Cancel"), Classes = { "clay-secondary" }, IsVisible = false, Margin = new Thickness(8, 0, 0, 0) };
            _cancelButton.Click += (_, _) => _exportCts?.Cancel();

            _progress = new ProgressBar { Minimum = 0, Maximum = 1, Height = 6, IsVisible = false, Margin = new Thickness(0, 10, 0, 0) };
            _exportStatus = new TextBlock { FontFamily = ClayTheme.FontBody, FontSize = 12, Foreground = ClayTheme.TextSecondary, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0) };
            _revealButton = new Button { Content = Loc.Tr("Mostrar en la carpeta", "Show in folder"), Classes = { "clay-secondary" }, IsVisible = false, Margin = new Thickness(0, 8, 0, 0) };
            _revealButton.Click += (_, _) => Reveal(_lastExport);

            _ffmpegText = new TextBlock { FontFamily = ClayTheme.FontBody, FontSize = 11.5, Foreground = ClayTheme.TextMuted, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 0) };
            _ffmpegButton = new Button { Content = Loc.Tr("Elegir ffmpeg…", "Choose ffmpeg…"), Classes = { "clay-secondary" }, Margin = new Thickness(0, 6, 0, 0), IsVisible = false };
            _ffmpegButton.Click += async (_, _) => await PickFfmpegAsync();
            _ffmpegLink = new Button { Content = Loc.Tr("Descargar ffmpeg", "Download ffmpeg"), Classes = { "clay-secondary" }, Margin = new Thickness(8, 6, 0, 0), IsVisible = false };
            _ffmpegLink.Click += (_, _) =>
            {
                try { Process.Start(new ProcessStartInfo("https://www.gyan.dev/ffmpeg/builds/") { UseShellExecute = true }); } catch { }
            };

            var exportRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0), Children = { _exportButton, _cancelButton } };

            var side = new StackPanel
            {
                Children =
                {
                    Heading(Loc.Tr("Guion", "Script")),
                    _summary,
                    _parts,
                    _messages,
                    _howTo,
                    new Border { Height = 1, Background = Hairline, Margin = new Thickness(0, 16) },
                    Heading(Loc.Tr("Exportar", "Export")),
                    _formatPicker,
                    exportRow,
                    _progress,
                    _exportStatus,
                    _revealButton,
                    _ffmpegText,
                    new StackPanel { Orientation = Orientation.Horizontal, Children = { _ffmpegButton, _ffmpegLink } },
                },
            };

            var sideDock = new DockPanel();
            DockPanel.SetDock(_reloadButton, Dock.Top);
            sideDock.Children.Add(_reloadButton);
            sideDock.Children.Add(new ScrollViewer { Content = side, Margin = new Thickness(0, 12, 0, 0) });

            var sideCard = new Border
            {
                Background = ClayTheme.Surface,
                CornerRadius = ClayTheme.RadiusMedium,
                Padding = new Thickness(20, 18),
                Child = sideDock,
            };

            var root = new Grid { ColumnDefinitions = new ColumnDefinitions("*,12,360"), Margin = new Thickness(24, 16, 24, 20) };
            Grid.SetColumn(previewCard, 0);
            Grid.SetColumn(sideCard, 2);
            root.Children.Add(previewCard);
            root.Children.Add(sideCard);

            ClayChrome.Apply(this, root);

            _uiTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(66) };
            _uiTimer.Tick += (_, _) => SyncTransport();
            _uiTimer.Start();

            Opened += async (_, _) => await ReloadAsync();
            Closed += (_, _) =>
            {
                _uiTimer.Stop();
                _exportCts?.Cancel();
                if (_canvas != null)
                    _canvas.IsPaused = true;
            };

            RefreshFfmpegStatus();
        }

        private static TextBlock Heading(string text) => new()
        {
            Text = text,
            FontFamily = ClayTheme.FontDisplay,
            FontWeight = FontWeight.SemiBold,
            FontSize = 15,
            Foreground = ClayTheme.TextPrimary,
            Margin = new Thickness(0, 0, 0, 6),
        };

        private static TextBlock Body(string text) => new()
        {
            Text = text,
            FontFamily = ClayTheme.FontBody,
            FontSize = 12.5,
            LineHeight = 18,
            Foreground = ClayTheme.TextSecondary,
            TextWrapping = TextWrapping.Wrap,
        };

        // ------------------------------------------------------------------
        // Loading
        // ------------------------------------------------------------------

        private async Task ReloadAsync()
        {
            _reloadButton.IsEnabled = false;
            try
            {
                var src = await _load();
                _script = ReelScript.Parse(src.Source);
                _factory = src.SketchFactory;

                _messages.Children.Clear();
                foreach (var e in src.Errors.Take(6))
                    _messages.Children.Add(Message("✕ " + e, ClayTheme.DangerHover));
                if (src.Errors.Count > 0)
                    _messages.Children.Add(Message(Loc.Tr("El sketch no compila: el reel se muestra sin la parte del resultado.", "The sketch doesn't compile: the reel is shown without the result part."), ClayTheme.TextSecondary));
                foreach (var w in _script.Warnings.Take(12))
                    _messages.Children.Add(Message("⚠ " + w, WarnBrush));

                _howTo.IsVisible = !_script.ImportsModule || !_script.HasMarkers;

                BuildPlayer();
            }
            catch (Exception ex)
            {
                _messages.Children.Clear();
                _messages.Children.Add(Message("✕ " + ex.Message, ClayTheme.DangerHover));
            }
            finally
            {
                _reloadButton.IsEnabled = true;
            }
        }

        private static TextBlock Message(string text, IBrush brush) => new()
        {
            Text = text,
            FontFamily = ClayTheme.FontBody,
            FontSize = 12,
            Foreground = brush,
            TextWrapping = TextWrapping.Wrap,
        };

        private void BuildPlayer()
        {
            if (_script == null)
                return;

            // The timeline swapped out two reloads ago can't be drawing anymore.
            _previousTimeline?.Dispose();
            _previousTimeline = _timeline;

            _timeline = ReelTimeline.Build(_script, _factory);

            // Preview size: as big as fits comfortably, same aspect as the video.
            double maxW = 560, maxH = 620;
            float scale = (float)Math.Min(maxW / _timeline.Width, maxH / _timeline.Height);
            _player = new ReelPlayerSketch(_timeline, scale);

            if (_canvas == null)
            {
                _canvas = new AvaloniaSketchCanvas(_player);
                _previewHost.Child = _canvas;
            }
            else
            {
                _canvas.LoadSketch(_player);
                _canvas.IsPaused = false;
            }
            _playButton.Content = "❚❚";

            _settingScrubber = true;
            _scrubber.Maximum = Math.Max(0.1, _timeline.Duration);
            _scrubber.Value = 0;
            _settingScrubber = false;

            _summary.Text = $"{_script.Describe()} · {Fmt(_timeline.Duration)} · {_timeline.Width}×{_timeline.Height} · {_timeline.Fps} fps";

            _parts.Children.Clear();
            foreach (var (start, duration, label) in _timeline.Parts)
            {
                var row = new Button
                {
                    Classes = { "clay-menu-item" },
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Stretch,
                    Content = new Grid
                    {
                        ColumnDefinitions = new ColumnDefinitions("56,*,Auto"),
                        Children =
                        {
                            new TextBlock { Text = Fmt(start), FontFamily = ClayTheme.FontMono, FontSize = 11.5, Foreground = ClayTheme.TextMuted, VerticalAlignment = VerticalAlignment.Center },
                            Col(new TextBlock { Text = PartLabel(label), FontFamily = ClayTheme.FontBody, FontSize = 12.5, Foreground = ClayTheme.TextPrimary, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center }, 1),
                            Col(new TextBlock { Text = duration.ToString("0.0", CultureInfo.InvariantCulture) + " s", FontFamily = ClayTheme.FontMono, FontSize = 11, Foreground = ClayTheme.TextMuted, VerticalAlignment = VerticalAlignment.Center }, 2),
                        },
                    },
                };
                var at = start;
                row.Click += (_, _) => _player?.Seek(at + 0.01);
                _parts.Children.Add(row);
            }
        }

        private static Control Col(Control c, int col)
        {
            Grid.SetColumn(c, col);
            return c;
        }

        private static string PartLabel(string label) => label switch
        {
            "Title" => Loc.Tr("Título", "Title"),
            "Result" => Loc.Tr("Resultado (sketch en vivo)", "Result (live sketch)"),
            "Outro" => Loc.Tr("Cierre", "Outro"),
            _ when label.StartsWith("Scene ", StringComparison.Ordinal) => Loc.Tr("Escena ", "Scene ") + label.Substring(6),
            _ => label,
        };

        private static string Fmt(double seconds)
        {
            var ts = TimeSpan.FromSeconds(Math.Max(0, seconds));
            return $"{(int)ts.TotalMinutes}:{ts.Seconds:00}.{ts.Milliseconds / 100}";
        }

        // ------------------------------------------------------------------
        // Transport
        // ------------------------------------------------------------------

        private void TogglePlay()
        {
            if (_player == null)
                return;
            if (!_player.IsPlaying && _player.Time >= _player.Duration - 0.05)
                _player.Seek(0);
            _player.IsPlaying = !_player.IsPlaying;
            _playButton.Content = _player.IsPlaying ? "❚❚" : "▶";
        }

        private void SyncTransport()
        {
            if (_player == null)
                return;
            _settingScrubber = true;
            _scrubber.Value = Math.Min(_scrubber.Maximum, _player.Time);
            _settingScrubber = false;
            _timeText.Text = $"{Fmt(_player.Time)} / {Fmt(_player.Duration)}";
        }

        // ------------------------------------------------------------------
        // Export
        // ------------------------------------------------------------------

        private ReelExportKind SelectedKind => _formatPicker.SelectedIndex switch
        {
            1 => ReelExportKind.Gif,
            2 => ReelExportKind.Png,
            _ => ReelExportKind.Mp4,
        };

        private void RefreshFfmpegStatus()
        {
            var settings = ReelSettingsStore.Load();
            var ffmpeg = ReelExporter.FindFfmpeg(settings.FfmpegPath);
            bool needs = SelectedKind == ReelExportKind.Mp4;
            if (ffmpeg != null)
            {
                _ffmpegText.Text = "ffmpeg: " + ffmpeg;
                _ffmpegText.Foreground = ClayTheme.TextMuted;
                _ffmpegButton.IsVisible = false;
                _ffmpegLink.IsVisible = false;
            }
            else
            {
                _ffmpegText.Text = needs
                    ? Loc.Tr("Para exportar MP4 hace falta ffmpeg, y no está instalado. Descargalo con el botón de abajo (en Windows también: winget install Gyan.FFmpeg), y después reabrí esta ventana o elegí ffmpeg.exe. Mientras tanto, podés exportar como GIF.",
                             "MP4 export needs ffmpeg, and it isn't installed. Download it with the button below (on Windows also: winget install Gyan.FFmpeg), then reopen this window or pick ffmpeg.exe. Meanwhile, you can export as GIF.")
                    : Loc.Tr("ffmpeg no encontrado (solo hace falta para MP4).", "ffmpeg not found (only needed for MP4).");
                _ffmpegText.Foreground = needs ? WarnBrush : ClayTheme.TextMuted;
                _ffmpegButton.IsVisible = true;
                _ffmpegLink.IsVisible = true;
            }
            _exportButton.IsEnabled = _exportCts == null && (!needs || ffmpeg != null);
        }

        private async Task PickFfmpegAsync()
        {
            var top = TopLevel.GetTopLevel(this);
            if (top?.StorageProvider is null)
                return;
            var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = Loc.Tr("Elegí ffmpeg", "Choose ffmpeg"),
                AllowMultiple = false,
            });
            if (files.Count == 0)
                return;
            var settings = ReelSettingsStore.Load();
            settings.FfmpegPath = files[0].Path.LocalPath;
            ReelSettingsStore.Save(settings);
            RefreshFfmpegStatus();
        }

        private async Task ExportAsync()
        {
            if (_script == null || _exportCts != null)
                return;
            var top = TopLevel.GetTopLevel(this);
            if (top?.StorageProvider is null)
                return;

            var kind = SelectedKind;
            var slug = Slug(_script.Title ?? "reel");
            string? path;
            if (kind == ReelExportKind.Png)
            {
                var folders = await top.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
                {
                    Title = Loc.Tr("Carpeta para los fotogramas", "Folder for the frames"),
                    AllowMultiple = false,
                });
                if (folders.Count == 0)
                    return;
                path = Path.Combine(folders[0].Path.LocalPath, slug + "-frames");
            }
            else
            {
                var ext = kind == ReelExportKind.Gif ? "gif" : "mp4";
                var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                {
                    Title = Loc.Tr("Guardar reel", "Save reel"),
                    SuggestedFileName = slug + "." + ext,
                    DefaultExtension = ext,
                    FileTypeChoices = new[] { new FilePickerFileType(ext.ToUpperInvariant()) { Patterns = new[] { "*." + ext } } },
                });
                if (file is null)
                    return;
                path = file.Path.LocalPath;
            }

            var options = new ReelExportOptions
            {
                Kind = kind,
                OutputPath = path,
                FfmpegPath = ReelSettingsStore.Load().FfmpegPath,
            };

            _exportCts = new CancellationTokenSource();
            _exportButton.IsEnabled = false;
            _cancelButton.IsVisible = true;
            _revealButton.IsVisible = false;
            _progress.IsVisible = true;
            _progress.Value = 0;
            _exportStatus.Foreground = ClayTheme.TextSecondary;
            _exportStatus.Text = Loc.Tr("Renderizando…", "Rendering…");
            if (_player != null)
                _player.IsPlaying = false; // the preview and the export would fight over the CPU
            _playButton.Content = "▶";

            var sw = Stopwatch.StartNew();
            var progress = new Progress<double>(p =>
            {
                _progress.Value = p;
                _exportStatus.Text = Loc.Tr("Renderizando… ", "Rendering… ") + (p * 100).ToString("0", CultureInfo.InvariantCulture) + "%";
            });

            try
            {
                await ReelExporter.ExportAsync(_script, _factory, options, progress, _exportCts.Token);
                _lastExport = path;
                _exportStatus.Text = Loc.Tr("Listo en ", "Done in ") + sw.Elapsed.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture) + " s: " + path;
                _revealButton.IsVisible = true;
            }
            catch (OperationCanceledException)
            {
                _exportStatus.Text = Loc.Tr("Exportación cancelada.", "Export cancelled.");
            }
            catch (Exception ex)
            {
                _exportStatus.Foreground = ClayTheme.DangerHover;
                _exportStatus.Text = ex.Message;
            }
            finally
            {
                _exportCts.Dispose();
                _exportCts = null;
                _cancelButton.IsVisible = false;
                _progress.IsVisible = false;
                RefreshFfmpegStatus();
            }
        }

        private static string Slug(string text)
        {
            var chars = text.ToLowerInvariant().Normalize(System.Text.NormalizationForm.FormD)
                .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                .Select(c => char.IsLetterOrDigit(c) ? c : '-')
                .ToArray();
            var slug = string.Join("-", new string(chars).Split('-', StringSplitOptions.RemoveEmptyEntries));
            if (slug.Length > 48) slug = slug.Substring(0, 48).TrimEnd('-');
            return slug.Length == 0 ? "reel" : slug;
        }

        private static void Reveal(string? path)
        {
            if (string.IsNullOrEmpty(path))
                return;
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    if (File.Exists(path))
                        Process.Start("explorer.exe", $"/select,\"{path}\"");
                    else
                        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
                }
                else
                {
                    var folder = Directory.Exists(path) ? path : Path.GetDirectoryName(path);
                    if (folder != null)
                        Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
                }
            }
            catch
            {
                // Nothing useful to do if the shell refuses.
            }
        }
    }

    /// <summary>Remembered reel choices (just the ffmpeg path for now), next to the IDE's other settings.</summary>
    public sealed class ReelSettings
    {
        public string? FfmpegPath { get; set; }
    }

    public static class ReelSettingsStore
    {
        private static string FilePath
        {
            get
            {
                var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DanaProcessingIde");
                Directory.CreateDirectory(dir);
                return Path.Combine(dir, "reel-settings.json");
            }
        }

        public static ReelSettings Load()
        {
            try
            {
                return File.Exists(FilePath)
                    ? JsonSerializer.Deserialize<ReelSettings>(File.ReadAllText(FilePath)) ?? new ReelSettings()
                    : new ReelSettings();
            }
            catch
            {
                return new ReelSettings();
            }
        }

        public static void Save(ReelSettings settings)
        {
            try
            {
                File.WriteAllText(FilePath, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch
            {
                // Settings are a convenience; failing to save one is not worth an error dialog.
            }
        }
    }
}
