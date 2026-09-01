using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DanaProcessing.Ide.Theme
{
    /// <summary>
    /// Every value the Settings window lets the user tweak: colors (applied
    /// live — see <see cref="ClayTheme.ApplyPalette"/>) plus "detalles" like
    /// corner roundness and font stacks (applied on next rebuild/restart).
    /// Colors are stored as hex strings ("#RRGGBB") so they round-trip
    /// through both <see cref="Avalonia.Media.Color.Parse(string)"/> and JSON
    /// without any custom converter.
    /// </summary>
    public class ThemeSettings
    {
        // --- Superficies ---
        public string BaseColor { get; set; } = "#F8F6F2";
        public string SurfaceColor { get; set; } = "#FFFFFF";
        public string SurfaceRaisedColor { get; set; } = "#FDFBF8";
        public string SurfaceHigherColor { get; set; } = "#FAF5EE";
        public string SurfaceHoverColor { get; set; } = "#E8DDD0";
        public string SurfacePressedColor { get; set; } = "#D5C8B8";

        // --- Acentos ---
        public string AccentColor { get; set; } = "#FFB08C";
        public string AccentDimColor { get; set; } = "#F09B78";
        public string OnAccentColor { get; set; } = "#4A3728";
        public string SecondaryColor { get; set; } = "#A8D0E6";
        public string SecondaryDimColor { get; set; } = "#89B8D0";

        // --- Estados ---
        public string SuccessColor { get; set; } = "#B5E6C3";
        public string SuccessSurfaceColor { get; set; } = "#E8F5ED";
        public string DangerColor { get; set; } = "#F5A0A0";
        public string DangerSurfaceColor { get; set; } = "#FCE8E8";
        public string DangerHoverColor { get; set; } = "#E86A6A";

        // --- Texto ---
        public string TextPrimaryColor { get; set; } = "#2D2A24";
        public string TextSecondaryColor { get; set; } = "#5A554C";
        public string TextMutedColor { get; set; } = "#9A958A";

        // --- Detalles: radios (en píxeles) ---
        public double RadiusSmall { get; set; } = 10;
        public double RadiusMedium { get; set; } = 14;
        public double RadiusLarge { get; set; } = 20;
        public double RadiusCard { get; set; } = 24;
        public double RadiusButton { get; set; } = 16;
        public double RadiusChrome { get; set; } = 8;

        // --- Detalles: tipografías (mismo formato de lista que usa Avalonia's FontFamily) ---
        public string FontDisplay { get; set; } = "Sora,Segoe UI Semibold,SF Pro Display,Inter,Arial";
        public string FontBody { get; set; } = "Inter,Segoe UI,SF Pro Text,Arial";
        public string FontMono { get; set; } = "Cascadia Code,JetBrains Mono,Consolas,Menlo,monospace";

        /// <summary>The exact values ClayTheme.cs shipped with, so "Restaurar valores predeterminados" is trivial.</summary>
        public static ThemeSettings Default() => new();

        public ThemeSettings Clone() => (ThemeSettings)MemberwiseClone();
    }

    /// <summary>Loads/saves <see cref="ThemeSettings"/> as JSON under the user's AppData folder.</summary>
    public static class ThemeSettingsStore
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
                return Path.Combine(dir, "theme-settings.json");
            }
        }

        /// <summary>Returns the saved settings, or <see cref="ThemeSettings.Default"/> if none exist yet or the file is unreadable.</summary>
        public static ThemeSettings Load()
        {
            try
            {
                if (!File.Exists(FilePath))
                    return ThemeSettings.Default();

                var json = File.ReadAllText(FilePath);
                var settings = JsonSerializer.Deserialize<ThemeSettings>(json, JsonOptions);
                return settings ?? ThemeSettings.Default();
            }
            catch
            {
                // Corrupt or unreadable file: fall back to defaults rather than
                // crash the app over a cosmetic settings file.
                return ThemeSettings.Default();
            }
        }

        public static void Save(ThemeSettings settings)
        {
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(FilePath, json);
        }
    }
}