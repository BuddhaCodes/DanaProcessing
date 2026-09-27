using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace DanaProcessing.Ide.CrashReporting
{
    /// <summary>A single crash, captured at the moment it happened -- enough
    /// detail to file a real, useful GitHub issue from, without needing to
    /// reproduce it or attach a debugger. Timestamp/AppVersion/OsDescription
    /// are captured then, not read again later, since the running app IS in
    /// the middle of dying when this gets built.</summary>
    public sealed record CrashReport(
        string ExceptionType,
        string Message,
        string? StackTrace,
        string Source,
        DateTime TimestampUtc,
        string AppVersion,
        string OsDescription);

    /// <summary>
    /// Persists crashes to plain JSON files under %APPDATA%, one per crash --
    /// written from inside AppDomain.CurrentDomain.UnhandledException (see
    /// Program.cs), read back on the NEXT startup to offer reporting them
    /// (see MainWindow's CheckForPendingCrashReportAsync).
    ///
    /// Deliberately NOT trying to show any UI or make a network call from
    /// inside the crash handler itself -- the process is actively
    /// terminating at that point (Avalonia's own message loop, the render
    /// thread, anything else could already be gone), so the only thing this
    /// does there is a best-effort local file write, wrapped in its own
    /// try/catch so a failure to write a crash report can never itself
    /// throw a SECOND unhandled exception out of the crash handler.
    /// </summary>
    public static class CrashReportStore
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

        private static string Directory
        {
            get
            {
                var dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "DanaProcessingIde",
                    "crash-reports");
                System.IO.Directory.CreateDirectory(dir);
                return dir;
            }
        }

        public static CrashReport Build(Exception ex, string source) => new(
            ex.GetType().FullName ?? ex.GetType().Name,
            ex.Message,
            ex.StackTrace,
            source,
            DateTime.UtcNow,
            AppVersion.Full,
            RuntimeInformation.OSDescription + " (" + RuntimeInformation.OSArchitecture + ")");

        /// <summary>Only actually writes if CrashReportSettings.Enabled is
        /// true -- opt-in means nothing is captured at all otherwise, not
        /// just "captured but never shown".</summary>
        public static void WriteBestEffort(CrashReport report)
        {
            try
            {
                if (!CrashReportSettingsStore.Load().Enabled)
                    return;

                var path = Path.Combine(Directory, $"{report.TimestampUtc:yyyyMMdd-HHmmss-fff}.json");
                File.WriteAllText(path, JsonSerializer.Serialize(report, JsonOptions));
            }
            catch
            {
                // Never let a crash report fail to write take down the crash
                // handler itself.
            }
        }

        public static IReadOnlyList<CrashReport> LoadPending()
        {
            var reports = new List<CrashReport>();
            try
            {
                foreach (var file in System.IO.Directory.GetFiles(Directory, "*.json"))
                {
                    try
                    {
                        var report = JsonSerializer.Deserialize<CrashReport>(File.ReadAllText(file));
                        if (report != null)
                            reports.Add(report);
                    }
                    catch
                    {
                        // A corrupt/partially-written crash file -- skip it rather
                        // than let one bad file block reporting the rest.
                    }
                }
            }
            catch
            {
                return Array.Empty<CrashReport>();
            }
            return reports;
        }

        /// <summary>Called after the user has been asked, whichever way they
        /// answered -- a crash that was already offered shouldn't nag again
        /// on the next launch either way.</summary>
        public static void ClearPending()
        {
            try
            {
                foreach (var file in System.IO.Directory.GetFiles(Directory, "*.json"))
                    File.Delete(file);
            }
            catch
            {
                // Best-effort cleanup -- a locked/undeletable file just means
                // it (and any others) get offered again next time, not a
                // reason to crash over.
            }
        }
    }
}
