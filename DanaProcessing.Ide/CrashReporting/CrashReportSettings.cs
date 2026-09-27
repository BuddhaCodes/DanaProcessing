using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DanaProcessing.Ide.CrashReporting
{
    /// <summary>Whether the IDE offers to report its own crashes on the next
    /// startup after one happens. Opt-in, default off -- see
    /// RenderingSettings.cs (DanaProcessing.Ide/Theme) for the identical
    /// load/save-as-JSON pattern this mirrors. Deliberately just one flag:
    /// nothing here decides WHAT gets sent or WHEN on its own -- Enabled only
    /// gates whether CrashReportStore.WriteBestEffort bothers writing a
    /// crash file at all (see its own remark), and even then a real GitHub
    /// issue is only ever opened after the user reviews the prefilled page
    /// themselves and submits it there.</summary>
    public class CrashReportSettings
    {
        public bool Enabled { get; set; } = false;

        public static CrashReportSettings Default() => new();

        public CrashReportSettings Clone() => (CrashReportSettings)MemberwiseClone();
    }

    public static class CrashReportSettingsStore
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
                return Path.Combine(dir, "crash-report-settings.json");
            }
        }

        public static CrashReportSettings Load()
        {
            try
            {
                if (!File.Exists(FilePath))
                    return CrashReportSettings.Default();

                var json = File.ReadAllText(FilePath);
                var settings = JsonSerializer.Deserialize<CrashReportSettings>(json, JsonOptions);
                return settings ?? CrashReportSettings.Default();
            }
            catch
            {
                return CrashReportSettings.Default();
            }
        }

        public static void Save(CrashReportSettings settings)
        {
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(FilePath, json);
        }
    }
}
