using System;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using DanaProcessing.Ide.Theme;

namespace DanaProcessing.Ide.Blocks
{
    /// <summary>
    /// The look of everything around the blocks: the bundled fonts (as
    /// Avalonia families), line icons, and the toolbar's buttons. Kept apart
    /// from the IDE-wide Clay styles so the blocks editor can be bigger and
    /// friendlier without changing the code editor's chrome.
    /// </summary>
    internal static class BlocksChrome
    {
        private const string Fonts = "avares://DanaProcessing.Ide/Theme/Fonts#";

        /// <summary>Atkinson Hyperlegible: toolbar, Renglones, everything grown-up-ish.</summary>
        public static readonly FontFamily FontUi = new(Fonts + "Atkinson Hyperlegible");
        /// <summary>Andika: what the youngest read (Fichas, challenge goals in Fichas).</summary>
        public static readonly FontFamily FontKids = new(Fonts + "Andika");
        /// <summary>IBM Plex Mono: the C#.</summary>
        public static readonly FontFamily FontCode = new(Fonts + "IBM Plex Mono");

        public static readonly IBrush Ink = Brush("#2B2620");
        public static readonly IBrush InkSoft = Brush("#5E564B");
        public static readonly IBrush InkMuted = Brush("#968C7E");
        public static readonly IBrush Line = Brush("#E6DFD3");
        public static readonly IBrush Well = Brush("#EEE8DE");
        public static readonly IBrush Go = Brush("#3DAA67");
        public static readonly IBrush GoEdge = Brush("#2C8550");
        public static readonly IBrush Gold = Brush("#F2B53A");
        public static readonly IBrush GoldSoft = Brush("#FFF3D4");
        public static readonly IBrush GoldInk = Brush("#8A5A00");
        public static readonly IBrush Hover = Brush("#EAE3D7");
        public static readonly IBrush Pressed = Brush("#DDD4C5");

        public static IBrush Brush(string hex) => new SolidColorBrush(Avalonia.Media.Color.Parse(hex));

        // Line icons on a 24x24 grid, stroked with round caps (the same
        // language as the block icons drawn on the canvas).
        public static class Icons
        {
            public const string Play = "M7 4.5v15l12.5-7.5z";
            public const string Stop = "M7 5h10a2 2 0 0 1 2 2v10a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V7a2 2 0 0 1 2-2z";
            public const string Turtle = "M3.5 15a8.5 7 0 0 1 17 0z M20.5 13.5h.5a2 2 0 0 0 0-4h-1.5 M7 15v3 M17 15v3 M8.5 9.5l2 5.5 M15.5 9.5l-2 5.5";
            public const string Undo = "M9 14L4 9l5-5 M4 9h10.5a5.5 5.5 0 0 1 0 11H11";
            public const string Redo = "M15 14l5-5-5-5 M20 9H9.5a5.5 5.5 0 0 0 0 11H13";
            public const string Trophy = "M8 21h8 M12 17v4 M7 4h10v5a5 5 0 0 1-10 0z M17 6h3v1a3 3 0 0 1-3 3 M7 6H4v1a3 3 0 0 0 3 3";
            public const string Code = "M8.5 7l-5 5 5 5 M15.5 7l5 5-5 5 M13.5 4.5l-3 15";
            public const string Tiles = "M5 4h5a1 1 0 0 1 1 1v5a1 1 0 0 1-1 1H5a1 1 0 0 1-1-1V5a1 1 0 0 1 1-1z M14 4h5a1 1 0 0 1 1 1v5a1 1 0 0 1-1 1h-5a1 1 0 0 1-1-1V5a1 1 0 0 1 1-1z M5 13h5a1 1 0 0 1 1 1v5a1 1 0 0 1-1 1H5a1 1 0 0 1-1-1v-5a1 1 0 0 1 1-1z M14 13h5a1 1 0 0 1 1 1v5a1 1 0 0 1-1 1h-5a1 1 0 0 1-1-1v-5a1 1 0 0 1 1-1z";
            public const string Lines = "M4 6h16 M8 12h12 M8 18h9 M4 12h.01 M4 18h.01";
            public const string Forward = "M5 12h14 M13 6l6 6-6 6";
            public const string Bulb = "M9 18h6 M10 21h4 M12 3a6 6 0 0 0-3.8 10.6c.6.5.8 1.3.8 2.4h6c0-1.1.2-1.9.8-2.4A6 6 0 0 0 12 3z";
            public const string Star = "M12 3l2.7 5.6 6.1.9-4.4 4.3 1 6.1L12 17l-5.4 2.9 1-6.1-4.4-4.3 6.1-.9z";
            public const string Check = "M5 12.5l4.5 4.5L19 7.5";
            public const string Retry = "M20 12a8 8 0 1 1-2.6-5.9 M20 4v5h-5";
        }

        /// <summary>A button with an icon and (optionally) a label, styled by <paramref name="classes"/>.</summary>
        public static Button Button(string? icon, string? text, params string[] classes)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
            if (icon != null)
                row.Children.Add(new StrokeIcon(icon, filled: icon == Icons.Play || icon == Icons.Stop));
            if (text != null)
                row.Children.Add(new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center });
            var button = new Button { Content = row, VerticalAlignment = VerticalAlignment.Center };
            button.Classes.Add("bk");
            if (text == null)
                button.Classes.Add("icon");
            foreach (var c in classes)
                button.Classes.Add(c);
            return button;
        }

        /// <summary>Changes a button's label, keeping its icon.</summary>
        public static void SetText(Button button, string text)
        {
            if (button.Content is StackPanel row)
                foreach (var child in row.Children)
                    if (child is TextBlock t)
                        t.Text = text;
        }

        public static Control Divider() => new Border
        {
            Width = 1, Height = 26, Background = Line, Margin = new Thickness(4, 0), VerticalAlignment = VerticalAlignment.Center,
        };

        /// <summary>The blocks toolbar's button looks: .bk plain, .bk.go (Probar), .bk.on (a toggle that's on), .bk.seg (segmented), .bk.outline.</summary>
        public static Style[] Styles()
        {
            var transitions = new Transitions
            {
                new BrushTransition { Property = Avalonia.Controls.Button.BackgroundProperty, Duration = TimeSpan.FromMilliseconds(120) },
            };
            return new[]
            {
                Rule(x => x.OfType<Button>().Class("bk"),
                    (TemplatedControl.FontFamilyProperty, FontUi),
                    (TemplatedControl.FontSizeProperty, 15.0),
                    (TemplatedControl.FontWeightProperty, FontWeight.Bold),
                    (TemplatedControl.PaddingProperty, new Thickness(14, 0)),
                    (Layoutable.MinHeightProperty, 42.0),
                    (TemplatedControl.CornerRadiusProperty, new CornerRadius(12)),
                    (TemplatedControl.BorderThicknessProperty, new Thickness(0)),
                    (TemplatedControl.BackgroundProperty, Brushes.Transparent),
                    (TemplatedControl.ForegroundProperty, InkSoft),
                    (InputElement.CursorProperty, new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand)),
                    (Animatable.TransitionsProperty, transitions)),
                Rule(x => x.OfType<Button>().Class("bk").Class("icon"), (TemplatedControl.PaddingProperty, new Thickness(11, 0)), (Layoutable.MinWidthProperty, 42.0)),

                Presenter(x => x.OfType<Button>().Class("bk"), Brushes.Transparent, InkSoft),
                Presenter(x => x.OfType<Button>().Class("bk").Class(":pointerover"), Hover, Ink),
                Presenter(x => x.OfType<Button>().Class("bk").Class(":pressed"), Pressed, Ink),

                // A toggle that's on: warm gold, like the "lit" blocks.
                Presenter(x => x.OfType<Button>().Class("bk").Class("on"), GoldSoft, GoldInk),
                Presenter(x => x.OfType<Button>().Class("bk").Class("on").Class(":pointerover"), Brush("#FFEBBF"), GoldInk),

                // Probar: solid green with a darker edge underneath, like a tile.
                Rule(x => x.OfType<Button>().Class("bk").Class("go"),
                    (TemplatedControl.PaddingProperty, new Thickness(20, 0, 22, 0)),
                    (TemplatedControl.FontSizeProperty, 16.5),
                    (Layoutable.MinHeightProperty, 46.0)),
                Presenter(x => x.OfType<Button>().Class("bk").Class("go"), Go, Brushes.White, "0 3 0 0 #2C8550"),
                Presenter(x => x.OfType<Button>().Class("bk").Class("go").Class(":pointerover"), Brush("#46B872"), Brushes.White, "0 3 0 0 #2C8550"),
                Presenter(x => x.OfType<Button>().Class("bk").Class("go").Class(":pressed"), GoEdge, Brushes.White, "0 1 0 0 #2C8550"),

                // A white card with a hairline: secondary actions that lead somewhere.
                Presenter(x => x.OfType<Button>().Class("bk").Class("outline"), Brushes.White, Ink, "0 0 0 1.5 #E2D9CB"),
                Presenter(x => x.OfType<Button>().Class("bk").Class("outline").Class(":pointerover"), Brush("#FFFDF8"), Ink, "0 0 0 1.5 #CFC3B0"),

                // The two halves of the Fichas | Renglones switch.
                Rule(x => x.OfType<Button>().Class("bk").Class("seg"),
                    (Layoutable.MinHeightProperty, 36.0),
                    (TemplatedControl.CornerRadiusProperty, new CornerRadius(9)),
                    (TemplatedControl.PaddingProperty, new Thickness(12, 0))),
                Presenter(x => x.OfType<Button>().Class("bk").Class("seg").Class(":pointerover"), Brush("#E4DCCF"), Ink),
                Presenter(x => x.OfType<Button>().Class("bk").Class("seg").Class("on"), Brushes.White, Ink, "0 1 3 0 #22000000"),
                Presenter(x => x.OfType<Button>().Class("bk").Class("seg").Class("on").Class(":pointerover"), Brushes.White, Ink, "0 1 3 0 #22000000"),
            };
        }

        private static Style Rule(Func<Selector?, Selector> selector, params (AvaloniaProperty Property, object Value)[] setters)
        {
            var style = new Style(selector);
            foreach (var (p, v) in setters)
                style.Setters.Add(new Setter(p, v));
            return style;
        }

        // Fluent paints a button's background and text on its ContentPresenter
        // (and changes them on hover/press), so that's where they're set.
        private static Style Presenter(Func<Selector?, Selector> button, IBrush background, IBrush foreground, string? shadow = null)
        {
            var style = new Style(x => button(x).Template().OfType<ContentPresenter>());
            style.Setters.Add(new Setter(ContentPresenter.BackgroundProperty, background));
            style.Setters.Add(new Setter(ContentPresenter.ForegroundProperty, foreground));
            if (shadow != null)
                style.Setters.Add(new Setter(ContentPresenter.BoxShadowProperty, BoxShadows.Parse(shadow)));
            return style;
        }
    }

    /// <summary>
    /// A 24x24 line icon drawn in the inherited text color -- so it follows a
    /// button's hover and on/off colors with no extra wiring.
    /// </summary>
    internal sealed class StrokeIcon : Control
    {
        public static readonly StyledProperty<IBrush?> ForegroundProperty = TextElement.ForegroundProperty.AddOwner<StrokeIcon>();

        static StrokeIcon() => AffectsRender<StrokeIcon>(ForegroundProperty);

        private readonly Geometry _geometry;
        private readonly bool _filled;
        private readonly double _size;
        private readonly double _thickness;

        public StrokeIcon(string data, double size = 18, bool filled = false, double thickness = 2.2)
        {
            _geometry = Geometry.Parse(data);
            _size = size;
            _filled = filled;
            _thickness = thickness;
            VerticalAlignment = VerticalAlignment.Center;
        }

        public IBrush? Foreground
        {
            get => GetValue(ForegroundProperty);
            set => SetValue(ForegroundProperty, value);
        }

        protected override Size MeasureOverride(Size availableSize) => new(_size, _size);

        public override void Render(DrawingContext context)
        {
            var brush = Foreground ?? Brushes.Black;
            double k = _size / 24;
            using (context.PushTransform(Matrix.CreateScale(k, k)))
            {
                if (_filled)
                    context.DrawGeometry(brush, new Pen(brush, 1.5, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round), _geometry);
                else
                    context.DrawGeometry(null, new Pen(brush, _thickness, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round), _geometry);
            }
        }
    }
}
