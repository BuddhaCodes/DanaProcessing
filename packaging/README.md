# DanaProcessing

Creative coding in C#, the Processing way: 2D (SkiaSharp), real 3D (OpenGL)
and a live-coding audio engine behind one `Sketch` class.

These packages put DanaProcessing sketches in **any .NET project**: the
same code you write in the DanaProcessing IDE runs unchanged.

| Package | What it is |
|---|---|
| `DanaProcessing` | The core: `Sketch`, drawing, 3D, audio, data. No UI of its own. |
| `DanaProcessing.AvaloniaHost` | Runs a sketch: `SketchApp.Run(...)` opens it in a window; `AvaloniaSketchCanvas` puts it inside your own Avalonia app. |
| `DanaProcessing.Templates` | `dotnet new dana-sketch`: a ready-to-run sketch project. |

## Quick start

```bash
dotnet new install DanaProcessing.Templates
dotnet new dana-sketch -n MySketch
cd MySketch
dotnet run
```

Or add it to a project you already have:

```bash
dotnet add package DanaProcessing.AvaloniaHost
```

```csharp
using DanaProcessing;
using DanaProcessing.AvaloniaHost;

SketchApp.Run(new Bubbles());

public class Bubbles : Sketch
{
    public override void Setup() => Size(600, 400);

    public override void Draw()
    {
        Background(20, 24, 40);
        Fill(255, 120, 180);
        Circle(MouseX, MouseY, 60);
    }
}
```

`SketchApp.Run()` with no arguments runs the only `Sketch` subclass in your
program, so a file copied straight out of the IDE runs as it is.

### Inside your own Avalonia app

```csharp
var canvas = new AvaloniaSketchCanvas(new Bubbles());
myPanel.Children.Add(canvas);
```

`using DanaProcessing;` is added for you when `ImplicitUsings` is on, like
in the IDE (turn it off with `<DanaProcessingImplicitUsings>false</DanaProcessingImplicitUsings>`).

Documentation and the full API: https://buddhacodes.github.io/DanaProcessing/

## VS Code

The **DanaProcessing** extension for VS Code creates, runs, live-codes and
exports these projects, and opens the IDE's examples:
[VS Code & NuGet](https://buddhacodes.github.io/DanaProcessing/reference/vscode-nuget.html).
