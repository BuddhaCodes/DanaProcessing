using System.Text.Json;
using Avalonia;

namespace DanaProcessing.AvaloniaHost
{
    /// <summary>
    /// Where each widget was left on screen (physical pixels), by sketch class,
    /// so it opens there again: a small JSON file in the user's local app data
    /// (DanaProcessing/widgets.json). Best effort: a missing or broken file
    /// just means the default corner.
    /// </summary>
    internal static class WidgetPlacement
    {
        private static readonly object s_lock = new();

        /// <summary>Overridable for tests.</summary>
        internal static string FilePath { get; set; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DanaProcessing", "widgets.json");

        public static PixelPoint? Load(string key)
        {
            lock (s_lock)
                return Read().TryGetValue(key, out var p) && p.Length == 2 ? new PixelPoint(p[0], p[1]) : null;
        }

        public static void Save(string key, PixelPoint point)
        {
            lock (s_lock)
            {
                try
                {
                    var all = Read();
                    all[key] = new[] { point.X, point.Y };
                    Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                    File.WriteAllText(FilePath, JsonSerializer.Serialize(all));
                }
                catch
                {
                    // A read-only profile or a full disk: the widget just opens in its corner next time.
                }
            }
        }

        private static Dictionary<string, int[]> Read()
        {
            try
            {
                if (File.Exists(FilePath))
                    return JsonSerializer.Deserialize<Dictionary<string, int[]>>(File.ReadAllText(FilePath)) ?? new();
            }
            catch
            {
                // Unreadable: start over.
            }
            return new();
        }
    }
}
