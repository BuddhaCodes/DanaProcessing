using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DanaProcessing.Ide.Localization
{
    public enum AppLanguage
    {
        Spanish,
        English,
    }

    /// <summary>
    /// Every hardcoded UI string in this project (Content/Text/Title/ToolTip/
    /// status/error text — anything a user actually reads) is wrapped at its
    /// call site with <see cref="Tr"/>, passing the Spanish and English
    /// variants side by side: `Loc.Tr("Nuevo", "New")`. Deliberately NOT a
    /// separate resource file or a giant key-lookup dictionary — with ~190
    /// scattered one-off strings and no plan to add a third language, keeping
    /// both variants visible right where the string is actually used is far
    /// easier to review and keep in sync than a resx/key indirection would be,
    /// and the compiler catches every reference (no risk of a missing key at
    /// runtime the way a dictionary lookup would have).
    ///
    /// Like ThemeSettings' corner-radius/font fields, this is a "baked into
    /// controls at construction time" setting — every Content/Text assignment
    /// above runs ONCE, when that control is built, so changing the language
    /// needs the same "Guardar y reiniciar ahora" full-process restart
    /// SettingsWindow already uses for those (see RestartApp() there) — not
    /// a live-switching / rebuild-the-tree scheme.
    /// </summary>
    public static class Loc
    {
        public static AppLanguage Current { get; private set; } = AppLanguage.Spanish;

        /// <summary>Call once, at startup (App.Initialize(), same spot ClayTheme.Initialize() runs), before any window gets constructed — every Loc.Tr() call site reads Current at CONSTRUCTION time, not reactively.</summary>
        public static void Initialize(AppLanguage language) => Current = language;

        public static string Tr(string es, string en) => Current == AppLanguage.Spanish ? es : en;
    }

    public class LocaleSettings
    {
        public AppLanguage Language { get; set; } = AppLanguage.Spanish;

        public static LocaleSettings Default() => new();

        public LocaleSettings Clone() => (LocaleSettings)MemberwiseClone();
    }

    /// <summary>Loads/saves <see cref="LocaleSettings"/> as JSON under the user's AppData folder — same file, same pattern as ThemeSettingsStore/RenderingSettingsStore.</summary>
    public static class LocaleSettingsStore
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter() },
        };

        private static string FilePath
        {
            get
            {
                var dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "DanaProcessingIde");
                Directory.CreateDirectory(dir);
                return Path.Combine(dir, "locale-settings.json");
            }
        }

        public static LocaleSettings Load()
        {
            try
            {
                if (!File.Exists(FilePath))
                    return LocaleSettings.Default();

                var json = File.ReadAllText(FilePath);
                var settings = JsonSerializer.Deserialize<LocaleSettings>(json, JsonOptions);
                return settings ?? LocaleSettings.Default();
            }
            catch
            {
                // Corrupt or unreadable file: fall back to defaults rather than
                // crash the app over a cosmetic settings file.
                return LocaleSettings.Default();
            }
        }

        public static void Save(LocaleSettings settings)
        {
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(FilePath, json);
        }
    }
}
