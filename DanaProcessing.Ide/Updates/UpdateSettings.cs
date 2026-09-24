using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DanaProcessing.Ide.Updates
{
    /// <summary>Whether/how the IDE checks GitHub for a newer release. See
    /// RenderingSettings.cs (DanaProcessing.Ide/Theme) for the identical
    /// load/save-as-JSON pattern this mirrors.</summary>
    public class UpdateSettings
    {
        /// <summary>Whether to check for updates automatically on startup. Manually
        /// triggering "Check for updates" from the file menu always works regardless
        /// of this — clicking it is explicit intent either way.</summary>
        public bool AutoCheckEnabled { get; set; } = true;

        /// <summary>The version the user last dismissed with "Later", so the startup
        /// banner doesn't nag about the SAME available version every single launch —
        /// it still resurfaces once a newer version than this is published.</summary>
        public string? DismissedVersion { get; set; }

        public static UpdateSettings Default() => new();
    }

    public static class UpdateSettingsStore
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
                return Path.Combine(dir, "update-settings.json");
            }
        }

        public static UpdateSettings Load()
        {
            try
            {
                if (!File.Exists(FilePath))
                    return UpdateSettings.Default();

                var json = File.ReadAllText(FilePath);
                var settings = JsonSerializer.Deserialize<UpdateSettings>(json, JsonOptions);
                return settings ?? UpdateSettings.Default();
            }
            catch
            {
                return UpdateSettings.Default();
            }
        }

        public static void Save(UpdateSettings settings)
        {
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(FilePath, json);
        }
    }
}
