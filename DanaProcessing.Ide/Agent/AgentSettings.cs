using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DanaProcessing.Ide.Agent
{
    /// <summary>Which provider Agent mode talks to, plus that provider's
    /// bring-your-own API key -- see Updates/UpdateSettings.cs for the
    /// identical load/save-as-JSON pattern this mirrors.
    ///
    /// Stored in plain text, like every other settings file in this codebase
    /// (confirmed: no encryption/DPAPI exists anywhere here today). A real
    /// DPAPI-backed store would be Windows-only with no equivalent story for
    /// the macOS/Linux builds this app also ships -- SettingsWindow shows an
    /// explicit warning about this next to the key field rather than quietly
    /// storing a credential as if it were protected.</summary>
    public class AgentSettings
    {
        /// <summary>Matches an ILlmProvider.Name exactly ("Anthropic"/"OpenAI") --
        /// see AgentProviders.Resolve().</summary>
        public string Provider { get; set; } = "Anthropic";

        public string? ApiKey { get; set; }

        /// <summary>Blank means "use the provider's own built-in default" --
        /// see ILlmProvider.SendAsync's own remarks on why this is a free-text
        /// field instead of a hardcoded dropdown.</summary>
        public string? Model { get; set; }

        public static AgentSettings Default() => new();
    }

    public static class AgentSettingsStore
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
                return Path.Combine(dir, "agent-settings.json");
            }
        }

        public static AgentSettings Load()
        {
            try
            {
                if (!File.Exists(FilePath))
                    return AgentSettings.Default();

                var json = File.ReadAllText(FilePath);
                var settings = JsonSerializer.Deserialize<AgentSettings>(json, JsonOptions);
                return settings ?? AgentSettings.Default();
            }
            catch
            {
                return AgentSettings.Default();
            }
        }

        public static void Save(AgentSettings settings)
        {
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(FilePath, json);
        }
    }
}
