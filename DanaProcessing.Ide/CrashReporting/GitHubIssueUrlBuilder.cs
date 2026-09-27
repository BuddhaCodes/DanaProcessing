using System;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;

namespace DanaProcessing.Ide.CrashReporting
{
    /// <summary>
    /// Builds a GitHub "new issue" URL prefilled with either a crash's
    /// details or just environment info for a manual report -- title + body
    /// arrive as query parameters GitHub's own compose page reads
    /// (https://github.com/{repo}/issues/new?title=...&amp;body=...).
    /// Nothing is ever sent over the network from here: opening this URL
    /// just shows the user their own browser, on GitHub's own page, with
    /// the fields already filled in -- they still have to review it and
    /// press GitHub's own "Submit new issue" button for anything to
    /// actually be created. No token, no API call, no server of ours
    /// involved at all (see the Settings section's own remark on why this
    /// was chosen over a fully-automatic relay).
    /// </summary>
    internal static class GitHubIssueUrlBuilder
    {
        private const string RepoSlug = "BuddhaCodes/DanaProcessing"; // same repo UpdateChecker already points at

        // GitHub's own compose page silently truncates an extremely long
        // body; capping the stack trace here keeps the URL well under the
        // length a browser/GitHub actually accepts, rather than finding out
        // the hard way with a real crash's real stack trace.
        private const int MaxStackTraceChars = 4000;

        public static string BuildForCrash(CrashReport report)
        {
            var title = $"Crash: {report.ExceptionType}";

            var stackTrace = report.StackTrace ?? "(sin stack trace)";
            if (stackTrace.Length > MaxStackTraceChars)
                stackTrace = stackTrace.Substring(0, MaxStackTraceChars) + "\n... (truncado)";

            var body = new StringBuilder()
                .AppendLine($"**Versión:** {report.AppVersion}")
                .AppendLine($"**SO:** {report.OsDescription}")
                .AppendLine($"**Momento (UTC):** {report.TimestampUtc:u}")
                .AppendLine($"**Origen:** {report.Source}")
                .AppendLine()
                .AppendLine($"**Excepción:** {report.ExceptionType}: {report.Message}")
                .AppendLine()
                .AppendLine("```")
                .AppendLine(stackTrace)
                .AppendLine("```")
                .AppendLine()
                .AppendLine("<!-- Si podés, contá acá qué estabas haciendo justo antes de que pasara esto. -->")
                .ToString();

            return Build(title, body);
        }

        /// <summary>The menu's "Reportar un problema..." -- a plain support/
        /// feedback entry point that needs no crash to have happened at
        /// all. Prefills just the environment info (so the user doesn't
        /// have to go hunting for their own version number) and leaves the
        /// title blank and the body's actual description empty for them to
        /// write themselves.</summary>
        public static string BuildManual()
        {
            var body = new StringBuilder()
                .AppendLine($"**Versión:** {AppVersion.Full}")
                .AppendLine($"**SO:** {RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture})")
                .AppendLine()
                .AppendLine("<!-- Contá acá el problema que encontraste, o cualquier sugerencia/feedback que tengas. -->")
                .ToString();

            return Build(title: "", body);
        }

        private static string Build(string title, string body) =>
            $"https://github.com/{RepoSlug}/issues/new"
            + $"?title={WebUtility.UrlEncode(title)}"
            + $"&body={WebUtility.UrlEncode(body)}";
    }
}
