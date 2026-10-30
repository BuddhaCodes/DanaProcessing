using System.Text;

namespace DanaProcessing.AvaloniaHost.Attach
{
    /// <summary>
    /// A diagnostic trace of what attached sketches do with their windows --
    /// overlays shown and hidden, z-order fixes, pane scans, held layouts --
    /// for bugs that only show on a real desktop (flicker, a sketch that
    /// vanishes). Off unless the file %LOCALAPPDATA%\DanaProcessing\trace.on
    /// exists (checked every 2 s, so it can be switched on while the sketch
    /// runs); writes to %LOCALAPPDATA%\DanaProcessing\logs\attached-trace.log,
    /// which starts over past 2 MB. Callers only trace changes, never every
    /// frame. Any thread; never throws.
    /// </summary>
    internal static class AttachTrace
    {
        private const long MaxBytes = 2 * 1024 * 1024;
        private const double CheckSeconds = 2;

        private static readonly object s_lock = new();
        private static readonly System.Diagnostics.Stopwatch s_clock = System.Diagnostics.Stopwatch.StartNew();
        private static double s_lastCheck = double.NegativeInfinity;
        private static volatile bool s_on;

        /// <summary>Tests point this at a temporary folder; null = %LOCALAPPDATA%\DanaProcessing.</summary>
        internal static string? RootOverride { get; set; }

        public static string Root => RootOverride ??
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DanaProcessing");

        /// <summary>Its existence switches the trace on.</summary>
        public static string FlagFile => Path.Combine(Root, "trace.on");

        public static string LogFile => Path.Combine(Root, "logs", "attached-trace.log");

        /// <summary>Whether tracing is on (the flag file, looked at every 2 s).</summary>
        public static bool Enabled
        {
            get
            {
                double now = s_clock.Elapsed.TotalSeconds;
                if (now - s_lastCheck >= CheckSeconds)
                {
                    s_lastCheck = now;
                    try { s_on = File.Exists(FlagFile); } catch { s_on = false; }
                }
                return s_on;
            }
        }

        /// <summary>Forgets the last look at the flag file (tests).</summary>
        internal static void Recheck() => s_lastCheck = double.NegativeInfinity;

        /// <summary>One line: time, thread, where, what.</summary>
        internal static string FormatLine(DateTime time, int thread, string where, string what) =>
            $"{time:HH:mm:ss.fff} [{thread,3}] {where}: {what}";

        public static void Write(string where, string what)
        {
            if (!Enabled)
                return;
            try
            {
                string line = FormatLine(DateTime.Now, Environment.CurrentManagedThreadId, where, what) + Environment.NewLine;
                lock (s_lock)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(LogFile)!);
                    var info = new FileInfo(LogFile);
                    if (info.Exists && info.Length > MaxBytes)
                        File.Delete(LogFile);
                    File.AppendAllText(LogFile, line, Encoding.UTF8);
                }
            }
            catch { /* a trace must never break the sketch */ }
        }

        /// <summary>A window for the trace: handle and class name.</summary>
        public static string Describe(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero)
                return "0";
            string cls = "";
            try
            {
                if (OperatingSystem.IsWindows())
                {
                    var sb = new StringBuilder(128);
                    Win32.GetClassName(hwnd, sb, sb.Capacity);
                    cls = sb.ToString();
                }
            }
            catch { }
            return $"0x{hwnd.ToInt64():X}" + (cls.Length > 0 ? " " + cls : "");
        }

        public static string Rect(Win32.RECT r) => $"({r.Left},{r.Top})-({r.Right},{r.Bottom})";
    }
}
