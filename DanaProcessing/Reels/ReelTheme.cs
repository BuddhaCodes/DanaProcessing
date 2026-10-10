#pragma warning disable CS0618 // SkiaSharp 3 marks the SKPaint text API obsolete; it still works and keeps this file usable on 2.88 too.
using SkiaSharp;

namespace DanaProcessing.Reels
{
    /// <summary>Colors and fonts of a reel. Clay is DanaProcessing's own look (the IDE's warm accent on a dark ground).</summary>
    internal sealed class ReelTheme
    {
        public SKColor BackgroundTop, BackgroundBottom, GlowA, GlowB;
        public SKColor Panel, PanelBorder, PanelHeader, Text, Muted, LineNumber, CurrentLine;
        public SKColor Keyword, Type, Method, String, Number, Comment, Punct;
        public SKColor Accent, OnAccent, Mark, Cursor, CaptionBg, CaptionText, Shadow;
        public SKColor Dot1, Dot2, Dot3;

        public SKTypeface Mono = null!, Ui = null!, UiBold = null!;

        public static ReelTheme Create(ReelThemeKind kind)
        {
            var t = kind switch
            {
                ReelThemeKind.Dark => new ReelTheme
                {
                    BackgroundTop = C("#0B0F17"), BackgroundBottom = C("#121826"), GlowA = C("#5B8CFF"), GlowB = C("#C77DFF"),
                    Panel = C("#141A26"), PanelBorder = C("#263046"), PanelHeader = C("#1A2232"),
                    Text = C("#E6EAF2"), Muted = C("#8B95A8"), LineNumber = C("#4A5468"), CurrentLine = C("#1E2638"),
                    Keyword = C("#C792EA"), Type = C("#7FDBCA"), Method = C("#82AAFF"), String = C("#C3E88D"),
                    Number = C("#F78C6C"), Comment = C("#5C6A82"), Punct = C("#A6B0C3"),
                    Accent = C("#82AAFF"), OnAccent = C("#0B0F17"), Mark = C("#82AAFF"), Cursor = C("#FFCB6B"),
                    CaptionBg = C("#E6EAF2"), CaptionText = C("#0B0F17"), Shadow = C("#000000"),
                    Dot1 = C("#FF6B6B"), Dot2 = C("#FFD166"), Dot3 = C("#06D6A0"),
                },
                ReelThemeKind.Light => new ReelTheme
                {
                    BackgroundTop = C("#F8F6F2"), BackgroundBottom = C("#EFE7DC"), GlowA = C("#FFB08C"), GlowB = C("#A8D0E6"),
                    Panel = C("#FFFFFF"), PanelBorder = C("#E8E2DA"), PanelHeader = C("#FAF5EE"),
                    Text = C("#2D2A24"), Muted = C("#9A958A"), LineNumber = C("#C9C1B5"), CurrentLine = C("#FBF3EA"),
                    Keyword = C("#C2410C"), Type = C("#0F766E"), Method = C("#1D4ED8"), String = C("#15803D"),
                    Number = C("#B45309"), Comment = C("#A39E93"), Punct = C("#6B665D"),
                    Accent = C("#F09B78"), OnAccent = C("#FFFFFF"), Mark = C("#FFB08C"), Cursor = C("#F09B78"),
                    CaptionBg = C("#2D2A24"), CaptionText = C("#F8F6F2"), Shadow = C("#5A4A3A"),
                    Dot1 = C("#F5A0A0"), Dot2 = C("#F7D58B"), Dot3 = C("#B5E6C3"),
                },
                _ => new ReelTheme // Clay
                {
                    BackgroundTop = C("#1A1512"), BackgroundBottom = C("#241C17"), GlowA = C("#FFB08C"), GlowB = C("#A8D0E6"),
                    Panel = C("#211B17"), PanelBorder = C("#3A2F27"), PanelHeader = C("#2A221C"),
                    Text = C("#F1E9DF"), Muted = C("#A39887"), LineNumber = C("#5E5246"), CurrentLine = C("#2C241E"),
                    Keyword = C("#FFB08C"), Type = C("#A8D0E6"), Method = C("#F3D98B"), String = C("#B5E6C3"),
                    Number = C("#E6B5D8"), Comment = C("#7D7164"), Punct = C("#C9BBA9"),
                    Accent = C("#FFB08C"), OnAccent = C("#4A3728"), Mark = C("#FFB08C"), Cursor = C("#FFB08C"),
                    CaptionBg = C("#FFB08C"), CaptionText = C("#3A2A1E"), Shadow = C("#000000"),
                    Dot1 = C("#F5A0A0"), Dot2 = C("#F7D58B"), Dot3 = C("#B5E6C3"),
                },
            };

            t.Mono = Font(SKFontStyle.Normal, "Cascadia Code", "Cascadia Mono", "JetBrains Mono", "Fira Code", "Consolas", "Menlo", "SF Mono", "DejaVu Sans Mono", "Liberation Mono", "monospace");
            t.Ui = Font(SKFontStyle.Normal, "Inter", "Segoe UI", "SF Pro Text", "Helvetica Neue", "Roboto", "DejaVu Sans", "Arial");
            t.UiBold = Font(SKFontStyle.Bold, "Sora", "Inter", "Segoe UI", "SF Pro Display", "Helvetica Neue", "Roboto", "DejaVu Sans", "Arial");
            return t;
        }

        public SKColor ColorOf(TokenKind kind) => kind switch
        {
            TokenKind.Keyword => Keyword,
            TokenKind.Type => Type,
            TokenKind.Method => Method,
            TokenKind.String => String,
            TokenKind.Number => Number,
            TokenKind.Comment => Comment,
            TokenKind.Punct => Punct,
            _ => Text,
        };

        private static SKColor C(string hex) => SKColor.Parse(hex);

        private static SKTypeface Font(SKFontStyle style, params string[] families)
        {
            foreach (var f in families)
            {
                var tf = SKFontManager.Default.MatchFamily(f, style);
                if (tf != null)
                    return tf;
            }
            return SKTypeface.FromFamilyName(null, style) ?? SKTypeface.Default;
        }
    }
}
