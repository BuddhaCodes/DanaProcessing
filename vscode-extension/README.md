# DanaProcessing for VS Code

Creative coding in C#, the Processing way, from VS Code. A sketch is a class with `Setup()` (runs once) and `Draw()` (runs every frame); this extension creates, runs, live-codes and exports them with the [DanaProcessing NuGet packages](https://www.nuget.org/packages/DanaProcessing.AvaloniaHost).

*[Español abajo](#español).*

## What it does

- **New sketch**: a ready-to-run project (`.csproj`, `Program.cs`, `Sketch.cs`, tasks and launch settings).
- **Open an example**: the DanaProcessing IDE's 87 examples (2D, 3D, sound, MIDI, ML.NET, GPU...), searchable by name, topic or description. Each one becomes its own project, with its NuGet packages.
- **Run** ▶ and **Live** ⚡ from the editor title bar, the status bar, or `Ctrl+Alt+R` / `Ctrl+Alt+L` (`Cmd` on macOS). Live runs `dotnet watch`: save and the running sketch picks up your edits. **Stop** with `Ctrl+Alt+S`. Compile errors go to the Problems panel.
- **Export as an app**: one self-contained file for Windows, macOS and/or Linux, in `export/<platform>/`, that runs without installing .NET. The `data/` folder goes with it.
- **IDE sketches run as they are**: `// nuget: NAudio, 2.2.1` lines become PackageReferences on Run, and **Make this sketch runnable** creates the project for a `.cs` file saved from the IDE. **Open in the DanaProcessing IDE** sends the current file to the IDE.
- **Add a NuGet package**: `NAudio` or `NAudio 2.2.1`.
- **Reference**: `Ctrl+Alt+H` opens the page of the name under the cursor; hovering a DanaProcessing name adds a link to it.
- **Snippets**: `sketch`, `setup`, `draw`, `mousePressed`, `keyPressed`, `grid`, `push`, `nuget`.
- A **Get started** walkthrough, and everything in English and Spanish (it follows VS Code's display language).

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).
- The [C# extension](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csharp) for completion, errors as you type and debugging (recommended in every new project).

## Settings

| Setting | Default | |
|---|---|---|
| `danaProcessing.packageVersion` | `1.*` | Version of `DanaProcessing.AvaloniaHost` for new sketches |
| `danaProcessing.dotnetPath` | `dotnet` | The `dotnet` command |
| `danaProcessing.idePath` | | The IDE, for *Open in the DanaProcessing IDE* (not needed on Windows) |
| `danaProcessing.openProjectIn` | `ask` | Where new sketches open |
| `danaProcessing.showStatusBar` | `true` | Run / Live / Stop in the status bar |
| `danaProcessing.referenceHover` | `true` | Reference links in hovers |

---

## Español

Programación creativa en C#, al estilo Processing, desde VS Code. Un sketch es una clase con `Setup()` (corre una vez) y `Draw()` (corre en cada cuadro); esta extensión los crea, los corre, los programa en vivo y los exporta con los paquetes NuGet de DanaProcessing.

- **Nuevo sketch**: un proyecto listo para correr.
- **Abrir un ejemplo**: los 87 ejemplos del IDE de DanaProcessing, con búsqueda por nombre, tema o descripción. Cada uno se vuelve su propio proyecto, con sus paquetes NuGet.
- **Correr** ▶ y **En vivo** ⚡ desde la barra del editor, la barra de estado o `Ctrl+Alt+R` / `Ctrl+Alt+L`. En vivo usa `dotnet watch`: guardas y el sketch que corre toma tus cambios. **Detener** con `Ctrl+Alt+S`. Los errores van al panel Problemas.
- **Exportar como app**: un solo archivo para Windows, macOS o Linux que corre sin instalar .NET, en `export/<plataforma>/`, con la carpeta `data/`.
- **Los sketches del IDE corren tal cual**: las líneas `// nuget: NAudio, 2.2.1` se vuelven PackageReferences al correr, y **Hacer ejecutable este sketch** crea el proyecto de un `.cs` guardado desde el IDE. **Abrir en el IDE de DanaProcessing** manda el archivo al IDE.
- **Agregar un paquete NuGet**, **referencia** del nombre bajo el cursor (`Ctrl+Alt+H`) y en el hover, **snippets** y un recorrido de **primeros pasos**.

Necesita el [SDK de .NET 10](https://dotnet.microsoft.com/download/dotnet/10.0) y, recomendada, la extensión de C#.

## License

MIT.
