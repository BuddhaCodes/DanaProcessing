using System.Text.Json;
using System.Text.Json.Serialization;

namespace DanaProcessing.Ide
{
    /// <summary>
    /// How the code looks over the visuals in performance mode (F11): how
    /// see-through the code itself is, how dark the band behind it is, and
    /// its size. Set in Settings → Performance mode, or live on stage with
    /// Ctrl+Shift ↑/↓, Ctrl ↑/↓ and Ctrl +/−; either way it's remembered for
    /// the next show.
    /// </summary>
    public sealed class PerformanceSettings
    {
        public const double MinCodeOpacity = 0.2, MaxCodeOpacity = 1.0;

        /// <summary>1 = solid code, 0.2 = a ghost of it over the visuals.</summary>
        public double CodeOpacity { get; set; } = 1.0;

        /// <summary>0 = no band behind the code, 0.9 = almost black.</summary>
        public double BackdropOpacity { get; set; } = PerformanceMode.DefaultOpacity;

        public double FontSize { get; set; } = PerformanceMode.DefaultFontSize;

        public static PerformanceSettings Default() => new();

        public PerformanceSettings Clone() => (PerformanceSettings)MemberwiseClone();

        /// <summary>Every value back inside its range (a hand-edited or old file can't break the stage).</summary>
        public PerformanceSettings Normalized() => new()
        {
            CodeOpacity = Clamp(CodeOpacity, MinCodeOpacity, MaxCodeOpacity, 1.0),
            BackdropOpacity = Clamp(BackdropOpacity, PerformanceMode.MinOpacity, PerformanceMode.MaxOpacity, PerformanceMode.DefaultOpacity),
            FontSize = Clamp(FontSize, PerformanceMode.MinFontSize, PerformanceMode.MaxFontSize, PerformanceMode.DefaultFontSize),
        };

        private static double Clamp(double v, double min, double max, double fallback) =>
            double.IsFinite(v) ? Math.Clamp(v, min, max) : fallback;
    }

    /// <summary>Loads/saves <see cref="PerformanceSettings"/> as JSON next to the IDE's other settings.</summary>
    public static class PerformanceSettingsStore
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        /// <summary>Another file instead of the one in AppData (tests use a temp file). Null = the usual place.</summary>
        public static string? PathOverride { get; set; }

        private static string FilePath
        {
            get
            {
                if (PathOverride != null)
                    return PathOverride;
                var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DanaProcessingIde");
                Directory.CreateDirectory(dir);
                return Path.Combine(dir, "performance-mode-settings.json");
            }
        }

        /// <summary>The saved settings (normalized), or the defaults if there are none or the file can't be read.</summary>
        public static PerformanceSettings Load()
        {
            try
            {
                if (!File.Exists(FilePath))
                    return PerformanceSettings.Default();
                var settings = JsonSerializer.Deserialize<PerformanceSettings>(File.ReadAllText(FilePath), JsonOptions);
                return (settings ?? PerformanceSettings.Default()).Normalized();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                return PerformanceSettings.Default();
            }
        }

        public static void Save(PerformanceSettings settings)
        {
            try
            {
                File.WriteAllText(FilePath, JsonSerializer.Serialize(settings.Normalized(), JsonOptions));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                DanaLogger.Warn($"Modo performance: no se pudieron guardar los ajustes ({ex.Message}).");
            }
        }
    }
}
