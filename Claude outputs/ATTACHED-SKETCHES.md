# Sketches adjuntos (AttachedSketch)

Un sketch sin ventana propia: su canvas es la ventana de otra app (Windows
Terminal por defecto). La sigue por la pantalla, toma su tamaño, se esconde
cuando se minimiza, siente cómo la mueves y (si lo pide) lee lo que escribes y
dónde haces clic. El seguimiento es el de Abyss Tide: polling cada frame con
DWM y una ventana layered transparente a los clics.

Referencia de la API para usuarios: `reference/attached.html` (fuente:
`site/content/attached.html`, se genera con `python site/build.py`).

```csharp
public class Water : AttachedSketch
{
    public override void Settings() => Attach(WindowTarget.Terminal);   // antes de buscar la ventana
    public override void Setup() => ListenToTargetKeys();               // opcional: teclas de la terminal

    public override void KeyTyped() { /* una gota */ }
    public override void TargetHidden() { /* minimizada */ }

    public override void Draw()
    {
        Clear();                                   // transparente: la terminal se ve debajo
        var a = TargetAcceleration;                // inercia: el agua siente -a
        // …
    }
}
```

## Samples

| Sample | Muestra |
|---|---|
| Lluvia sobre la terminal | Lo mínimo: `Attach`, `Clear()`, `WindowResized()`, `MouseX/MouseY` |
| Agua en la terminal | `TargetAcceleration` como fuerza (inercia), `ListenToTargetKeys/Mouse`, `IsTargetFocused`, `TargetShown()` |
| Burbujas del teclado | `KeyTyped/KeyPressed` desde la terminal, `MousePressed`, `TargetHidden/Shown`, inercia en partículas |
| Campo vectorial en la terminal | `AttachMode.EachWindow` (un campo por terminal), `ListenToShell` + `CommandFinished` (tormenta roja con el código de salida / pulso cian), inercia y giro del campo al arrastrar, `DeltaTime`. Idea de "Vector field with particles" (jcponcemath, OpenProcessing, código oculto y CC BY-NC-SA): escrito de cero, solo se toma la idea y se cita |
| Tela sobre toda la terminal | Tela Verlet translúcida que cubre toda la ventana, colgada del borde de arriba: `-TargetAcceleration` (limitada) la balancea y la pliega, brisa con `Noise`, `ListenToTargetMouse` (el ratón la empuja, clic la corta), un error la rasga y la tiñe de rojo, un comando correcto le manda una ráfaga, `dana-signal reset` cuelga una nueva. Relleno en 6 tonos (contornos, sin antialias) + trama hairline. Idea de "Cloth!!" (Millie, OpenProcessing, código oculto): escrito de cero |
| Blobs en la terminal | Lámpara de lava: los blobs flotan (cohesión hacia el centro de masa, deriva con `Noise`, poca gravedad), se funden (misma área y momento) y sueltan gotitas de vez en cuando. Inercia fuerte al arrastrar; teclas sueltan gotas que se unen; un error los revienta en gotas rojas que vuelan y luego se vuelven a unir; un éxito los hace latir. Relleno exacto con marching squares en una sola malla sombreada por el campo. Idea de "Blobules" (OpenProcessing, código oculto): escrito de cero |
| Funciones ondulantes | `EachWindow`: 13 cintas (suma de senos) en el 40 % inferior, cada línea es además una cuerda con ecuación de onda: el arrastre agita cada capa con su resorte (las de abajo más tarde) y desliza las ondas, cada tecla pulsa las cuerdas (`ListenToTargetKeys`), el cursor las aparta, error → maraña roja, éxito → onda de izquierda a derecha. Idea de "Class 8/31" (Tyler Alexander, Genuary 2024 "wobbly function", código oculto): escrito de cero |
| Blob CSS en la terminal | El blob de CSS: `border-radius` de 8 valores (4 esquinas × radio horizontal/vertical) animado entre 4 keyframes con ease-in-out, cada esquina un cuarto de elipse con `BezierVertex` y la regla de CSS para radios que no caben; `LinearGradientFill` que gira, glow con copias más grandes y tenues, brillo con `RadialGradientFill`. Resorte + `-TargetAcceleration` (se queda atrás), se estira en la dirección del movimiento, se inclina al cursor, late con teclas, rojo y tembloroso con error, se infla con éxito. Inspirado en "Create Smooth Blob Animation with CSS" (Code With Nazia, YouTube): escrito de cero |
| Audio reactivo en la terminal | Port de "Audio reactivo: FFT en vivo": `// nuget: NAudio`, `WasapiLoopbackCapture` (audio del sistema), FFT de 1024 con 40 bandas logarítmicas → 40 blobs con glow sobre tallos en el borde inferior; silencio (con histéresis) → se sueltan y suben flotando, vuelven con la música; inercia al arrastrar; error → lanzados hacia arriba en rojo; éxito → ola de luz. Captura en `Setup()`, se detiene en `OnStop()` (arregla el aviso del original de que la captura seguía viva). Fuera de Windows muestra "audio del sistema: solo Windows" |

## Cómo está armado

| Pieza | Archivo | Qué hace |
|---|---|---|
| API del sketch | `DanaProcessing/Sketch/AttachedSketch.cs` | `AttachedSketch`, `Attach()`, `WindowTarget` (Terminal / Class / Process), `AttachLayer` (Above / Behind), `TargetX/Y`, `TargetScale`, `IsAttached` |
| Movimiento, eventos, input | `DanaProcessing/Sketch/AttachedSketch.Target.cs` | `TargetVelocity/Acceleration` (suavizado tipo Abyss Tide), `TargetMoved/Focused/Blurred/Shown/Hidden()`, `ListenToTargetKeys/Mouse()`. Lógica pura: el host le pasa `TargetSample`/`TargetInput` con `Track()`, `TrackCursor()`, `DispatchTargetInput()` |
| Tiempo, eco | `DanaProcessing/Sketch/AttachedSketch.Runtime.cs` | `DeltaTime`, `EcoMode()/NoEco()/IsEco/EcoFrameRate`; `EcoPolicy` (pura) decide cuándo bajar fps |
| Señales del shell | `DanaProcessing/Sketch/AttachedSketch.Shell.cs` | `ShellSignal` (formato `ok 0 pid` / `error 3 pid` / `signal nombre pid`), `ShellSignals` (snippet de PowerShell, `Install/Uninstall/IsInstalled/InstallForPowerShell`), `ListenToShell/CommandFinished/OnShellSignal` |
| Host | `DanaProcessing.AvaloniaHost/Attach/AttachedSketchHost.cs` | Hilo propio con message loop: busca el objetivo (o usa uno fijo), mide, redimensiona, corre `Settings()` → `Setup()` → eventos → `RenderFrame()`, presenta. Eco (fps por frame), pausa, `DeltaTime`, batería cada 10 s. Hot reload y crash como el canvas |
| Flota | `Attach/AttachedSketchFleet.cs` | Lo que usan la IDE y la bandeja: un host (`Front`) o uno por ventana (`EachWindow`, escáner cada 0,5 s, instancias nuevas con `Seed + n`, `OnStop()` al cerrar la ventana). Hot reload a todas, pausa, estado agregado e insignias. Lee la lista de hosts sin lock (snapshot) para no bloquearse con el hot reload |
| Señales (host) | `Attach/ShellSignalHub.cs` | Un timer por proceso vigila `%LOCALAPPDATA%\DanaProcessing\shell-signal.txt` y entrega a la ventana al frente / la última enfocada / todas (`Route`, pura). `Power.IsLowBattery()` |
| Bandeja | `Attach/AttachedSketchTray.cs` | Icono (gota dibujada con Skia), menú: estado, Pausar/Reanudar, Instalar señales de PowerShell, Salir |
| Seguimiento | `Attach/WindowTracker.cs` | `EnumWindows` por clase o proceso, `DWMWA_EXTENDED_FRAME_BOUNDS`, `DWMWA_CLOAKED`, DPI por ventana |
| Input | `Attach/InputHooks.cs` | Hooks `WH_KEYBOARD_LL`/`WH_MOUSE_LL` en un hilo aparte (GetMessage) → cola. Filtra: teclas solo con el objetivo al frente; clics solo dentro del área y con el objetivo bajo el cursor. `KeyMapping` (vk → Key/KeyCode con `ToUnicodeEx`) e `InputGeometry` son puros |
| Ventana | `Attach/LayeredWindow.cs` | Popup layered + click-through; el `SKSurface` dibuja directo en un DIB BGRA premultiplicado → `UpdateLayeredWindow` sin copias. Orden Z justo encima (o debajo) del objetivo |
| Eventos | `Attach/EventDriven.cs` | `FrameScheduler` (cuándo corre un frame; un wake corre uno al instante), `WinEventFilter` + `WinEventHooks` (mover/redimensionar/minimizar/ocultar/cerrar/primer plano), `AttachedHostStats` + `StatsMeter` (fps, costo de Draw y de presentar, latencia de señal/tecla/movimiento), `FileSignalWatcher` |
| P/Invoke | `Attach/Win32.cs` | Todo lo nativo |
| IDE | `AvaloniaSketchCanvas.cs` | Si el sketch es `AttachedSketch`, no lo corre: arranca una flota y muestra una tarjeta de estado (520×240) con insignias (N VENTANAS, TECLADO/RATÓN en naranja, SHELL, ECO, PAUSA) y aviso si falta el snippet. Run, ⚡ Hot Reload, ■ Stop, pausa y errores pasan por el mismo camino de siempre |
| Export | `Ide/Export/ExportedSketchRunner.cs`, `App.cs`, `ExportOptionsWindow.cs`, `MainWindow.cs` | Un `AttachedSketch` exportado arranca como app de bandeja (`ShutdownMode.OnExplicitShutdown`, sin ventana); el diálogo de export lo explica en vez de ofrecer "sin barra de título" |
| Samples | `DanaProcessing.Ide/Editor/SketchSamples.Attached.cs` | Los tres de arriba |
| Docs | `site/content/attached.html`, `site/site.json`, `site/keywords.json` | Página "Attached sketches" en Tooling, 21 entradas en el índice A–Z |

Orden de un frame en el host: medir → (primera vez) `Setup()` → `Track()` (eventos de
ventana y movimiento) → `TrackCursor()` (`MouseMoved`) → input de los hooks →
`Draw()` → presentar. Todo en el hilo del host, nunca en medio de un `Draw()`.

Coordenadas: el sketch dibuja en píxeles lógicos (`Width`/`Height` = tamaño
físico / escala del monitor); el host escala el canvas por `TargetScale`.

## Pruebas

| Archivo | Cubre |
|---|---|
| `Tests/Core/AttachedSketchTests.cs` | API, tamaño/`WindowResized`, host sin Windows, hot reload con host detenido |
| `Tests/Core/AttachedSketchEventsTests.cs` | Eventos (sin disparo inicial, foco, show/hide una vez, moved), convergencia de velocidad, frenazo = aceleración negativa fuerte, clamp, teclas/clics solo con opt-in, `MouseMoved` solo dentro |
| `Tests/Core/AttachedInputMappingTests.cs` | vk → Key/KeyCode (flechas, modificadores, Enter/Backspace/…, layout), pantalla → coordenadas lógicas |
| `Tests/Ide/AttachedSampleTests.cs` | Cada sample adjunto compilado como en la IDE y paseado por una terminal falsa (arrastre, sacudida, tecleo, clic, foco, ocultar, redimensionar); el agua se va a la pared tras un frenazo; las burbujas aparecen al escribir; el campo vectorial es uno por ventana, se pone rojo con un error y se calma, y sus partículas van al revés del arrastre y rebotan al frenar |
| `Tests/Support/AttachedDriver.cs` | Conduce un `AttachedSketch` como el host pero en memoria — para tests nuevos |
| `Tests/Ide/DocsExamplesTests.cs` | Acepta `: AttachedSketch` y exige el ejemplo de `attached.html` |
| `Tests/Core/ShellSignalsTests.cs` | Formato de señales (parse/ida y vuelta), el snippet (envuelve el prompt, no escribe el comando), instalar idempotente / conservar el perfil y CRLF / desinstalar / reemplazar una copia vieja, opt-in de `ListenToShell` |
| `Tests/Core/AttachedRuntimeTests.cs` | `EcoPolicy` (tabla), fps eco nunca acelera, batería baja, `DeltaTime`, `AttachMode`, flota sin Windows, insignias, ruteo de señales, tooltip de la bandeja, detección de sketch adjunto para export |
| `Tests/Core/SketchStartupAndKeysTests.cs` | `RunStartup()` (orden, Settings que falla), `RaiseKeyDown()` (typed solo si escribe algo), `CanvasKeyMapping` (flechas, modificadores, especiales, Shift/layout, sin símbolo) |

Lo que necesita escritorio real (ventanas, hooks, DPI) se prueba a mano:

## Probar a mano (Windows)

1. Samples → "Lluvia sobre la terminal" → Run, con Windows Terminal abierta. Mover/redimensionar/maximizar la sigue; minimizar la esconde; otra ventana encima la tapa; clics y teclado llegan a la terminal; ■ Stop la quita; ⚡ conserva el estado. ✅ (25-oct-2026)
2. "Agua en la terminal": arrastrar y soltar de golpe → el agua choca con la pared; tirón hacia abajo → salta; escribir → gotas; Enter → salpica; clic → gota donde clicas; cambiar a otra ventana → se atenúa.
3. "Burbujas del teclado": escribir en la terminal → burbujas con la letra correcta (probar mayúsculas, ñ/acentos, AltGr+@); Backspace revienta la última; Enter todas; escribir en **otra** app → no aparece nada; clic sobre la terminal → anillo de chispas y la terminal recibe el clic; clic en una ventana que tapa la terminal → nada; minimizar/restaurar → contador.
4. Monitor con otra escala (125 %, 150 %): tamaños iguales y clics donde el cursor.
5. Un `Draw()` lento a propósito (`Delay`/bucle) con `ListenToTargetKeys()`: escribir en cualquier app no debe ir con retraso.
6. "Campo vectorial en la terminal" con 2–3 ventanas de Windows Terminal abiertas: cada una con su campo (distinto); abrir otra → aparece su campo en ≤ 0,5 s; cerrar una → desaparece sin afectar a las otras; la tarjeta dice "N VENTANAS".
7. Señales: ejecutar `ShellSignals.InstallForPowerShell()` (o el menú de la bandeja), abrir una terminal nueva, `cmd /c exit 3` → tormenta roja con "exit 3" solo en esa ventana; `dir` → pulso cian; Enter vacío → nada; `dana-signal reset` → campo nuevo. Revisar que el perfil conserva lo que ya tenía y que el prompt sigue igual (oh-my-posh/starship incluidos).
8. Eco: con la terminal visible pero sin foco (otra ventana al lado) → sigue a 60 fps, sin ECO; taparla entera con otra ventana (maximizada) 3 s → insignia ECO (15 fps); destaparla, traerla al frente o arrastrarla → vuelve a 60; con portátil en batería < 25 % → ECO siempre. El movimiento no debe ir más lento en eco (usa `DeltaTime`).
9. Pausa: ⏸ de la IDE → el overlay desaparece y vuelve donde estaba.
10. Exportar "Campo vectorial…": doble clic en el exe → sin ventana, icono de gota en la bandeja; menú: estado, Pausar/Reanudar (y clic en el icono), Instalar señales, Salir (el overlay desaparece y el proceso termina).
11. **Arrastre sin pausas**: con "Tela sobre toda la terminal" o "Campo vectorial…", arrastrar la terminal en círculos varios segundos → el sketch sigue animando (la tela se balancea mientras arrastras, no solo al soltar) y el overlay no se queda atrás; la métrica "mover" de la tarjeta queda en pocos ms.
12. "Blobs en la terminal": flotan en medio (no en el piso); escribir → gotas pequeñas que nadan hasta el grupo y se unen; Backspace quita; arrastrar/sacudir → salen lanzados al otro lado y rebotan; `cmd /c exit 1` → revientan en gotas rojas que vuelan y en unos segundos se vuelven a unir; `dir` → laten.
14. "Tela sobre toda la terminal": cubre toda la ventana y el texto se lee debajo; arrastrar → se balancea y hace pliegues sin "volarse"; clic → agujero; `cmd /c exit 1` → rasgón y rojo; `dir` → ráfaga.
13. "Funciones ondulantes" con 2 terminales: cada una con sus cintas; arrastrar una → solo esa se agita (las capas de abajo más tarde); escribir → las cuerdas vibran (Enter fuerte); pasar el cursor → las aparta; error → maraña roja; comando correcto → onda que cruza.
15. "Blob CSS en la terminal": el blob cambia de forma suave y gira; arrastrar → se queda atrás y se estira; cursor cerca → se inclina; teclear → late; `cmd /c exit 1` → rojo y tiembla; `dir` → se infla verde-azul.
16. "Audio reactivo en la terminal": poner música → 40 blobs bailan en el borde de abajo (graves a la izquierda); pausar la música → se sueltan y suben; reanudar → vuelven a sus tallos; arrastrar → se balancean; ■ Stop → la captura se detiene (el indicador de audio de Windows no queda abierto).
17. Instalador: abrir "Tela sobre toda la terminal" → Archivo → Exportar, dejar marcadas "Un sketch por cada terminal" e "Instalador de PowerShell" → en la carpeta exportada, clic derecho en `install.ps1` → Ejecutar con PowerShell. Debe: copiar a `%LOCALAPPDATA%\DanaProcessing\Sketches\…`, arrancar (icono en la bandeja), poner la tela en **cada** ventana de Windows Terminal, crear el acceso directo en `shell:startup`, y en una terminal nueva `cmd /c exit 1` → rasgón. Ejecutar `install.ps1` otra vez → reemplaza la copia sin duplicar el overlay. Doble clic en el exe instalado con él ya corriendo → no aparece un segundo overlay. Cerrar sesión y volver → arranca solo. `install.ps1 -Uninstall` → desaparece el overlay, el acceso directo y la carpeta.
18. Perfil propio: exportar "Blobs en la terminal" con las tres casillas → `install.ps1` → reiniciar Windows Terminal → aparece el perfil "PowerShell · …" en la flecha de nueva pestaña. En ese perfil los blobs se ven detrás del texto (fondo oscuro, texto legible); en una pestaña de otro perfil (opaco) no se ven. Arrastrar la ventana con el perfil → reaccionan. `install.ps1 -Uninstall` → el perfil desaparece al reiniciar la terminal.

## Lo que dice el código de Windows Terminal (microsoft/terminal, 23-oct-2026)

Revisado para integrarnos mejor y entender las caídas de FPS:

- **Ritmo de frames:** Terminal dibuja con AtlasEngine (Direct3D 11) en una swap chain *flip* y espera su *frame latency waitable object* antes de cada frame (`AtlasEngine::_waitUntilCanRender`): va al ritmo de la pantalla. Nosotros usábamos un temporizador, que se desfasa poco a poco respecto al refresco: aunque la tarjeta diga 60 fps, a ratos caen dos frames en un refresco y ninguno en el siguiente (tirón). **Hecho:** el host pregunta a DWM cuándo es el refresco (`DwmGetCompositionTimingInfo`) y arranca cada frame justo después de uno, con un intervalo de refrescos enteros (60 fps en 144 Hz = cada 2 refrescos, 72 fps, parejo).
- **Transparencia:** con `opacity` < 100 Terminal crea la swap chain con `DXGI_ALPHA_MODE_PREMULTIPLIED` sobre una superficie de DirectComposition (`CreateSwapChainForCompositionSurfaceHandle`); el fondo es un `SolidColorBrush` de XAML con esa opacidad. DWM mezcla la terminal con lo que hay detrás: por eso funciona dibujar *detrás* (como Abyss Tide).
- **Título de la ventana = título de la pestaña activa** (`TerminalPage::Title`, si `showTitleInTitlebar` está activo, que es lo normal). **Hecho:** el perfil exportado fija `tabTitle` + `suppressApplicationTitle`, y el sketch solo dibuja mientras esa pestaña está al frente (en otra pestaña, opaca, no se vería y gastaba CPU). Un evento de cambio de nombre (`EVENT_OBJECT_NAMECHANGE`) lo despierta al cambiar de pestaña.
- **Shaders de Terminal (`experimental.pixelShaderPath`):** reciben solo `Time`, `Scale`, `Resolution`, `Background` y la imagen de `experimental.pixelShaderImagePath`, que se carga **una vez** (WIC) al aplicar la configuración; la recarga en caliente de shaders es solo para compilaciones de depuración (`ATLAS_DEBUG_SHADER_HOT_RELOAD`). No hay forma de pasarle frames ni estado desde fuera: un sketch interactivo no puede vivir dentro del shader. Si el shader usa `Time`, Terminal redibuja continuamente.
- **No hay API de extensiones** para dibujar dentro de la terminal desde otro proceso; los fragments solo añaden perfiles, esquemas y acciones.

**Siguiente paso posible (fase 2, GPU):** presentar como lo hace Terminal: una ventana con DirectComposition y una swap chain *flip* con alfa premultiplicado, esperando su waitable object, en vez de `UpdateLayeredWindow` (que copia todo el bitmap por GDI a DWM en cada frame). Sigue dibujando Skia en CPU y sube la imagen a una textura; o Skia sobre Direct3D más adelante.
19. Ritmo: con "Tela" o "Funciones ondulantes" mirar la tarjeta: debe decir 60 fps (72 en 144 Hz) y casi nunca "N tardíos (peor X ms)". Si aparecen tardíos, anotar cuándo (escribiendo, arrastrando, en reposo).
20. Perfil propio (exportar de nuevo y reinstalar): con dos pestañas en la misma ventana, una del perfil del sketch y otra normal, cambiar entre ellas → en la normal la tarjeta/bandeja dice "Otra pestaña al frente…" y el uso de CPU baja; al volver, el sketch sigue al instante.
21. GPU: exportar "Tela sobre toda la terminal" con "Usar la GPU" (y sin perfil propio, para verla encima) → instalar → la tela se ve igual que antes, la tarjeta/estado no dice "sin GPU", los clics llegan a la terminal, arrastrar la ventana sigue bien y "N tardíos" casi nunca aparece. Repetir con perfil propio (detrás). Si se ve **nada** con GPU y sí sin ella, anotarlo: sería la combinación layered + DirectComposition.
22. Un sketch por panel: exportar con "Un sketch por cada terminal" + perfil propio, instalar, abrir el perfil y dividir (Alt+Shift+D) → cada panel tiene su propio sketch (distinto, con su borde en la división), en ≤ ¼ s. Mover el divisor → cada sketch se ajusta a su panel y conserva su estado. Dividir con otro perfil → ese panel no tiene sketch. Cambiar a otra pestaña y volver → los sketches siguen donde estaban (se esconden mientras tanto). Cerrar un panel → su sketch desaparece, el otro sigue.
23. Con paneles: abrir el menú contextual de la pestaña (clic derecho) y el desplegable "∨" → los sketches **no** desaparecen. Dividir en dos, ejecutar `cmd /c exit 1` en el panel izquierdo → solo el sketch izquierdo reacciona; repetir en el derecho. `Start-Sleep 5; cmd /c exit 1` en el izquierdo y pasar al derecho antes de que termine → reacciona el izquierdo. Redimensionar la ventana arrastrando un borde → cada sketch se queda pegado a su panel (sin desfase bajo la barra de pestañas).

## Pendiente

Estado: `[ ]` por hacer · `[~]` empezado · `[x]` hecho

### MVP
- [x] `AttachedSketch` + `Attach()` + `WindowTarget` + `AttachLayer`
- [x] Host Win32 con su hilo, seguimiento por polling, DPI y orden Z
- [x] Integración con la IDE (Run, Hot Reload, Stop, errores, tarjeta de estado)
- [x] Sample "Lluvia sobre la terminal" y tests de la API
- [x] Probado a mano en Windows
- [ ] Correr la suite completa de tests en CI (Windows/macOS/Linux)

### Eventos
- [x] `TargetVelocity` / `TargetAcceleration` / `IsTargetMoving` (suavizados, con clamp)
- [x] `TargetMoved()`, `TargetFocused()` / `TargetBlurred()`, `TargetShown()` / `TargetHidden()`
- [x] `ListenToTargetKeys()`: hook de teclado opcional, solo con el objetivo al frente → `KeyPressed/KeyTyped/KeyReleased`
- [x] `ListenToTargetMouse()`: hook de ratón opcional, solo dentro del área y con el objetivo debajo → `MousePressed/Released/Clicked/Wheel`
- [x] `MouseMoved()` por polling del cursor (sin hook)
- [x] Samples "Agua en la terminal" y "Burbujas del teclado", página de referencia
- [ ] **Probar a mano** los puntos 2–5 de arriba
- [x] Señales del shell: `ListenToShell()`, `CommandFinished()`, `OnShellSignal()`, `ShellSignals.InstallForPowerShell()`, ruteo a la ventana al frente
- [x] Aviso en la IDE cuando un sketch activa un hook (insignias TECLADO/RATÓN en naranja, SHELL, ECO, N VENTANAS, PAUSA)
- [ ] **Probar a mano** los puntos 6–10 de arriba
- [x] **Bug (oh-my-posh):** el snippet v1 iba en `profile.ps1` (todos los hosts), que carga ANTES de `$PROFILE`; oh-my-posh se inicia en `$PROFILE` y reemplazaba el prompt → sin señales. Además, envolver el prompt cambiaba `$?` y el tema habría mostrado éxito. Arreglado: va al final de `$PROFILE` (ruta preguntada a `powershell.exe`/`pwsh.exe`), se vuelve a envolver en `PowerShell.OnIdle` tras cargar todos los perfiles (sin envolver dos veces), y devuelve `$?`/`$LASTEXITCODE` al tema (truco de starship). La reinstalación quita la copia vieja de `profile.ps1`; `IsInstalled` da falso para una copia vieja. Menú **Herramientas → Instalar señales de PowerShell** en la IDE.
- [x] **Revisado contra el hook de Abyss Tide** (que sí funciona con oh-my-posh): la diferencia clave era que Abyss re-envuelve en **cada** `OnIdle`, no una sola vez. Adoptado: OnIdle persistente (sin `-MaxTriggerCount`/`-SupportEvent`, así `Get-EventSubscriber` lo muestra), guarda contra cargar el perfil dos veces, `[IO.File]::WriteAllText` en vez de `Set-Content`. Encontrado un problema latente en los dos hooks: dos envoltorios que re-envuelven en OnIdle se envolverían entre sí sin fin. Arreglado con una convención compartida (`# __PromptWrapper:variable` + recorrer la cadena) en Dana **y en `abyss-tide/install.ps1`** (hay que volver a ejecutar `install.ps1` para que el perfil tenga la versión nueva).
- [x] Dana también lee `%LOCALAPPDATA%\AbyssTide\signal.txt`: con el hook de Abyss Tide no hace falta instalar nada más. Con los dos hooks, cada comando cuenta una vez (`SignalMerger`: la señal de Abyss espera un poll; gana la de Dana, que trae el código de salida).
- [x] "Probar: comando fallido" en Herramientas (IDE) y en el menú de la bandeja, como el de Abyss Tide.
- [x] **Bug: la consola se colgaba tras un error.** Causa: los envoltorios leían su prompt interior de una variable global en el momento de llamarlo. Con dos hooks que re-envuelven (Dana v2 con su OnIdle de una vez + Abyss Tide en cada OnIdle), uno re-envolvía al otro y la variable global pasaba a apuntar al de afuera: A → D → A → … recursión sin fin en el prompt. Arreglado en Dana **y en `abyss-tide/install.ps1`**: el prompt interior se captura en un closure (`GetNewClosure`), así la cadena solo puede apuntar a prompts anteriores y nunca forma un ciclo; la búsqueda en la cadena lee la variable del closure; y si el envoltorio se reentra devuelve un prompt simple en vez de recursar. Hay que reinstalar los dos hooks y abrir una terminal nueva.
- [ ] **Probar a mano con oh-my-posh:** Herramientas → Instalar señales, terminal nueva, `lsd` (no existe) → tormenta roja y el tema sigue marcando el error; `dir` → pulso cian; `Get-Content $env:LOCALAPPDATA\DanaProcessing\shell-signal.txt`
- [ ] Señales para bash/zsh (WSL, Git Bash) y cmd: mismo formato, otro snippet
- [ ] Mapear la señal a la pestaña exacta de Windows Terminal (hoy va a la ventana al frente)
- [ ] `MouseDragged()` real (hoy `IsMousePressed` solo se conoce con el hook de ratón)

### Rendimiento y eventos (fase 1, hecha)
- [x] **El host duerme hasta que pasa algo**: `MsgWaitForMultipleObjectsEx` sobre un evento + la cola de mensajes, en vez de `Thread.Sleep`. Un wake (tecla, clic, señal, cambio del objetivo) corre un frame al instante, también en eco.
- [x] **WinEvents del objetivo** (`SetWinEventHook`, fuera de proceso, filtrado al proceso de la terminal): un movimiento mueve el overlay en el acto, entre frames (`LayeredWindow.MoveTo`), así ya no va un frame por detrás al arrastrar; minimizar/ocultar/cerrar/cloak y el cambio de ventana al frente despiertan el loop.
- [x] **Ventanas nuevas al instante** en `EachWindow`: hook global de `EVENT_OBJECT_SHOW` filtrado por la clase/proceso del objetivo; el escaneo completo queda cada 2 s como respaldo. Un host cuya ventana se cerró pide un escaneo en el acto.
- [x] **Señales del shell en milisegundos**: `FileSystemWatcher` sobre los archivos de Dana y Abyss Tide; el poll pasa de 250 ms a 1 s y solo es respaldo. La espera de deduplicación con Abyss baja de 200 ms a 50 ms (los dos hooks escriben en el mismo prompt, con ~1 ms de diferencia). No hace falta reinstalar los hooks.
- [x] **Las teclas/clics despiertan el host** (callback de `InputHooks`).
- [x] **Métricas en la tarjeta**: fps, dibujo, presentar, latencia de señal (desde que el shell escribió), de tecla (desde que la vio el hook) y de movimiento (desde el evento de Windows).
- [x] **Sample del campo vectorial ~2,7× más rápido** (15,8 → 5,8 ms/frame a 1123×596, medido): trazos agrupados por color con `BeginShape(ShapeKind.Lines)` y `StrokeWeight(0)` (hairline). La medición mostró que el costo no eran las llamadas sino el trazo antialias de 1,6 px con extremos redondos (16 ms vs 1,4 ms el hairline para 6700 líneas).
- [x] **Arreglo: arrastrar pausaba el sketch.** Windows manda cientos de `EVENT_OBJECT_LOCATIONCHANGE` por segundo al arrastrar; el host medía la ventana y llamaba a `SetWindowPos` por cada uno dentro de `PumpMessages`, que no salía nunca mientras durara el arrastre → ni un frame. Ahora el evento solo marca "movimiento pendiente"; el loop aplica uno como mucho cada 4 ms (`FrameScheduler.ShouldFastMove`), `PumpMessages` vacía como máximo 64 mensajes por vuelta y la espera se acorta a 4 ms mientras haya un movimiento pendiente. 3 tests nuevos (incluye un arrastre simulado que sigue dibujando frames).
- [ ] **Probar a mano**: arrastrar rápido (el overlay no debe ir detrás), `psd` → reacción inmediata, teclear con el sketch en eco, abrir una terminal nueva con `EachWindow`; leer las métricas de la tarjeta.

### Un sketch por panel (hecho)
- [x] Paneles de Windows Terminal por **UI Automation** (`TerminalPanes`): cada panel es un `TermControl` (ClassName "TermControl") y su Name es el `tabTitle` del perfil (`TermControlAutomationPeer::GetNameCore` → `StartingTitle`), así que con perfil propio solo cuentan los paneles de ese perfil. Solo están en el árbol los de la pestaña activa.
- [x] El escáner de la flota mide los paneles cada ¼ s; cada host dibuja en su panel (`PaneSlot`, posición como fracción de la ventana: al redimensionar escala hasta la siguiente medida). Los sketches siguen a "su" panel por solapamiento (`PaneLayout.Match`): dividir, mover el divisor o cerrar un panel no reinicia a los demás. Un panel fuera de la vista (otra pestaña) esconde su sketch y lo libera a los 60 s.
- [x] Si UI Automation no responde, esa ventana vuelve a un sketch para toda la ventana.
- [x] **Arreglo: menú de la pestaña.** Con el menú contextual de la pestaña (o el desplegable de nueva pestaña) abierto, UI Automation dejaba de ver los paneles → todos los sketches se escondían y el perfil translúcido dejaba ver el escritorio. Ahora el escáner congela la disposición de esa ventana (`PaneHold`) mientras una ventana emergente suya está al frente (propiedad `GA_ROOTOWNER` o mismo proceso sin ser otra ventana de la terminal) y cuando los paneles "desaparecen" sin que cambie el título (= misma pestaña). Un cambio de pestaña real cambia el título o muestra otros paneles.
- [x] **Arreglo: señales del shell por panel.** Antes iban a todos los sketches de la ventana al frente (los paneles comparten ventana). Ahora el escáner lee qué panel tiene el foco de teclado (UIA `CurrentHasKeyboardFocus`, slot 26 → `PaneSlot.FocusedAt`) y `ShellSignalHub.Route` da la señal solo a ese panel. Además aprende de qué panel es cada shell (el PID que escribe el hook): un comando largo que termina cuando ya estás en otro panel llega igual al suyo.
- [x] **Arreglo: colocación al redimensionar.** El panel se guardaba como fracción de toda la ventana, así que la barra de pestañas "crecía" con ella y el sketch se desfasaba hasta la siguiente medida (¼ s). Ahora es fracción del área de contenido (unión de los paneles) más los márgenes en píxeles hasta los bordes, como reparte Windows Terminal.
- [x] **Arreglo: overlays de paneles peleando por el orden Z.** Cada overlay (detrás) se ponía "justo debajo" de la terminal en cada frame; con varios paneles se intercambiaban sin parar (un `SetWindowPos` por frame y overlay). Ahora cuenta como "justo debajo" si entre él y la terminal solo hay otros overlays de sketch o ventanas ocultas (`ZOrder.Reaches`).
- [x] **Arreglo: arrastre en modo panel.** El escáner comparaba paneles en coordenadas de pantalla: un arrastre rápido (> ancho de un panel en ¼ s) los daba por nuevos → sketch nuevo y el viejo escondido (parpadeo y estado perdido). Ahora se comparan relativos a la ventana (`PaneLayout.Relative`), y si la ventana se movió mientras UI Automation respondía, ese escaneo no toca los paneles (sus medidas no cuadrarían con la ventana).
- [x] **Traza de diagnóstico** (`AttachTrace`): si existe `%LOCALAPPDATA%\DanaProcessing\trace.on`, escribe en `%LOCALAPPDATA%\DanaProcessing\logs\attached-trace.log` (≤ 2 MB) los cambios de estado de cada host, overlay mostrado/oculto, correcciones de orden Z y lo que ve cada escaneo de paneles (solo cuando cambia).
- [ ] Parpadeo de la barra de pestañas al pasar el ratón (GPU + perfil + paneles): reproducir con la traza activa y leerla
- [ ] Probar a mano (puntos 22 y 23)
- [ ] "El movimiento no funciona bien" en modo panel: a la espera de que el usuario diga qué exactamente (¿arrastre, redimensionar, la reacción del sketch?)
- [ ] El texto de la tarjeta sigue diciendo "ventanas" aunque cuente paneles

### Fase 2: GPU (pendiente, decidir con las métricas)
- [x] **Presentador GPU** (`DCompWindow`), casilla "Usar la GPU (DirectComposition)" al exportar (marcador `sketch.gpu`, `AttachedRunOverrides.UseGpu`): Direct3D 11 + swap chain *flip* con alfa premultiplicado mostrada con DirectComposition, como AtlasEngine de Terminal. Skia sigue dibujando en CPU; cada frame se sube al back buffer (`UpdateSubresource`) y se presenta con sync 1 (al refresco). COM por vtable, sin paquetes. Ventana sin redirection bitmap; encima del objetivo además layered+transparent (clics atraviesan), detrás no hace falta. Si no arranca o falla al presentar, el host vuelve solo a `LayeredWindow` (la tarjeta dice "CPU (sin GPU: …)"); insignia GPU en la tarjeta. Tests: en Windows crea, presenta, redimensiona y mueve de verdad (`GpuPresenterTests`); `Win32ImportsTests` revisa también d3d11/dxgi/dcomp.
- [ ] **Probar a mano** el presentador GPU (punto 21)
- [ ] Opción GPU también al correr desde el IDE (hoy solo en la exportación)
- [ ] Skia sobre Direct3D (sin la subida CPU→GPU) si la subida llega a pesar
- [ ] DirectComposition + swap chain con alfa premultiplicado, Skia sobre GPU (Direct3D/ANGLE): sin copia CPU→pantalla, sin `UpdateLayeredWindow` del bitmap completo cada frame
- [ ] Mientras tanto: `AttachResolution(0.5f)` opcional para dibujar a media resolución en 4K

### Host y rendimiento
- [x] Una instancia por ventana (`AttachMode.EachWindow`, máx. 8)
- [ ] Cambiar de objetivo cuando otra ventana que coincide pasa al frente (modo `Front`)
- [x] Modo eco: 15 fps cuando la ventana no está al frente, quieta y sin actividad 2 s, o con batería < 25 %; `DeltaTime` para que el movimiento no cambie de velocidad
- [ ] Compartir un solo hook de teclado/ratón entre todas las ventanas (hoy uno por host)
- [ ] Renderizar a escala reducida (`AttachResolution(0.5f)`) para ventanas 4K
- [x] `SetWinEventHook(EVENT_OBJECT_LOCATIONCHANGE)` para menos latencia al arrastrar
- [ ] `Renderer3D` en sketches adjuntos (probar el blit del backend GPU)
- [ ] Panel `[Tweak]` y F3: se dibujan sobre la ventana; con `ListenToTargetMouse()` los clics llegan pero el clic también va a la terminal
- [x] Pausa (`IsPaused`) de la IDE llega a la flota
- [ ] El rebobinado de la IDE no aplica a sketches adjuntos
- [ ] Bloq Mayús en el hook de teclado: se lee con `GetKeyState`, que en el hilo del hook puede quedar desfasado

### IDE y export
- [ ] Vista previa en vivo (miniatura) dentro de la tarjeta de estado
- [ ] Botón "Elegir ventana…" que liste ventanas abiertas y escriba el `Attach(...)`
- [x] Exportar como app de bandeja (automático para un `AttachedSketch`): icono, estado, pausar/reanudar, instalar señales, salir
- [x] Opción "iniciar con Windows" para la app de bandeja: `install.ps1` (acceso directo en Inicio; `-NoAutostart` para no hacerlo)
- [x] Un solo icono de bandeja aunque se abra dos veces el mismo export: mutex por ruta del exe (`ExportedSketchRunner.TryClaimSingleInstance`)
- [x] **Exportar como instalable (como Abyss Tide)**: casillas "Un sketch por cada terminal" (marcador `sketch.eachwindow` → `AttachMode.EachWindow` forzado en la flota) e "Instalador de PowerShell" (`install.ps1` + `LEEME.txt`, `PowerShellInstaller`). El script copia a `%LOCALAPPDATA%\DanaProcessing\Sketches\<id>`, desbloquea los archivos, instala el hook con `exe --install-shell-signals`, crea el acceso directo de Inicio y lo arranca; `-NoAutostart`, `-NoShellHook`, `-Uninstall`, `-Uninstall -RemoveShellHook`
- [x] **Perfil propio de Windows Terminal (como Abyss Tide)**: casilla "En su propio perfil de Windows Terminal" (requiere el instalador). La exportación lleva `terminal-profile.json` (esquema "Dana · <nombre>" + perfil "PowerShell · <nombre>", opacidad 25 %, GUID estable por sketch) y `sketch.profile` (color de fondo `#RRGGBBAA`). `install.ps1` copia el fragmento a `%LOCALAPPDATA%\Microsoft\Windows Terminal\Fragments\DanaSketch-<id>` (lo quita al desinstalar); la app fuerza `AttachLayer.Behind` (`AttachedSketch.ForceLayer`) y el host pinta el fondo bajo el sketch (`Backdrop`, `DstOver`). Resultado: el sketch solo se ve en ese perfil translúcido. `AttachedRunOverrides` agrupa lo que fuerza una exportación (modo, capa, fondo).
- [ ] **Probar a mano** el instalador (ver puntos 17 y 18)
- [ ] Un `.zip` listo para compartir con la carpeta exportada
- [ ] Instalador para pwsh en Linux/macOS cuando haya host fuera de Windows
- [x] Página en `reference/` con la API (`attached.html`)

### Plataformas
- [ ] Interfaz `IWindowTracker` para separar lo Win32
- [ ] macOS (`NSWindow` + `ignoresMouseEvents` + Accessibility API)
- [ ] Linux X11 (Wayland no permite leer la posición de otras ventanas)

### Encontrado por el camino (afecta a todos los sketches, no solo a los adjuntos)
- [x] Textos del host en el idioma del IDE: `UiText.Tr(es, en)` en el núcleo (lo fija `Loc.Initialize`); tarjeta, insignias, línea de velocidad, menú de la bandeja, estados y errores del host, overlay de error del canvas, `install.ps1`/README. Una app exportada usa el idioma guardado en esa máquina o, si no hay, el del sistema.
- [ ] Quedan en español mensajes del núcleo fuera de los sketches adjuntos (audio/live loops, micrófono, `LoadPixels`/`Save` sin superficie, `PrintExport`, `TweakPanel`, `Sketch.Web`): pasarlos a `UiText.Tr`.
- [ ] `LinearGradientFill`/`RadialGradientFill` heredan el alfa del último `Fill()` (Skia multiplica el shader por el color del paint): tras `Fill(…, 10)` el degradado sale casi invisible. El sample del blob CSS hace `Fill(255)` antes; decidir si el degradado debe poner el paint en blanco opaco.
- [ ] Los samples con `// nuget:` no entran en la teoría genérica de `AttachedSampleTests` (necesitan restaurar el paquete); el de audio tiene un test propio que solo corre con acceso a nuget.org.
- [x] `AvaloniaSketchCanvas` nunca llamaba a `Settings()`. Ahora todo host usa `Sketch.RunStartup()` (Settings → Setup; si Settings falla, Setup no corre).
- [x] El canvas normal nunca llamaba a `KeyTyped()` → ahora `Sketch.RaiseKeyDown()` (KeyPressed + KeyTyped). Afectaba a samples que ya usaban `KeyTyped()`.
- [x] El canvas nunca ponía `KeyCode` (las flechas no llegaban, el ejemplo de `input.html` con `KeyCode == LEFT` no funcionaba) y no respetaba Shift ni el layout. Nuevo `CanvasKeyMapping` (usa `KeySymbol` de Avalonia), mismas reglas que el host adjunto.
- [ ] Otros que llaman a `Setup()` directamente (variaciones, export de impresión, reels, `Offscreen` de tests) todavía no llaman a `Settings()`: pasarlos a `RunStartup()`.
- [ ] **Probar a mano** en la IDE: flechas, Shift+letra, ñ/acentos, Backspace/Enter en un sketch normal; un sketch con `Settings()` que llama `Size()`.

## Registro

- **25-oct-2026** — MVP: host, API, IDE, sample de lluvia, 6 tests. Probado en Windows.
- **25-oct-2026** — Eventos: movimiento, foco, mostrar/ocultar, teclado y ratón opcionales, `MouseMoved`; 2 samples; página de referencia; 37 tests nuevos (43 en total del módulo).
- **25-oct-2026** — Arreglos del canvas normal encontrados por el camino: `Settings()`, `KeyTyped()`, `KeyCode`/Shift/layout; docs de `input.html` y `structure.html` al día; 21 tests nuevos (64 en total).
- **25-oct-2026** — Señales del shell, insignias en la tarjeta, un sketch por ventana, modo eco y `DeltaTime`, export a bandeja, pausa; sample "Campo vectorial en la terminal"; 2 ejemplos ejecutables en la referencia; 43 tests nuevos (107 en total del módulo). Verificado compilando toda la solución y corriendo la suite completa fuera de Windows.
- **25-oct-2026** — Arreglo: señales del shell con oh-my-posh (orden de perfiles, re-envoltura en OnIdle, `$?` para el tema), ruta real de `$PROFILE`, menú de instalación en la IDE; 5 tests nuevos.
- **25-oct-2026** — Hook de PowerShell alineado con el de Abyss Tide (re-envoltura en cada OnIdle, cadena de envoltorios sin bucles, también en Abyss), lectura de la señal de Abyss Tide con deduplicación, "Probar: comando fallido"; 10 tests nuevos.
- **25-oct-2026** — Arreglo del cuelgue tras un error (ciclo en la cadena de prompts) en Dana y en Abyss Tide: closures + guarda de reentrada; 3 tests nuevos.
- **25-oct-2026** — Rendimiento fase 1: host dirigido por eventos (WinEvents, file watcher, wakes de hooks), overlay que sigue el arrastre sin retraso, métricas en la tarjeta, campo vectorial 2,7× más rápido; 21 tests nuevos.
- **25-oct-2026** — Arreglo: arrastrar la terminal pausaba el sketch (inundación de WinEvents de movimiento) → movimientos agrupados cada ≤ 4 ms y bombeo de mensajes acotado. 3 samples nuevos inspirados en OpenProcessing (Tela colgada, Blobs, Funciones ondulantes; ideas citadas, código propio porque los originales lo tienen oculto); 3 tests nuevos + los samples entran en la teoría de `AttachedSampleTests`.
- **25-oct-2026** — Arreglo: `GetTickCount` estaba declarado en `user32.dll` (es de `kernel32.dll`) y el host fallaba en el primer arrastre ("Unable to find an entry point"). Test nuevo `Win32ImportsTests`: en Windows comprueba que cada P/Invoke existe en su DLL.
- **25-oct-2026** — Samples retocados tras probarlos: **Blobs** con relleno exacto (marching squares → una malla sombreada por el campo, sin escalones), flotan como lámpara de lava en vez de vivir en el piso, inercia ~4× más fuerte, un error los separa en gotas que se vuelven a unir; **Tela** cubre toda la terminal (relleno sin antialias en 6 tonos, rigidez a compresión, inercia limitada para que un tirón no la vuele, ráfaga con un comando correcto); **Funciones ondulantes** con cintas, cuerdas con ecuación de onda, resortes por capa, teclas y cursor; colores de error que pasan por magenta (no por verde). 6 tests nuevos en `AttachedSampleTests`.
- **25-oct-2026** — Blobs: el núcleo tenía una meseta plana con borde dentado (el sombreado se cortaba en seco en `depth = 1`); ahora la profundidad sube de forma suave y sin tope (`over / (over + 1.6)` + smoothstep) y cada blob lleva un brillo suave arriba a la izquierda (14 elipses translúcidas apiladas).
- **25-oct-2026** — 2 samples nuevos: **Blob CSS en la terminal** (border-radius animado por keyframes, inspirado en el video de Code With Nazia) y **Audio reactivo en la terminal** (port del sample de NAudio al borde inferior de la terminal, con la captura detenida en `OnStop()`). 2 tests nuevos; los samples con `// nuget:` salen de la teoría genérica.
- **25-oct-2026** — Exportar como instalable tipo Abyss Tide: "Un sketch por cada terminal" (`sketch.eachwindow`, fuerza `EachWindow`) e "Instalador de PowerShell" (`install.ps1` + `LEEME.txt`: copia a `%LOCALAPPDATA%`, hook del prompt con `--install-shell-signals`, arranque con Windows, `-Uninstall`); una sola instancia por exe. 20 tests nuevos (`PowerShellInstallerTests`; el que comprueba que el script parsea corre donde haya `powershell.exe`/`pwsh`).
- **25-oct-2026** — Investigando una `ArgumentOutOfRangeException` ("index") que a veces aparece en `StartWithClassicDesktopLifetime` con el sample de NAudio: sin la pila no se pudo ubicar. Añadido `ErrorLog` (`%LOCALAPPDATA%\DanaProcessingIde\logs\errores.log`, siempre activo) para excepciones del hilo de UI y del dominio; los diagnósticos en vivo (handler `async void`) ya no pueden tumbar el IDE; el sample "Audio reactivo" detiene la captura en `OnStop()`. 3 tests nuevos.
- **25-oct-2026** — Arreglo: el sample "MidiBounce" (NAudio.Midi) daba CS0104 porque Dana ya tiene su propio `MidiMessage` (para `OnMidi`); ahora escribe `NAudio.Midi.MidiMessage`. Test `NuGetSampleNameClashTests` para que ningún sample con un paquete vuelva a chocar con un nombre de Dana.

- **25-oct-2026** — Localización: la tarjeta del sketch adjunto, sus insignias, la línea de velocidad, el menú de la bandeja, los estados/errores del host y el overlay de error siguen el idioma del IDE (`UiText`); el instalador sale en inglés si el IDE está en inglés (`install.ps1` + `README.txt`); las apps exportadas usan el idioma del sistema si la máquina no tiene el IDE. Los símbolos ▶ ⏸ ■ salían como cuadros (no están en la fuente): ahora un punto de color dibujado. 11 tests nuevos.
- **26-oct-2026** — Exportar en su propio perfil de Windows Terminal, como Abyss Tide: fragmento con perfil translúcido + esquema, el sketch detrás de la terminal sobre un fondo oscuro, solo visible en ese perfil. 12 tests nuevos (`TerminalProfileTests`).
- **26-oct-2026** — Revisado el código de Windows Terminal. Frames al ritmo del refresco (DWM) en vez de un temporizador que se desfasa; contador de frames tardíos en la tarjeta ("N tardíos (peor X ms)"); con perfil propio el sketch solo dibuja mientras su pestaña está al frente (título fijo de la pestaña + `EVENT_OBJECT_NAMECHANGE`). Estados del host que quedaban sin traducir. 21 tests nuevos.
- **26-oct-2026** — Presentador GPU (DirectComposition + swap chain flip, como Windows Terminal) con casilla al exportar y vuelta automática a CPU; insignia GPU. 6 tests nuevos (el de Windows presenta de verdad).
- **26-oct-2026** — Arreglo: el perfil propio no aparecía. Windows Terminal ya había visto su GUID (está en `state.json` → `generatedProfiles`) pero no estaba en `settings.json`, y en ese caso lo oculta por considerarlo borrado por el usuario (`SettingsLoader::DisableDeletedProfiles`). `install.ps1` ahora lo anota en `settings.json` (entrada de una línea con `source: DanaSketch-<id>`, como las que escribe la propia terminal; copia en `settings.json.bak-dana`); al desinstalar la entrada queda huérfana y la terminal la oculta sola. Aplicado a mano en la instalación actual de MiSketch.
- **26-oct-2026** — Arreglo: la caída de FPS sin foco era el modo eco, que bajaba a 15 fps en cuanto la terminal no estaba al frente aunque se viera. Ahora solo baja cuando otra ventana la tapa entera (`WindowTracker.IsCovered`, comprobado dos veces por segundo) o con batería baja; la regla anterior queda opcional con `EcoMode(whenUnfocused: true)`. 7 tests nuevos.
- **26-oct-2026** — Un sketch por panel de Windows Terminal (como los shaders de la terminal, que van por panel): paneles por UI Automation, un sketch por panel del perfil propio, que sigue a su panel al dividir/mover/cerrar y se esconde con la pestaña. "Un sketch por cada terminal" al exportar ahora incluye los paneles. 11 tests nuevos (uno en Windows usa UI Automation de verdad).
- **26-oct-2026** — Arreglos del modo panel tras probarlo: el menú de la pestaña ya no esconde los sketches (`PaneHold`), las señales del shell van solo al panel con el foco y luego al panel de su shell (PID aprendido), y el panel se coloca relativo al área de contenido (la barra de pestañas no escala al redimensionar). 6 tests nuevos/actualizados (`TerminalPanesTests`, `AttachedRuntimeTests`); suite completa 559 ok (los 4 que fallan en el contenedor son de entorno: benchmarks, `.github` y memoria).
- **26-oct-2026** — Parpadeo al pasar el ratón por las pestañas y al arrastrar: los overlays de paneles ya no pelean por el orden Z, el arrastre rápido no crea sketches nuevos (paneles comparados relativos a la ventana, escaneo en pausa mientras se mueve) y traza de diagnóstico opcional (`trace.on`). 4 tests nuevos (`ZOrder`, arrastre rápido, `AttachTraceTests`).
