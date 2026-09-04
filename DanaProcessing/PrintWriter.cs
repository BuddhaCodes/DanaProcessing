using System;
using System.IO;

namespace DanaProcessing
{
    /// <summary>
    /// A simple text-file writer, equivalent to Processing's PrintWriter —
    /// https://processing.org/reference/PrintWriter.html. Get one via
    /// Sketch.CreateWriter(path); call Flush() periodically (or at least
    /// once before the sketch exits) and Close() when done — nothing is
    /// guaranteed to hit disk until then, matching Processing's own
    /// buffered-writer behavior.
    /// </summary>
    public sealed class PrintWriter : IDisposable
    {
        private readonly StreamWriter _writer;
        private bool _disposed;

        internal PrintWriter(StreamWriter writer) => _writer = writer;

        /// <summary>Writes text with no trailing newline, like Processing's PrintWriter.print().</summary>
        public void Print(object value) => _writer.Write(value);

        /// <summary>Writes text followed by a newline, like Processing's PrintWriter.println(). Call with no argument for a blank line.</summary>
        public void Println(object? value = null) => _writer.WriteLine(value);

        /// <summary>Flushes buffered writes to disk, like Processing's PrintWriter.flush() — call this periodically for a long-running writer, since nothing is guaranteed to be on disk until then.</summary>
        public void Flush() => _writer.Flush();

        /// <summary>Flushes and closes the underlying file, like Processing's PrintWriter.close(). Always call this (or dispose the PrintWriter) once you're done writing.</summary>
        public void Close()
        {
            if (_disposed)
                return;
            _writer.Flush();
            _writer.Dispose();
            _disposed = true;
        }

        public void Dispose() => Close();
    }

    /// <summary>
    /// A simple line-based text-file reader, equivalent to Processing's
    /// BufferedReader — https://processing.org/reference/BufferedReader.html.
    /// Get one via Sketch.CreateReader(path); call ReadLine() until it
    /// returns null, then Close().
    /// </summary>
    public sealed class BufferedReader : IDisposable
    {
        private readonly StreamReader _reader;
        private bool _disposed;

        internal BufferedReader(StreamReader reader) => _reader = reader;

        /// <summary>Reads the next line, or null once the file is exhausted, like Processing's BufferedReader.readLine().</summary>
        public string? ReadLine() => _reader.ReadLine();

        /// <summary>Closes the underlying file, like Processing's BufferedReader.close().</summary>
        public void Close()
        {
            if (_disposed)
                return;
            _reader.Dispose();
            _disposed = true;
        }

        public void Dispose() => Close();
    }
}
