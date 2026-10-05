using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DanaProcessing.Ide.Support
{
    /// <summary>
    /// Single source of truth for the donation link -- the Help menu, the
    /// About window and the occasional banner all read it from here, so
    /// changing the PayPal account is a one-line edit. (README.md,
    /// .github/FUNDING.yml and index.html carry the same URL as plain text.)
    /// </summary>
    internal static class SupportLinks
    {
        public const string DonateUrl = "https://paypal.me/CarlosFernandez934";

        public static void Open(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch
            {
                // No default browser / sandboxed environment: nothing useful
                // to do, and a donation link must never take the IDE down.
            }
        }
    }

    /// <summary>
    /// State for the occasional "support the project" banner. Deliberately
    /// conservative so it reads as a friendly reminder, not a nag: never
    /// before the user has had real use out of the IDE, at most once per
    /// session, with a long gap (both in runs and in days) between prompts,
    /// and a permanent "don't show again". Same JSON pattern as UpdateSettings.
    /// </summary>
    public class SupportSettings
    {
        /// <summary>Successful Runs ever (hot reloads don't count).</summary>
        public int SuccessfulRuns { get; set; }

        /// <summary>Value of <see cref="SuccessfulRuns"/> the last time the banner was shown (0 = never).</summary>
        public int LastPromptAtRun { get; set; }

        public DateTime? LastPromptUtc { get; set; }

        /// <summary>"No volver a mostrar" -- once true, the banner never appears again.</summary>
        public bool PromptDisabled { get; set; }

        public const int FirstPromptAfterRuns = 20;
        public const int RunsBetweenPrompts = 60;
        public static readonly TimeSpan MinTimeBetweenPrompts = TimeSpan.FromDays(30);

        public bool ShouldPrompt(DateTime nowUtc)
        {
            if (PromptDisabled)
                return false;
            if (SuccessfulRuns < FirstPromptAfterRuns)
                return false;
            if (LastPromptAtRun > 0 && SuccessfulRuns - LastPromptAtRun < RunsBetweenPrompts)
                return false;
            if (LastPromptUtc is { } last && nowUtc - last < MinTimeBetweenPrompts)
                return false;
            return true;
        }
    }

    public static class SupportSettingsStore
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        private static string FilePath
        {
            get
            {
                var dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "DanaProcessingIde");
                Directory.CreateDirectory(dir);
                return Path.Combine(dir, "support-settings.json");
            }
        }

        public static SupportSettings Load()
        {
            try
            {
                if (!File.Exists(FilePath))
                    return new SupportSettings();
                return JsonSerializer.Deserialize<SupportSettings>(File.ReadAllText(FilePath), JsonOptions) ?? new SupportSettings();
            }
            catch
            {
                return new SupportSettings();
            }
        }

        public static void Save(SupportSettings settings)
        {
            try
            {
                File.WriteAllText(FilePath, JsonSerializer.Serialize(settings, JsonOptions));
            }
            catch
            {
                // Best-effort: losing a run count is harmless.
            }
        }
    }
}
