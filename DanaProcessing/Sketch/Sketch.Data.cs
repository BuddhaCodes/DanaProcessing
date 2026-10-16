namespace DanaProcessing
{
    public abstract partial class Sketch
    {
        // =====================================================================
        // The sketch folder and its data/ folder -- Processing's convention:
        // images, fonts, CSV, sounds and shaders go in a "data" folder next to
        // the sketch's .cs file, and LoadImage("cat.png") finds them there.
        // The IDE tells the sketch where its file is; an exported app keeps a
        // copy of data/ next to itself, so the same relative paths work in both.
        //
        // Every Load*/Create*Reader/LoadShape/LoadSample with a relative path
        // looks in data/ first, then in the sketch folder, then where the
        // program runs; Save*/SaveFrame/Create*Writer with a relative path
        // write into the sketch folder (Processing does the same).
        // =====================================================================

        private string? _sketchFolder;

        /// <summary>
        /// Folder of the sketch's .cs file (the exported app's folder, once
        /// exported). Null for a sketch that was never saved -- then relative
        /// paths are relative to wherever the program runs.
        /// </summary>
        public string? SketchFolder => _sketchFolder;

        /// <summary>Host hook: the IDE (from the tab's file) and exported apps (their own folder).</summary>
        internal void SetSketchFolder(string? folder) =>
            _sketchFolder = string.IsNullOrWhiteSpace(folder) ? null : Path.GetFullPath(folder);

        /// <summary>The data/ folder next to the sketch (it may not exist yet).</summary>
        public string DataFolder => Path.Combine(_sketchFolder ?? Directory.GetCurrentDirectory(), "data");

        /// <summary>Full path of a file in the data/ folder, like Processing's dataPath().</summary>
        public string DataPath(string name) => Path.Combine(DataFolder, name);

        /// <summary>Full path of a file in the sketch folder, like Processing's sketchPath().</summary>
        public string SketchPath(string name = "") => Path.Combine(_sketchFolder ?? Directory.GetCurrentDirectory(), name);

        /// <summary>
        /// Where a relative path to READ points: data/ first, then the sketch
        /// folder, then the working directory. Absolute paths and URLs are
        /// left alone; a file found nowhere comes back unchanged (so the error
        /// names what the sketch asked for).
        /// </summary>
        internal override string ResolveInputPath(string path)
        {
            if (string.IsNullOrEmpty(path) || Path.IsPathRooted(path) || path.Contains("://"))
                return path;
            if (_sketchFolder != null)
            {
                var inData = Path.Combine(_sketchFolder, "data", path);
                if (File.Exists(inData) || Directory.Exists(inData))
                    return inData;
                var inSketch = Path.Combine(_sketchFolder, path);
                if (File.Exists(inSketch) || Directory.Exists(inSketch))
                    return inSketch;
            }
            return path;
        }

        /// <summary>Where a relative path to WRITE goes: the sketch folder (the working directory for an unsaved sketch).</summary>
        internal override string ResolveOutputPath(string path)
        {
            if (string.IsNullOrEmpty(path) || Path.IsPathRooted(path) || _sketchFolder == null)
                return path;
            return Path.Combine(_sketchFolder, path);
        }
    }
}
