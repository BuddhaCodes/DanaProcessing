using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using DanaProcessing;

namespace DanaProcessing.AvaloniaHost
{
    /// <summary>
    /// Standalone Avalonia window that runs a Sketch full-window. Thin wrapper
    /// around AvaloniaSketchCanvas.
    /// </summary>
    public class AvaloniaSketchWindow : Window
    {
        public AvaloniaSketchCanvas Canvas { get; }

        /// <summary>windowless: no OS title bar/caption buttons at all (see
        /// ExportedSketchRunner/SketchExporter — this is what "gadget mode"
        /// export produces) — dragged by Alt+left-click-holding anywhere on
        /// the drawing instead (plain left-click still reaches the sketch's
        /// own MousePressed/MouseClicked unmolested — see the Tunnel-phase
        /// remark below for why that split actually works), closed with
        /// Alt+F4 (still works with no visible chrome — it's an OS-level
        /// shortcut, not dependent on the title bar existing) since there's
        /// no close button to click.</summary>
        public AvaloniaSketchWindow(Sketch sketch, string title = "DanaProcessing Sketch", bool windowless = false)
        {
            Title = title;

            // NOT `Width = sketch.Width; Height = sketch.Height;` here: Setup()
            // (where a sketch actually calls Size(w, h)) doesn't run at
            // construction time — AvaloniaSketchCanvas runs it lazily, during its
            // own first MeasureOverride (see the remark there on why). Reading
            // sketch.Width/Height this early would still be whatever placeholder
            // value a fresh Sketch starts with, so the window would open at some
            // unrelated default size with the canvas's real (correctly sized)
            // content floating in the corner of it — exactly the black-bordered
            // window this class used to produce. SizeToContent fits the window to
            // whatever the canvas's own MeasureOverride reports instead, which is
            // correct precisely because it's driven by the same layout pass that
            // makes Setup() run in the first place, and keeps re-fitting if a
            // sketch calls Size() again later to resize itself. CanResize = false
            // makes that guarantee permanent — dragging the window bigger than
            // the canvas is exactly how the black borders came back otherwise.
            SizeToContent = SizeToContent.WidthAndHeight;
            CanResize = false;

            Canvas = new AvaloniaSketchCanvas(sketch);
            Content = Canvas;

            if (windowless)
            {
                WindowDecorations = WindowDecorations.None;

                // Full manual control over sizing instead of leaning on
                // SizeToContent (still used above for the non-windowless
                // case): running SizeToContent's own automatic re-fitting
                // AND a separate Min/Max lock at the same time turned out to
                // fight each other -- neither reliably won, so the window
                // still visibly resized in testing. One explicit owner
                // (this callback, setting Width/Height AND Min/Max together
                // every time) replaces that with a single, unambiguous
                // source of truth for this window's size. Starts at
                // Sketch's own known default (600x400) so it opens at a
                // sane size before Setup() has even run once.
                SizeToContent = SizeToContent.Manual;
                Width = 600;
                Height = 400;
                Canvas.SketchSizeChanged += (w, h) =>
                {
                    Width = w;
                    Height = h;
                    MinWidth = MaxWidth = w;
                    MinHeight = MaxHeight = h;
                };

                // Tunnel-only, and marking Handled ourselves: the previous
                // approach (listen on Bubble with handledEventsToo, after
                // Canvas's own PointerPressed had already run) never
                // actually triggered a drag in testing. Tunnel reaches this
                // handler FIRST, before Canvas (or anything else) sees the
                // event at all -- an Alt+left-click is consumed for the
                // drag right here and never reaches Canvas; a plain
                // left-click (no Alt) is left alone and continues on to
                // Canvas exactly as before, so sketch interaction is
                // unaffected.
                AddHandler(PointerPressedEvent, (_, e) =>
                {
                    if (e.KeyModifiers.HasFlag(KeyModifiers.Alt) && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                    {
                        e.Handled = true;
                        BeginMoveDrag(e);
                    }
                }, RoutingStrategies.Tunnel);
            }
        }
    }
}
