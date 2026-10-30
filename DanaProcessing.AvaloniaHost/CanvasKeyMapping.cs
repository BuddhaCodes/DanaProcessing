using Avalonia.Input;
using DanaProcessing;

namespace DanaProcessing.AvaloniaHost
{
    /// <summary>
    /// Avalonia key → Processing's Key/KeyCode, for AvaloniaSketchCanvas. Same
    /// rules as the attached-sketch host (see Attach/InputHooks.cs KeyMapping),
    /// so a sketch reads keys the same way wherever it runs. Pure: testable
    /// without a window.
    /// </summary>
    public static class CanvasKeyMapping
    {
        /// <summary>
        /// Arrows and modifiers: Key = CODED, KeyCode = UP/DOWN/LEFT/RIGHT/SHIFT/CONTROL/ALT.
        /// Enter, Backspace, Tab, Esc, Delete: Processing's chars (ENTER, BACKSPACE...).
        /// A key that types a character: that character, from <paramref name="keySymbol"/>
        /// (so Shift and the keyboard layout count: 'A', 'ñ', '@'), with KeyCode =
        /// its upper-case code ('A' = 65, like Java's VK codes). Without a
        /// symbol, letters/digits/space/+/- still map (lower-case letters).
        /// Anything else: '\0' and 0.
        /// </summary>
        public static (char Key, int KeyCode) Map(Key key, string? keySymbol)
        {
            switch (key)
            {
                case Key.Up: return (Sketch.CODED, Sketch.UP);
                case Key.Down: return (Sketch.CODED, Sketch.DOWN);
                case Key.Left: return (Sketch.CODED, Sketch.LEFT);
                case Key.Right: return (Sketch.CODED, Sketch.RIGHT);
                case Key.LeftShift or Key.RightShift: return (Sketch.CODED, Sketch.SHIFT);
                case Key.LeftCtrl or Key.RightCtrl: return (Sketch.CODED, Sketch.CONTROL);
                case Key.LeftAlt or Key.RightAlt: return (Sketch.CODED, Sketch.ALT);
                case Key.Enter: return (Sketch.ENTER, 10);
                case Key.Back: return (Sketch.BACKSPACE, 8);
                case Key.Tab: return (Sketch.TAB, 9);
                case Key.Escape: return (Sketch.ESC, 27);
                case Key.Delete: return (Sketch.DELETE, 127);
            }

            if (keySymbol is { Length: 1 } && !char.IsControl(keySymbol[0]))
                return (keySymbol[0], char.ToUpperInvariant(keySymbol[0]));

            char c = key switch
            {
                >= Key.A and <= Key.Z => (char)('a' + (key - Key.A)),
                >= Key.D0 and <= Key.D9 => (char)('0' + (key - Key.D0)),
                >= Key.NumPad0 and <= Key.NumPad9 => (char)('0' + (key - Key.NumPad0)),
                Key.Space => ' ',
                Key.OemPlus or Key.Add => '+',
                Key.OemMinus or Key.Subtract => '-',
                _ => '\0',
            };
            return (c, c == '\0' ? 0 : char.ToUpperInvariant(c));
        }
    }
}
