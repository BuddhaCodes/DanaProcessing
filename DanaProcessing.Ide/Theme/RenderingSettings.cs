using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DanaProcessing.Ide.Theme
{
    /// <summary>
    /// Antialiasing quality for the IDE's own live preview — separate knobs
    /// for the 2D (Skia) and 3D (OpenGL) renderers, since they use two
    /// entirely different AA techniques with different costs (see
    /// AvaloniaSketchCanvas.SupersampleScale and Renderer3DBackend's MSAA
    /// support). Applies on the next "Run" (see MainWindow's wiring), not
    /// live and not requiring a full app restart — unlike ThemeSettings'
    /// radius/font fields, nothing here is baked into an already-constructed
    /// control.
    /// </summary>
    public class RenderingSettings
    {
        /// <summary>1 = off, 2/3/4 = render the 2D canvas at that many times
        /// the sketch's own resolution and downscale — see
        /// AvaloniaSketchCanvas.SupersampleScale's own remark.</summary>
        public int SupersampleScale2D { get; set; } = 2;

        /// <summary>0 = off, otherwise the requested MSAA sample count for
        /// Renderer3D sketches (clamped to GL_MAX_SAMPLES at runtime) — see
        /// Renderer3DBackend.Create's own remark.</summary>
        public int MsaaSamples3D { get; set; } = 4;

        public static RenderingSettings Default() => new();

        public RenderingSettings Clone() => (RenderingSettings)MemberwiseClone();
    }

    /// <summary>Loads/saves <see cref="RenderingSettings"/> as JSON under the user's AppData folder — same file, same pattern as ThemeSettingsStore.</summary>
    public static class RenderingSettingsStore
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
                return Path.Combine(dir, "rendering-settings.json");
            }
        }

        /// <summary>Returns the saved settings, or <see cref="RenderingSettings.Default"/> if none exist yet or the file is unreadable.</summary>
        public static RenderingSettings Load()
        {
            try
            {
                if (!File.Exists(FilePath))
                    return RenderingSettings.Default();

                var json = File.ReadAllText(FilePath);
                var settings = JsonSerializer.Deserialize<RenderingSettings>(json, JsonOptions);
                return settings ?? RenderingSettings.Default();
            }
            catch
            {
                // Corrupt or unreadable file: fall back to defaults rather than
                // crash the app over a cosmetic settings file.
                return RenderingSettings.Default();
            }
        }

        public static void Save(RenderingSettings settings)
        {
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(FilePath, json);
        }
    }
}
