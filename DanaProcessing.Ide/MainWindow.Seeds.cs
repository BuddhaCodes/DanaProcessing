using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using DanaProcessing.Ide.Localization;
using DanaProcessing.Ide.Theme;

namespace DanaProcessing.Ide
{
    /// <summary>
    /// Seeds: every Run gives the sketch a seed (Random(), RandomGaussian() and
    /// Noise() follow it, and every PNG it saves records it). 🎲 runs again with
    /// a new one, 🔒 keeps the current one across Runs, and "Open seed from
    /// PNG..." brings back the seed -- and the [Tweak] values -- an image was
    /// made with.
    /// </summary>
    public partial class MainWindow
    {
        private int _seed;
        private bool _seedLocked;
        private bool _useSeedOnce;
        private IReadOnlyDictionary<string, string>? _pendingTweakLiterals;
        private Button? _seedLockMenuItem;

        /// <summary>The seed the next Run uses: the same one while locked (or once after 🎲 / a PNG), a fresh one otherwise.</summary>
        private int SeedForRun()
        {
            if (_useSeedOnce)
                _useSeedOnce = false;
            else if (!_seedLocked || _seed == 0)
                _seed = Random.Shared.Next(1, 1_000_000);
            return _seed;
        }

        /// <summary>What Run does with a freshly compiled sketch: seed it, put back any [Tweak] values waiting (from a PNG or a picked variation), load it.</summary>
        private void LoadWithSeed(Sketch sketch, bool keepCurrentSeed = false)
        {
            if (keepCurrentSeed && _seed != 0)
                _useSeedOnce = true;
            sketch.ApplySeed(SeedForRun());
            sketch.SetSketchFolder(ActiveSketchFolder); // data/ lives next to the .cs file

            // Before Setup(), exactly like the variations sheet renders them, so
            // a Setup() that builds the scene from these values sees them too.
            var tweaks = _pendingTweakLiterals;
            _pendingTweakLiterals = null;
            if (tweaks != null)
                sketch.ApplyTweakLiterals(tweaks);
            _canvas.LoadSketch(sketch);
        }

        private string SeedSuffix() =>
            _seed == 0 ? "" : $"  ·  {Loc.Tr("semilla", "seed")} {_seed}{(_seedLocked ? " 🔒" : "")}";

        private Button BuildSeedButton()
        {
            var button = new Button
            {
                Content = "🎲",
                Classes = { "clay-secondary" },
                Padding = new Avalonia.Thickness(10, 7),
                Margin = new Avalonia.Thickness(6, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
            };
            ToolTip.SetTip(button, Loc.Tr(
                "Nueva semilla y ejecutar (F6): Random() y Noise() cambian, el código no",
                "New seed and run (F6): Random() and Noise() change, the code doesn't"));
            button.Click += (_, _) => RerollAndRun();
            return button;
        }

        /// <summary>🎲: a new seed, then Run. If the seed is locked, it stays locked -- on the new one.</summary>
        private void RerollAndRun()
        {
            if (!_runButton.IsEnabled)
                return;
            _seed = Random.Shared.Next(1, 1_000_000);
            _useSeedOnce = true;
            _ = RunCurrentSketchAsync();
        }

        private void ToggleSeedLock()
        {
            _seedLocked = !_seedLocked;
            UpdateSeedLockMenuItem();
            ShowTransientStatus(_seedLocked
                ? Loc.Tr($"🔒 Semilla {_seed} fija: cada Run dibuja lo mismo hasta que la sueltes o tires 🎲",
                         $"🔒 Seed {_seed} locked: every Run draws the same until you unlock it or roll 🎲")
                : Loc.Tr("🔓 Semilla libre: cada Run usa una nueva", "🔓 Seed unlocked: every Run uses a new one"));
        }

        private void UpdateSeedLockMenuItem()
        {
            if (_seedLockMenuItem?.Content is Grid row && row.Children.Count > 1 && row.Children[1] is TextBlock label && row.Children[0] is TextBlock icon)
            {
                icon.Text = _seedLocked ? "🔒" : "🔓";
                label.Text = _seedLocked
                    ? Loc.Tr("Soltar la semilla", "Unlock the seed")
                    : Loc.Tr("Mantener la semilla entre Runs", "Keep the seed between Runs");
            }
        }

        private Control[] BuildSeedMenuItems()
        {
            _seedLockMenuItem = BuildMenuItem("🔓", "", null, ToggleSeedLock);
            UpdateSeedLockMenuItem();
            return new Control[]
            {
                MirrorEnabled(BuildMenuItem("🎲", Loc.Tr("Nueva semilla y ejecutar", "New seed and run"), "F6", RerollAndRun), _runButton),
                _seedLockMenuItem,
                BuildMenuItem("🖼", Loc.Tr("Abrir semilla desde PNG...", "Open seed from PNG..."), null, () => _ = OpenSeedFromPngAsync()),
                BuildMenuItem("▦", Loc.Tr("Hoja de variaciones...", "Variations sheet..."), "Ctrl+Shift+V", OpenVariations),
            };
        }

        /// <summary>
        /// Reads the seed (and [Tweak] values) a PNG saved by a sketch carries,
        /// locks that seed and runs the active sketch with it -- the same image
        /// again, ready to keep exploring from.
        /// </summary>
        private async Task OpenSeedFromPngAsync()
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.StorageProvider is null)
                return;
            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = Loc.Tr("Elegí un PNG guardado por un sketch", "Pick a PNG saved by a sketch"),
                AllowMultiple = false,
                FileTypeFilter = new[] { new FilePickerFileType("PNG") { Patterns = new[] { "*.png" } } },
            });
            if (files.Count == 0)
                return;

            SketchImageInfo? info;
            try
            {
                info = SketchImageInfo.Read(files[0].Path.LocalPath);
            }
            catch (Exception ex)
            {
                ShowTransientStatus(Loc.Tr($"No se pudo leer la imagen: {ex.Message}", $"Couldn't read the image: {ex.Message}"));
                return;
            }

            if (info?.Seed is not int seed)
            {
                ShowTransientStatus(Loc.Tr(
                    "Esa imagen no tiene semilla: solo los PNG que guarda un sketch (Save/SaveFrame) la llevan",
                    "That image has no seed: only PNGs saved by a sketch (Save/SaveFrame) carry one"));
                return;
            }

            _seed = seed;
            _seedLocked = true;
            _useSeedOnce = true;
            _pendingTweakLiterals = info.Tweaks.Count > 0 ? info.Tweaks : null;
            UpdateSeedLockMenuItem();

            string tweaks = info.Tweaks.Count > 0
                ? Loc.Tr($" y {info.Tweaks.Count} valor(es) de [Tweak]", $" and {info.Tweaks.Count} [Tweak] value(s)")
                : "";
            ShowTransientStatus(Loc.Tr(
                $"🔒 Semilla {seed}{tweaks} de '{info.Sketch}' (frame {info.Frame}) -- ejecutando",
                $"🔒 Seed {seed}{tweaks} from '{info.Sketch}' (frame {info.Frame}) -- running"));
            await RunCurrentSketchAsync();
        }
    }
}
