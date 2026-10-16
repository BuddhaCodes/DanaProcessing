using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using DanaProcessing.Ide.Localization;
using DanaProcessing.Ide.Theme;

namespace DanaProcessing.Ide.Editor
{
    /// <summary>
    /// The code time machine: every Run of this sketch as a card -- what it drew,
    /// when, and how much the code differs from what's in the editor now.
    /// "Restore" puts that code back (one Ctrl+Z undoes it) and locks its seed,
    /// so the next Run looks like the picture; "New tab" opens it beside yours.
    /// </summary>
    public sealed class CodeHistoryWindow : Window
    {
        private readonly Func<string> _currentCode;
        private readonly Action<CodeVersion> _restore;
        private readonly Action<CodeVersion> _openInTab;

        public CodeHistoryWindow(string sketchName, IReadOnlyList<CodeVersion> versions, Func<string> currentCode,
                                 Action<CodeVersion> restore, Action<CodeVersion> openInTab)
        {
            _currentCode = currentCode;
            _restore = restore;
            _openInTab = openInTab;

            Title = Loc.Tr($"Máquina del tiempo · {sketchName}", $"Time machine · {sketchName}");
            Width = 980;
            Height = 720;
            Background = ClayTheme.Base;
            Styles.AddRange(ClayTheme.ButtonEffectStyles());

            Control body;
            if (versions.Count == 0)
            {
                body = new TextBlock
                {
                    Text = Loc.Tr("Todavía no hay versiones: cada Run (▶) guarda una, con una foto de lo que dibujó.",
                                  "No versions yet: every Run (▶) keeps one, with a picture of what it drew."),
                    Foreground = ClayTheme.TextMuted,
                    FontFamily = ClayTheme.FontBody,
                    Margin = new Thickness(24),
                    TextWrapping = TextWrapping.Wrap,
                };
            }
            else
            {
                string now = currentCode();
                var cards = new WrapPanel { Margin = new Thickness(14) };
                foreach (var v in versions)
                    cards.Children.Add(BuildCard(v, now));
                body = new ScrollViewer { Content = cards };
            }

            ClayChrome.Apply(this, body);
        }

        private Control BuildCard(CodeVersion v, string now)
        {
            Control picture;
            if (v.ThumbnailPath != null && File.Exists(v.ThumbnailPath))
            {
                using var stream = File.OpenRead(v.ThumbnailPath);
                picture = new Image { Source = new Bitmap(stream), Stretch = Stretch.Uniform, Height = 150 };
            }
            else
            {
                picture = new Border
                {
                    Height = 150,
                    Background = ClayTheme.SurfaceHigher,
                    CornerRadius = ClayTheme.RadiusSmall,
                    Child = new TextBlock { Text = "—", Foreground = ClayTheme.TextMuted, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
                };
            }

            string code = "";
            try { code = v.ReadCode(); } catch (IOException) { }
            var (added, removed) = CodeHistory.LineDiff(code, now);
            string diff = added == 0 && removed == 0
                ? Loc.Tr("igual al código actual", "same as the current code")
                : Loc.Tr($"+{added} −{removed} líneas hasta ahora", $"+{added} −{removed} lines to now");

            var local = v.TimeUtc.ToLocalTime();
            var when = new TextBlock
            {
                Text = $"{Ago(v.TimeUtc)}  ·  {local:HH:mm}" + (v.Seed != 0 ? $"  ·  {Loc.Tr("semilla", "seed")} {v.Seed}" : ""),
                Foreground = ClayTheme.TextPrimary,
                FontFamily = ClayTheme.FontBody,
                FontSize = 12,
                Margin = new Thickness(2, 8, 2, 0),
            };
            var changes = new TextBlock
            {
                Text = diff,
                Foreground = ClayTheme.TextMuted,
                FontFamily = ClayTheme.FontBody,
                FontSize = 11.5,
                Margin = new Thickness(2, 2, 2, 8),
            };

            var restore = new Button { Content = Loc.Tr("Restaurar", "Restore"), Classes = { "clay-run" }, Padding = new Thickness(12, 6), FontSize = 12 };
            restore.Click += (_, _) => { _restore(v); Close(); };
            var newTab = new Button { Content = Loc.Tr("Pestaña nueva", "New tab"), Classes = { "clay-secondary" }, Padding = new Thickness(12, 6), FontSize = 12, Margin = new Thickness(6, 0, 0, 0) };
            newTab.Click += (_, _) => { _openInTab(v); Close(); };
            ToolTip.SetTip(restore, Loc.Tr("Pone este código en la pestaña (Ctrl+Z lo deshace) y fija su semilla", "Puts this code in the tab (Ctrl+Z undoes it) and locks its seed"));

            return new Border
            {
                Width = 220,
                Margin = new Thickness(8),
                Padding = new Thickness(10),
                Background = ClayTheme.Surface,
                CornerRadius = ClayTheme.RadiusMedium,
                BoxShadow = ClayTheme.ShadowSubtle,
                Child = new StackPanel
                {
                    Children =
                    {
                        picture, when, changes,
                        new StackPanel { Orientation = Orientation.Horizontal, Children = { restore, newTab } },
                    },
                },
            };
        }

        private static string Ago(DateTime utc)
        {
            var span = DateTime.UtcNow - utc;
            if (span.TotalMinutes < 1) return Loc.Tr("recién", "just now");
            if (span.TotalHours < 1) return Loc.Tr($"hace {(int)span.TotalMinutes} min", $"{(int)span.TotalMinutes} min ago");
            if (span.TotalDays < 1) return Loc.Tr($"hace {(int)span.TotalHours} h", $"{(int)span.TotalHours} h ago");
            return utc.ToLocalTime().ToString("d MMM");
        }
    }
}
