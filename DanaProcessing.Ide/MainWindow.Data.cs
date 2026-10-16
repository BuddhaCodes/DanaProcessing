using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using DanaProcessing.Ide.Localization;

namespace DanaProcessing.Ide
{
    /// <summary>
    /// The sketch folder and its data/ folder: the sketch learns where its .cs
    /// file is (see Sketch.Data.cs), and these menu items put files into
    /// data/ and open the folder.
    /// </summary>
    public partial class MainWindow
    {
        /// <summary>Folder of the active tab's file; null while it has never been saved.</summary>
        private string? ActiveSketchFolder =>
            _editorView.ActiveTab?.FilePath is { } path ? Path.GetDirectoryName(Path.GetFullPath(path)) : null;

        private Control[] BuildDataMenuItems() => new Control[]
        {
            BuildMenuItem("📎", Loc.Tr("Agregar archivo a data/...", "Add file to data/..."), null, () => _ = AddFilesToDataAsync()),
            BuildMenuItem("📁", Loc.Tr("Abrir la carpeta del sketch", "Open the sketch folder"), null, OpenSketchFolder),
        };

        private bool RequireSavedSketch()
        {
            if (ActiveSketchFolder != null)
                return true;
            ShowTransientStatus(Loc.Tr(
                "Guardá el sketch primero (Ctrl+S): la carpeta data/ vive al lado del archivo .cs",
                "Save the sketch first (Ctrl+S): the data/ folder lives next to the .cs file"));
            return false;
        }

        /// <summary>Copies the picked files into data/ (creating it) and reminds how to load them.</summary>
        private async Task AddFilesToDataAsync()
        {
            if (!RequireSavedSketch())
                return;
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.StorageProvider is null)
                return;
            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = Loc.Tr("Archivos para la carpeta data/", "Files for the data/ folder"),
                AllowMultiple = true,
            });
            if (files.Count == 0)
                return;

            var data = Path.Combine(ActiveSketchFolder!, "data");
            Directory.CreateDirectory(data);
            var names = new List<string>();
            foreach (var file in files)
            {
                var source = file.Path.LocalPath;
                var name = Path.GetFileName(source);
                File.Copy(source, Path.Combine(data, name), overwrite: true);
                names.Add(name);
            }
            ShowTransientStatus(Loc.Tr(
                $"En data/: {string.Join(", ", names)} -- cargalo con su nombre, p. ej. LoadImage(\"{names[0]}\")",
                $"In data/: {string.Join(", ", names)} -- load it by name, e.g. LoadImage(\"{names[0]}\")"));
        }

        private void OpenSketchFolder()
        {
            if (!RequireSavedSketch())
                return;
            try
            {
                Process.Start(new ProcessStartInfo(ActiveSketchFolder!) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                ShowTransientStatus(ex.Message);
            }
        }
    }
}
