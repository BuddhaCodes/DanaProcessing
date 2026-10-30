using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DanaProcessing.Ide.Agent
{
    /// <summary>
    /// The five tools Agent mode gives the model -- read/edit/run the active
    /// sketch, look at what it draws, and search the API reference. Each ExecuteAsync call is
    /// wrapped so a tool NEVER throws out to AgentSession: a failure (bad
    /// arguments, no active tab, a compile error) becomes the tool's own
    /// RESULT text instead, exactly like a real tool call that "failed" in a
    /// way the model can read and react to (e.g. try a different edit) rather
    /// than a crash that ends the whole conversation.
    /// </summary>
    public static class AgentTools
    {
        public static readonly IReadOnlyList<AgentTool> All = new[]
        {
            new AgentTool(
                "read_sketch",
                "Devuelve el código fuente C# completo del sketch actualmente abierto en el editor.",
                """{"type":"object","properties":{},"required":[]}"""),

            new AgentTool(
                "edit_sketch",
                "Reemplaza TODO el código fuente del sketch actualmente abierto. Siempre mandá el archivo completo, no solo la parte que cambió.",
                """{"type":"object","properties":{"source":{"type":"string","description":"El código C# completo y nuevo del sketch."}},"required":["source"]}"""),

            new AgentTool(
                "run_sketch",
                "Compila y corre el sketch actualmente abierto, exactamente como si se apretara el botón Run. Devuelve errores de compilación si falla al compilar, una excepción si Setup()/Draw() falla en tiempo de ejecución después de compilar bien, o una confirmación de que corrió correctamente.",
                """{"type":"object","properties":{},"required":[]}"""),

            new AgentTool(
                "look_at_canvas",
                "Saca una captura del frame actual del sketch que está corriendo y te la devuelve como imagen, para que veas cómo se ve de verdad: colores, composición, si aparece algo o no. Corré el sketch con run_sketch antes. Usala para verificar cualquier pedido visual ('que parezca un atardecer', 'no se ve nada') en vez de suponer el resultado. wait_ms (opcional, 0-5000) espera antes de capturar, para que avance una animación.",
                """{"type":"object","properties":{"wait_ms":{"type":"integer","description":"Milisegundos a esperar antes de capturar (0-5000)."}},"required":[]}"""),

            new AgentTool(
                "search_docs",
                "Busca en la referencia de la API de DanaProcessing (métodos de Sketch: dibujo 2D/3D, input, matemática, etc.) entradas que coincidan con una palabra clave, y devuelve su descripción.",
                """{"type":"object","properties":{"query":{"type":"string","description":"Un nombre de método o palabra clave para buscar, por ejemplo 'PVector' o 'lights'."}},"required":["query"]}"""),
        };

        public static async Task<AgentToolResult> ExecuteAsync(AgentToolCall call, MainWindow owner, AgentToolContext context, CancellationToken ct)
        {
            try
            {
                if (call.Name == "look_at_canvas")
                    return await LookAtCanvasAsync(call, owner, ct);

                string result = call.Name switch
                {
                    "read_sketch" => ReadSketch(owner),
                    "edit_sketch" => EditSketch(owner, context, call.ArgumentsJson),
                    "run_sketch" => await RunSketchAsync(owner, context, ct),
                    "search_docs" => SearchDocs(call.ArgumentsJson),
                    _ => $"Herramienta desconocida: '{call.Name}'.",
                };
                return new AgentToolResult(call.Id, result);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return new AgentToolResult(call.Id, $"'{call.Name}' fue cancelada.");
            }
            catch (Exception ex)
            {
                // A tool "failing" this way is still a valid result, not a
                // crash -- the model reads this text and can react to it
                // (fix its own arguments, try something else) instead of the
                // whole conversation dying on a malformed tool call.
                return new AgentToolResult(call.Id, $"Error ejecutando '{call.Name}': {ex.Message}");
            }
        }

        private static string ReadSketch(MainWindow owner)
        {
            var source = owner.EditorView.ActiveSourceText;
            return string.IsNullOrEmpty(source)
                ? "No hay ningún sketch abierto en el editor."
                : source;
        }

        private static string EditSketch(MainWindow owner, AgentToolContext context, string argumentsJson)
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(argumentsJson) ? "{}" : argumentsJson);
            if (!doc.RootElement.TryGetProperty("source", out var sourceEl) || sourceEl.ValueKind != JsonValueKind.String)
                return "Falta el argumento 'source' (debe ser un string con el código completo del sketch).";

            if (owner.EditorView.ActiveTab is null)
                return "No hay ningún sketch abierto para editar.";

            if (owner.EditorView.ActiveTab.Blocks is not null)
                return "El sketch abierto está hecho con bloques: su código sale de los bloques y no se puede reemplazar como texto. "
                     + "Pedile al usuario que use \"Pasar a código\" si quiere editarlo como C#.";

            var newSource = sourceEl.GetString() ?? "";
            context.NoteSourceBeforeAgentEdit(owner.EditorView.ActiveSourceText);
            owner.EditorView.ReplaceActiveSourceText(newSource);
            context.NoteAgentWrote(owner.EditorView.ActiveSourceText ?? newSource);
            return "Listo -- el sketch fue reemplazado con el nuevo código.";
        }

        private static async Task<string> RunSketchAsync(MainWindow owner, AgentToolContext context, CancellationToken ct)
        {
            var source = owner.EditorView.ActiveSourceText;
            if (string.IsNullOrWhiteSpace(source))
                return "No hay ningún sketch abierto para ejecutar.";

            // Packages the agent added get the user's OK before anything is
            // downloaded or loaded -- see AgentToolContext for why.
            var unapproved = context.Unapproved(source);
            if (unapproved.Count > 0)
            {
                bool allowed = context.ConfirmPackages != null && await context.ConfirmPackages(unapproved);
                if (!allowed)
                {
                    var names = string.Join(", ", unapproved.Select(d => d.Version is null ? d.Id : $"{d.Id} {d.Version}"));
                    return $"El usuario NO autorizó descargar y ejecutar estos paquetes NuGet: {names}. El sketch no se ejecutó. "
                         + "Sacá esas directivas `// nuget:` (o preguntale al usuario) antes de volver a intentar.";
                }
                context.Approve(unapproved);
            }

            // Another Run (the user's, or a previous one still restoring
            // packages) may be in progress: wait for it rather than reporting
            // on a run that never happened.
            RunOutcome outcome;
            var busyDeadline = DateTime.UtcNow.AddSeconds(60);
            while ((outcome = await owner.RunCurrentSketchAsync()) == RunOutcome.Busy)
            {
                if (DateTime.UtcNow > busyDeadline)
                    return "No se pudo ejecutar: hay otra ejecución en curso que no terminó en 60 segundos.";
                await Task.Delay(100, ct);
            }

            switch (outcome)
            {
                case RunOutcome.RestoreFailed:
                    return "No se pudieron restaurar los paquetes NuGet, así que el sketch no se ejecutó:\n" + string.Join("\n", owner.LastCompileErrors);
                case RunOutcome.CompileFailed:
                    return "Error de compilación:\n" + string.Join("\n", owner.LastCompileErrors);
                case RunOutcome.Failed:
                    return "El sketch no se pudo iniciar:\n" + string.Join("\n", owner.LastCompileErrors);
                case RunOutcome.NothingToRun:
                    return "No hay ningún sketch abierto para ejecutar.";
            }

            // Setup()/Draw() only corren en el próximo frame compuesto,
            // asíncronamente en el hilo de render -- si hubo una excepción en
            // tiempo de ejecución, HasCrashed recién se pone en true un
            // instante después de que Run() ya devolvió. Esperar un rato
            // corto y acotado en vez de asumir éxito inmediatamente.
            var deadline = DateTime.UtcNow.AddMilliseconds(800);
            while (DateTime.UtcNow < deadline)
            {
                if (owner.Canvas.HasCrashed)
                {
                    return $"El sketch compiló bien pero tiró una excepción en tiempo de ejecución (en {owner.Canvas.LastCrashContext}()): "
                         + $"{owner.Canvas.LastCrashException?.GetType().Name}: {owner.Canvas.LastCrashException?.Message}";
                }
                await Task.Delay(50, ct);
            }

            return "El sketch compiló y corrió sin errores.";
        }

        private static async Task<AgentToolResult> LookAtCanvasAsync(AgentToolCall call, MainWindow owner, CancellationToken ct)
        {
            int waitMs = 0;
            using (var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(call.ArgumentsJson) ? "{}" : call.ArgumentsJson))
            {
                if (doc.RootElement.TryGetProperty("wait_ms", out var waitEl) && waitEl.ValueKind == JsonValueKind.Number && waitEl.TryGetInt32(out int w))
                    waitMs = Math.Clamp(w, 0, 5000);
            }
            if (waitMs > 0)
                await Task.Delay(waitMs, ct);

            var capture = owner.Canvas.CaptureFrameAsync();
            var finished = await Task.WhenAny(capture, Task.Delay(2000, ct));
            byte[]? jpeg = finished == capture ? await capture : null;
            if (jpeg is null)
                return new AgentToolResult(call.Id, "No se pudo capturar el canvas: no se dibujó ningún frame en 2 segundos. ¿Está corriendo el sketch (run_sketch) y el canvas visible en la ventana?");

            string text = $"Captura del frame actual del sketch ({owner.Canvas.SketchWidth}x{owner.Canvas.SketchHeight}, reducida para enviarla).";
            if (owner.Canvas.HasCrashed)
                text += $" Ojo: el sketch está detenido por un error en {owner.Canvas.LastCrashContext}() ({owner.Canvas.LastCrashException?.Message}) y la captura muestra ese aviso, no el dibujo.";
            return new AgentToolResult(call.Id, text, jpeg);
        }

        private static string SearchDocs(string argumentsJson)
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(argumentsJson) ? "{}" : argumentsJson);
            if (!doc.RootElement.TryGetProperty("query", out var queryEl) || queryEl.ValueKind != JsonValueKind.String)
                return "Falta el argumento 'query' (un string con lo que querés buscar).";

            var matches = ApiDocsIndex.Search(queryEl.GetString() ?? "");
            if (matches.Count == 0)
                return "No se encontraron entradas en la documentación para esa búsqueda.";

            var lines = new List<string>();
            foreach (var entry in matches)
                lines.Add($"### {entry.Title}\n{entry.Description}");
            return string.Join("\n\n", lines);
        }
    }
}
