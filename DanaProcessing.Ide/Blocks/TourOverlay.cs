using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using DanaProcessing.Ide.Localization;

namespace DanaProcessing.Ide.Blocks
{
    /// <summary>
    /// The tour on screen: everything dims except the part being explained,
    /// which gets a gold ring, and a card next to it says what it is.
    /// Next / Done, Skip, or the keyboard (Enter, Esc).
    /// </summary>
    internal sealed class TourOverlay : Canvas
    {
        private IReadOnlyList<TourStep> _steps = Array.Empty<TourStep>();
        private Func<TourTarget, Rect?> _where = _ => null;
        private Action? _done;
        private int _index;

        private readonly Rectangle _top = Dim(), _bottom = Dim(), _left = Dim(), _right = Dim();
        private readonly Border _ring = new()
        {
            BorderBrush = BlocksChrome.Gold, BorderThickness = new Thickness(3), CornerRadius = new CornerRadius(14), IsHitTestVisible = false,
            BoxShadow = BoxShadows.Parse("0 0 0 4 #55FFD35E"),
        };
        private readonly Border _card;
        private readonly TextBlock _title, _text, _count;
        private readonly Button _next, _skip;

        public bool IsRunning => IsVisible;

        public TourOverlay()
        {
            IsVisible = false;
            Background = Brushes.Transparent; // the program underneath can't be touched during the tour
            Focusable = true;

            _title = new TextBlock { FontFamily = BlocksChrome.FontKids, FontSize = 21, FontWeight = FontWeight.Bold, Foreground = BlocksChrome.Ink, TextWrapping = TextWrapping.Wrap };
            _text = new TextBlock { FontFamily = BlocksChrome.FontUi, FontSize = 16, Foreground = BlocksChrome.InkSoft, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0), LineHeight = 22 };
            _count = new TextBlock { FontFamily = BlocksChrome.FontUi, FontSize = 13, FontWeight = FontWeight.Bold, Foreground = BlocksChrome.InkMuted, VerticalAlignment = VerticalAlignment.Center };
            _next = BlocksChrome.Button(BlocksChrome.Icons.Forward, Loc.Tr("Siguiente", "Next"), "go");
            if (_next.Content is StackPanel row && row.Children.Count == 2)
                row.Children.Move(0, 1);
            _next.Click += (_, _) => Next();
            _skip = BlocksChrome.Button(null, Loc.Tr("Saltar", "Skip"));
            _skip.Click += (_, _) => Finish();
            var buttons = new DockPanel { Margin = new Thickness(0, 16, 0, 0) };
            DockPanel.SetDock(_count, Dock.Left);
            buttons.Children.Add(_count);
            buttons.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, HorizontalAlignment = HorizontalAlignment.Right, Children = { _skip, _next } });
            _card = new Border
            {
                Width = 360, Background = Brushes.White, CornerRadius = new CornerRadius(18), Padding = new Thickness(20, 16),
                BoxShadow = BoxShadows.Parse("0 12 36 0 #40000000"),
                Child = new StackPanel { Children = { _title, _text, buttons } },
            };
            Children.Add(_top);
            Children.Add(_bottom);
            Children.Add(_left);
            Children.Add(_right);
            Children.Add(_ring);
            Children.Add(_card);
            KeyDown += (_, e) =>
            {
                if (e.Key is Key.Enter or Key.Right or Key.Space) { Next(); e.Handled = true; }
                else if (e.Key == Key.Escape) { Finish(); e.Handled = true; }
            };
            SizeChanged += (_, _) => { if (IsVisible) Place(); };
        }

        private static Rectangle Dim() => new() { Fill = new SolidColorBrush(Avalonia.Media.Color.FromArgb(150, 20, 16, 12)) };

        /// <summary>Starts the tour. <paramref name="where"/> says where each target is on this overlay (null: that step is skipped).</summary>
        public void Start(IReadOnlyList<TourStep> steps, Func<TourTarget, Rect?> where, Action? done = null)
        {
            _steps = steps;
            _where = where;
            _done = done;
            _index = -1;
            IsVisible = true;
            Focus();
            Next();
        }

        private void Next()
        {
            do
                _index++;
            while (_index < _steps.Count && _where(_steps[_index].Target) is null);
            if (_index >= _steps.Count)
            {
                Finish();
                return;
            }
            var step = _steps[_index];
            _title.Text = step.Title;
            _text.Text = step.Text;
            _count.Text = $"{_index + 1} / {_steps.Count}";
            bool last = _index == _steps.Count - 1 || NoMoreAfter(_index);
            BlocksChrome.SetText(_next, last ? Loc.Tr("¡Listo!", "Done!") : Loc.Tr("Siguiente", "Next"));
            _skip.IsVisible = !last;
            Place();
        }

        private bool NoMoreAfter(int i)
        {
            for (int k = i + 1; k < _steps.Count; k++)
                if (_where(_steps[k].Target) is not null)
                    return false;
            return true;
        }

        public void Finish()
        {
            IsVisible = false;
            var done = _done;
            _done = null;
            done?.Invoke();
        }

        /// <summary>The dim parts around the hole, the ring, and the card beside it.</summary>
        private void Place()
        {
            if (_index < 0 || _index >= _steps.Count || _where(_steps[_index].Target) is not { } r)
                return;
            double w = Bounds.Width, h = Bounds.Height;
            var hole = new Rect(Math.Max(0, r.X - 6), Math.Max(0, r.Y - 6), Math.Min(w, r.Width + 12), Math.Min(h, r.Height + 12));
            Set(_top, 0, 0, w, hole.Top);
            Set(_bottom, 0, hole.Bottom, w, Math.Max(0, h - hole.Bottom));
            Set(_left, 0, hole.Top, hole.Left, hole.Height);
            Set(_right, hole.Right, hole.Top, Math.Max(0, w - hole.Right), hole.Height);
            Set(_ring, hole.X, hole.Y, hole.Width, hole.Height);

            _card.Measure(Size.Infinity);
            double cw = _card.DesiredSize.Width, ch = _card.DesiredSize.Height;
            // Beside the hole if there's room (right, then left), else below or above it.
            double x, y;
            if (hole.Right + 18 + cw < w) { x = hole.Right + 18; y = hole.Top; }
            else if (hole.Left - 18 - cw > 0) { x = hole.Left - 18 - cw; y = hole.Top; }
            else if (hole.Bottom + 18 + ch < h) { x = hole.Left; y = hole.Bottom + 18; }
            else { x = hole.Left; y = hole.Top - 18 - ch; }
            SetLeft(_card, Math.Clamp(x, 12, Math.Max(12, w - cw - 12)));
            SetTop(_card, Math.Clamp(y, 12, Math.Max(12, h - ch - 12)));
        }

        private static void Set(Control c, double x, double y, double width, double height)
        {
            SetLeft(c, x);
            SetTop(c, y);
            c.Width = Math.Max(0, width);
            c.Height = Math.Max(0, height);
        }

        /// <summary>For tests: the step on screen.</summary>
        internal TourStep? Current => _index >= 0 && _index < _steps.Count && IsVisible ? _steps[_index] : null;
    }
}
