using System;
using System.IO;
using System.Text;

namespace DanaProcessing.Ide.CrashReporting
{
    /// <summary>
    /// A plain text log of every unhandled exception, with its whole stack
    /// trace (inner exceptions included), always written -- unlike
    /// CrashReportStore, which is opt-in and meant for sending. When the IDE
    /// breaks on an exception with only "StartWithClassicDesktopLifetime" to
    /// show (an exception on the UI thread inside Avalonia), this file says
    /// where it really came from: %LOCALAPPDATA%\DanaProcessingIde\logs\errores.log.
    /// </summary>
    public static class ErrorLog
    {
        private const long MaxBytes = 512 * 1024;   // past this, the old log becomes errores.old.log
        private static readonly object Gate = new();

        public static string FilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DanaProcessingIde", "logs", "errores.log");

        /// <summary>Appends <paramref name="ex"/> (never throws).</summary>
        public static void Write(Exception ex, string source) => Write(FilePath, ex, source, DateTime.Now);

        internal static void Write(string path, Exception ex, string source, DateTime now)
        {
            try
            {
                lock (Gate)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    if (File.Exists(path) && new FileInfo(path).Length > MaxBytes)
                        File.Move(path, Path.ChangeExtension(path, ".old.log"), overwrite: true);
                    File.AppendAllText(path, Format(ex, source, now), new UTF8Encoding(false));
                }
            }
            catch
            {
                // Logging an error must never cause another one.
            }
        }

        /// <summary>One entry: when, where from, and ex.ToString() (type, message, stack, inner exceptions).</summary>
        internal static string Format(Exception ex, string source, DateTime now) =>
            $"===== {now:yyyy-MM-dd HH:mm:ss.fff} · {source} · Dana {AppVersion.Full}{Environment.NewLine}{ex}{Environment.NewLine}{Environment.NewLine}";
    }
}
