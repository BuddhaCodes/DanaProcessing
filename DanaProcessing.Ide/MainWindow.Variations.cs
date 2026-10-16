using DanaProcessing.Ide.Editor;
using DanaProcessing.Ide.Localization;

namespace DanaProcessing.Ide
{
    /// <summary>Sketch > Variations sheet: see VariationsWindow / VariationRenderer.</summary>
    public partial class MainWindow
    {
        private VariationsWindow? _variationsWindow;

        private void OpenVariations()
        {
            if (_variationsWindow != null)
            {
                _variationsWindow.Activate();
                return;
            }

            int baseSeed = _seed != 0 ? _seed : Random.Shared.Next(1, 1_000_000);
            _variationsWindow = new VariationsWindow(LoadVariationSourceAsync, baseSeed, CurrentTweakLiterals, PickVariation);
            _variationsWindow.Closed += (_, _) => _variationsWindow = null;
            _variationsWindow.Show(this);
        }

        /// <summary>The active tab, compiled; each variation gets a fresh instance of it.</summary>
        private async Task<VariationsWindow.Source> LoadVariationSourceAsync()
        {
            var result = await ResolveAndCompileActiveSketchAsync();
            if (result is null)
                return new VariationsWindow.Source(null, new[] { Loc.Tr("No hay código para compilar (o falló NuGet)", "No code to compile (or NuGet failed)") });
            if (!result.Success)
                return new VariationsWindow.Source(null, result.Errors);
            var type = result.Sketch!.GetType();
            string? folder = ActiveSketchFolder;
            return new VariationsWindow.Source(() =>
            {
                var sketch = (Sketch)Activator.CreateInstance(type)!;
                sketch.SetSketchFolder(folder);
                return sketch;
            }, Array.Empty<string>());
        }

        /// <summary>The running sketch's [Tweak] values, when it's the active tab (what the sheet's first cell keeps).</summary>
        private IReadOnlyDictionary<string, string> CurrentTweakLiterals()
        {
            if (_runningTab == null || _runningTab != _editorView.ActiveTab)
                return new Dictionary<string, string>();
            return _canvas.ReadTweakValues().ToDictionary(v => v.FieldName, v => v.CSharpLiteral);
        }

        /// <summary>A cell was clicked: lock its seed, queue its [Tweak] values and run it in the IDE.</summary>
        private void PickVariation(VariationsWindow.Pick pick)
        {
            _seed = pick.Seed;
            _seedLocked = true;
            _useSeedOnce = true;
            _pendingTweakLiterals = pick.Tweaks.Count > 0 ? pick.Tweaks : null;
            UpdateSeedLockMenuItem();
            _ = RunCurrentSketchAsync();
        }
    }
}
