using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using DanaProcessing;
using SkiaSharp;

namespace DanaProcessing.AvaloniaHost.Attach
{
    /// <summary>
    /// An attached sketch as a tray app: no window of its own at all, just an
    /// icon in the notification area whose menu says what the sketch is doing
    /// and can pause it, install the PowerShell snippet, or quit. This is how
    /// an exported AttachedSketch runs (see the IDE's ExportedSketchRunner).
    /// </summary>
    public sealed class AttachedSketchTray : IDisposable
    {
        private readonly AttachedSketchFleet _fleet;
        private readonly Action _quit;
        private readonly string _title;
        private readonly TrayIcon _icon;
        private readonly NativeMenuItem _status;
        private readonly NativeMenuItem _pause;
        private bool _disposed;

        /// <param name="sketch">The sketch to run (Settings()/Setup() not run yet).</param>
        /// <param name="title">Shown in the tooltip and at the top of the menu (the sketch's name).</param>
        /// <param name="quit">What "Quit" does after the sketch stopped (shut the app down).</param>
        /// <param name="overrides">See AttachedSketchFleet: what the export forces (one sketch per terminal, its own terminal profile).</param>
        public AttachedSketchTray(AttachedSketch sketch, string title, Action quit, AttachedRunOverrides? overrides = null)
        {
            _title = title;
            _quit = quit;
            _fleet = new AttachedSketchFleet(sketch, overrides: overrides);

            var header = new NativeMenuItem(title) { IsEnabled = false };
            _status = new NativeMenuItem(UiText.Tr("Iniciando…", "Starting…")) { IsEnabled = false };
            _pause = new NativeMenuItem(UiText.Tr("Pausar", "Pause"));
            _pause.Click += (_, _) => TogglePause();
            var install = new NativeMenuItem(UiText.Tr("Instalar señales de PowerShell", "Install PowerShell signals"));
            install.Click += (_, _) => InstallShellSignals();
            var test = new NativeMenuItem(UiText.Tr("Probar: comando fallido", "Test: failed command"));
            test.Click += (_, _) =>
            {
                try { ShellSignals.SendTest(new ShellSignal(ShellSignalKind.Error, 1, "", 0)); }
                catch (Exception ex) { _status.Header = UiText.Tr("No se pudo enviar la señal: ", "Couldn't send the signal: ") + ex.Message; }
            };
            var exit = new NativeMenuItem(UiText.Tr("Salir", "Quit"));
            exit.Click += (_, _) => Quit();

            var menu = new NativeMenu();
            menu.Items.Add(header);
            menu.Items.Add(_status);
            menu.Items.Add(new NativeMenuItemSeparator());
            menu.Items.Add(_pause);
            menu.Items.Add(install);
            menu.Items.Add(test);
            menu.Items.Add(new NativeMenuItemSeparator());
            menu.Items.Add(exit);

            _icon = new TrayIcon
            {
                Icon = MakeIcon(),
                ToolTipText = title,
                Menu = menu,
                IsVisible = true,
            };
            _icon.Clicked += (_, _) => TogglePause();
            if (Application.Current is { } app)
                TrayIcon.SetIcons(app, new TrayIcons { _icon });

            _fleet.StatusChanged += () => Dispatcher.UIThread.Post(Refresh);
            _fleet.ExitRequested += () => Dispatcher.UIThread.Post(Quit);
            _fleet.Start();
            Refresh();
        }

        /// <summary>The fleet running the sketch (one host, or one per window).</summary>
        public AttachedSketchFleet Fleet => _fleet;

        /// <summary>Tray tooltip for a state, kept short (Windows cuts tooltips at 127 characters).</summary>
        public static string Tooltip(string title, AttachState state, string status)
        {
            string text = state == AttachState.Attached ? title : $"{title} — {status}";
            return text.Length <= 120 ? text : text[..117] + "…";
        }

        private void Refresh()
        {
            if (_disposed)
                return;
            _status.Header = _fleet.StatusText;
            _pause.Header = _fleet.IsPaused ? UiText.Tr("Reanudar", "Resume") : UiText.Tr("Pausar", "Pause");
            _icon.ToolTipText = Tooltip(_title, _fleet.State, _fleet.StatusText);
        }

        private void TogglePause()
        {
            _fleet.IsPaused = !_fleet.IsPaused;
            Refresh();
        }

        private void InstallShellSignals()
        {
            _status.Header = UiText.Tr("Instalando señales de PowerShell…", "Installing PowerShell signals…");
            Task.Run(() =>
            {
                string text;
                try
                {
                    var changed = ShellSignals.InstallForPowerShell(); // asks powershell.exe for $PROFILE: not on the UI thread
                    text = changed.Count > 0
                        ? UiText.Tr("Señales instaladas: abre una terminal nueva.", "Signals installed: open a new terminal.")
                        : UiText.Tr("Las señales ya estaban instaladas.", "The signals were already installed.");
                }
                catch (Exception ex)
                {
                    DanaLogger.ErrorFromException(ex, "Instalando señales de PowerShell");
                    text = "No se pudieron instalar: " + ex.Message;
                }
                Dispatcher.UIThread.Post(() => { if (!_disposed) _status.Header = text; });
            });
        }

        private void Quit()
        {
            Dispose();
            _quit();
        }

        /// <summary>Stops the sketch (its OnStop() runs) and removes the icon.</summary>
        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _fleet.Stop();
            try { _fleet.Sketch.RaiseStopped(); } catch (Exception ex) { DanaLogger.ErrorFromException(ex, "Error en OnStop()"); }
            _icon.IsVisible = false;
            _icon.Dispose();
        }

        /// <summary>A small water drop, drawn with Skia (no image file to ship).</summary>
        internal static WindowIcon MakeIcon()
        {
            using var surface = SKSurface.Create(new SKImageInfo(32, 32, SKColorType.Rgba8888, SKAlphaType.Premul))!;
            var c = surface.Canvas;
            c.Clear(SKColors.Transparent);
            using var path = new SKPath();
            path.MoveTo(16, 2);
            path.CubicTo(16, 2, 5, 15, 5, 21);
            path.ArcTo(new SKRect(5, 10, 27, 31), 180, -180, false);
            path.CubicTo(27, 15, 16, 2, 16, 2);
            path.Close();
            using var fill = new SKPaint
            {
                IsAntialias = true,
                Shader = SKShader.CreateLinearGradient(new SKPoint(0, 2), new SKPoint(0, 31),
                    new[] { new SKColor(155, 240, 255), new SKColor(26, 92, 156) }, SKShaderTileMode.Clamp),
            };
            c.DrawPath(path, fill);
            using var shine = new SKPaint { IsAntialias = true, Color = new SKColor(255, 255, 255, 170) };
            c.DrawOval(new SKRect(10, 17, 14, 23), shine);
            using var image = surface.Snapshot();
            using var png = image.Encode(SKEncodedImageFormat.Png, 100);
            return new WindowIcon(new MemoryStream(png.ToArray()));
        }
    }
}
