using System;

namespace DanaProcessing
{
    /// <summary>
    /// Colored, timestamped console logging for DanaProcessing. Used both by the
    /// library internally (e.g. reporting a crashed sketch) and available to sketch
    /// authors via Sketch.Println() / LogWarning() / LogError().
    /// </summary>
    public static class DanaLogger
    {
        public static void Info(string message) => Write("INFO", ConsoleColor.Cyan, message);
        public static void Warn(string message) => Write("WARN", ConsoleColor.Yellow, message);
        public static void Error(string message) => Write("ERROR", ConsoleColor.Red, message);

        /// <summary>Logs an exception with context, including a dimmed stack trace.</summary>
        public static void ErrorFromException(Exception ex, string context)
        {
            Write("ERROR", ConsoleColor.Red, $"{context}: {ex.GetType().Name}: {ex.Message}");
            var prev = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine(ex.StackTrace);
            Console.ForegroundColor = prev;
        }

        private static void Write(string level, ConsoleColor color, string message)
        {
            var prev = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write($"[{DateTime.Now:HH:mm:ss}] ");
            Console.ForegroundColor = color;
            Console.Write($"{level,-5} ");
            Console.ForegroundColor = prev;
            Console.WriteLine(message);
        }
    }
}
