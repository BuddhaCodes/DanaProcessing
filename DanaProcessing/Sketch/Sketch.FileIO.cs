using System;
using System.IO;

namespace DanaProcessing
{
    public abstract partial class Sketch
    {
        // =====================================================================
        // Files — https://processing.org/reference/createWriter_.html and
        // siblings (createReader/createInput/createOutput). LoadStrings/
        // SaveStrings/LoadBytes/SaveBytes (in Sketch.cs) already cover reading
        // or writing a file in one shot; these are for incremental/streaming
        // access instead — a long-running log file, or a large input read a
        // line/chunk at a time.
        // =====================================================================

        /// <summary>Opens a file for incremental text writing, like Processing's createWriter(path). Creates the containing directory if needed. Remember to Flush()/Close() the returned PrintWriter.</summary>
        public PrintWriter CreateWriter(string path)
        {
            var dir = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            return new PrintWriter(new StreamWriter(path, append: false));
        }

        /// <summary>Opens a text file for line-by-line reading, like Processing's createReader(path). Throws if the file doesn't exist.</summary>
        public BufferedReader CreateReader(string path) => new BufferedReader(new StreamReader(path));

        /// <summary>Opens a raw output byte stream to a file, like Processing's createOutput(path). Creates the containing directory if needed. The caller owns the returned Stream and should Dispose()/Close() it when done.</summary>
        public Stream CreateOutput(string path)
        {
            var dir = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            return File.Create(path);
        }

        /// <summary>Opens a raw input byte stream from a file, like Processing's createInput(path). Throws if the file doesn't exist.</summary>
        public Stream CreateInput(string path) => File.OpenRead(path);

        /// <summary>Opens a raw output byte stream to a file, like Processing's single-argument saveStream(filename) — an alias of CreateOutput() under Processing's other name for the same thing.</summary>
        public Stream SaveStream(string path) => CreateOutput(path);

        /// <summary>Copies the entire contents of `input` to a new file at `path` and closes both, like Processing's two-argument saveStream(filename, input) — https://processing.org/reference/saveStream_.html. Handy when you already have an input stream (e.g. from CreateInput() on another file, or a network/download stream) and just want it saved whole, without reading it into memory by hand first.</summary>
        public void SaveStream(string path, Stream input)
        {
            using var output = CreateOutput(path);
            input.CopyTo(output);
        }

        /// <summary>Opens a file, folder, URL, or application with the OS's default handler, like Processing's launch() — https://processing.org/reference/launch_.html. Fire-and-forget: returns as soon as the OS has been asked to open it, same as Processing's version.</summary>
        public void Launch(string path) =>
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });

        // =====================================================================
        // Output — https://processing.org/reference/printArray_.html.
        // Print()/Println() live in Sketch.cs.
        // =====================================================================

        /// <summary>Prints every element of an array, one per line, each prefixed with its index in square brackets — like Processing's printArray(). Works with any array, matching Processing's own overload-per-type version.</summary>
        public void PrintArray(Array array)
        {
            int width = Math.Max(1, (array.Length - 1).ToString().Length);
            for (int i = 0; i < array.Length; i++)
                Println($"[{i.ToString().PadLeft(width)}] {array.GetValue(i)}");
        }
    }
}
