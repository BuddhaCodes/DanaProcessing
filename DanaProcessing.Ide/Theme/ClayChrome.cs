using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace DanaProcessing.Ide.Theme
{
    /// <summary>
    /// The IDE's own window chrome, reusable by any secondary window: the
    /// same title bar as MainWindow (accent dot + title, "—" "▢" "✕" with the
    /// clay-chrome / clay-chrome-close styles), drag-to-move, double-click to
    /// maximize, and invisible edge grips for resizing -- since
    /// WindowDecorations.None removes the OS's own.
    ///
    /// Usage, at the end of a window's constructor (after Styles.AddRange(
    /// ClayTheme.ButtonEffectStyles()), which the buttons need):
    /// <code>ClayChrome.Apply(this, content);</code>
    /// </summary>
    public static class ClayChrome
    {
        public const double TitleBarHeight = 46;

        private static readonly IBrush Hairline = new SolidColorBrush(Avalonia.Media.Color.Parse("#E8E2DA"));

        /// <param name="window">Window to dress. Its Title is shown (and kept in sync) in the bar.</param>
        /// <param name="content">What goes under the title bar.</param>
        /// <param name="titleExtras">Optional control shown right after the title (e.g. a result count).</param>
        public static void Apply(Window window, Control content, Control? titleExtras = null,
                                 bool canMinimize = true, bool canMaximize = true)
        {
            window.WindowDecorations = WindowDecorations.None;

            var dot = new Ellipse
            {
                Width = 9,
                Height = 9,
                Fill = ClayTheme.Accent,
                VerticalAlignment = VerticalAlignment.Center,
            };
            var title = new TextBlock
            {
                Text = window.Title,
                Foreground = ClayTheme.TextPrimary,
                FontFamily = ClayTheme.FontDisplay,
                FontWeight = FontWeight.SemiBold,
                FontSize = 13.5,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
            };
            window.PropertyChanged += (_, e) =>
            {
                if (e.Property == Window.TitleProperty)
                    title.Text = window.Title;
            };

            var left = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 11,
                Margin = new Thickness(20, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Children = { dot, title },
            };
            if (titleExtras != null)
            {
                titleExtras.VerticalAlignment = VerticalAlignment.Center;
                left.Children.Add(titleExtras);
            }

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 4,
                Margin = new Thickness(0, 0, 12, 0),
                VerticalAlignment = VerticalAlignment.Center,
            };
            if (canMinimize)
            {
                var min = new Button { Content = "—", Classes = { "clay-chrome" } };
                min.Click += (_, _) => window.WindowState = WindowState.Minimized;
                buttons.Children.Add(min);
            }
            if (canMaximize && window.CanResize)
            {
                var max = new Button { Content = "▢", Classes = { "clay-chrome" } };
                max.Click += (_, _) => ToggleMaximized(window);
                buttons.Children.Add(max);
            }
            var close = new Button { Content = "✕", Classes = { "clay-chrome-close" } };
            close.Click += (_, _) => window.Close();
            buttons.Children.Add(close);

            var barGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            Grid.SetColumn(left, 0);
            Grid.SetColumn(buttons, 1);
            barGrid.Children.Add(left);
            barGrid.Children.Add(buttons);

            var bar = new Border
            {
                Height = TitleBarHeight,
                Background = ClayTheme.TitleBarBackground,
                BorderBrush = Hairline,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Child = barGrid,
            };
            // Same as MainWindow: buttons handle their own PointerPressed, so
            // only presses on the bar's empty area start a drag.
            bar.PointerPressed += (_, e) =>
            {
                if (e.GetCurrentPoint(bar).Properties.IsLeftButtonPressed)
                    window.BeginMoveDrag(e);
            };
            if (canMaximize && window.CanResize)
                bar.DoubleTapped += (_, _) => ToggleMaximized(window);

            var frame = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
            Grid.SetRow(bar, 0);
            Grid.SetRow(content, 1);
            frame.Children.Add(bar);
            frame.Children.Add(content);

            // Without OS decorations a secondary window has no edge of its own
            // and blends into the IDE behind it -- a hairline keeps it legible.
            var outline = new Border
            {
                BorderBrush = Hairline,
                BorderThickness = new Thickness(1),
                Child = frame,
            };

            window.Content = window.CanResize ? WithResizeGrips(window, outline) : outline;
        }

        private static void ToggleMaximized(Window window) =>
            window.WindowState = window.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

        /// <summary>
        /// Invisible strips/corners over the edges that forward a press into
        /// BeginResizeDrag -- the same trick as MainWindow.BuildResizeOverlay.
        /// </summary>
        private static Grid WithResizeGrips(Window window, Control content)
        {
            const double edge = 6;
            const double corner = 12;

            var root = new Grid();
            root.Children.Add(content);

            void Add(WindowEdge which, HorizontalAlignment h, VerticalAlignment v, double? width, double? height, StandardCursorType cursor)
            {
                var grip = new Border
                {
                    Background = Brushes.Transparent,
                    HorizontalAlignment = h,
                    VerticalAlignment = v,
                    Cursor = new Cursor(cursor),
                };
                if (width.HasValue)
                    grip.Width = width.Value;
                if (height.HasValue)
                    grip.Height = height.Value;
                grip.PointerPressed += (_, e) =>
                {
                    if (window.WindowState == WindowState.Normal && e.GetCurrentPoint(grip).Properties.IsLeftButtonPressed)
                        window.BeginResizeDrag(which, e);
                };
                root.Children.Add(grip);
            }

            Add(WindowEdge.North, HorizontalAlignment.Stretch, VerticalAlignment.Top, null, edge, StandardCursorType.TopSide);
            Add(WindowEdge.South, HorizontalAlignment.Stretch, VerticalAlignment.Bottom, null, edge, StandardCursorType.BottomSide);
            Add(WindowEdge.West, HorizontalAlignment.Left, VerticalAlignment.Stretch, edge, null, StandardCursorType.LeftSide);
            Add(WindowEdge.East, HorizontalAlignment.Right, VerticalAlignment.Stretch, edge, null, StandardCursorType.RightSide);
            Add(WindowEdge.NorthWest, HorizontalAlignment.Left, VerticalAlignment.Top, corner, corner, StandardCursorType.TopLeftCorner);
            Add(WindowEdge.NorthEast, HorizontalAlignment.Right, VerticalAlignment.Top, corner, corner, StandardCursorType.TopRightCorner);
            Add(WindowEdge.SouthWest, HorizontalAlignment.Left, VerticalAlignment.Bottom, corner, corner, StandardCursorType.BottomLeftCorner);
            Add(WindowEdge.SouthEast, HorizontalAlignment.Right, VerticalAlignment.Bottom, corner, corner, StandardCursorType.BottomRightCorner);

            return root;
        }
    }
}
