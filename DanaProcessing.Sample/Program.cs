using System;
using System.Windows;
using DanaProcessing;

namespace DanaProcessing.Sample
{
    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            var app = new Application();
            var sketch = new MySketch();

            // Standalone (this sample): a whole window dedicated to the sketch.
            var window = new SketchWindow(sketch, "DanaProcessing Sample");

            // Embedded alternative (for reference): drop a SketchCanvas into any
            // existing WPF layout, e.g. inside a Grid alongside other controls:
            //
            //   var grid = new Grid();
            //   var canvas = new SketchCanvas(sketch);
            //   grid.Children.Add(canvas);
            //   grid.Children.Add(someOtherControl);
            //   window.Content = grid;

            app.Run(window);
        }
    }
}
