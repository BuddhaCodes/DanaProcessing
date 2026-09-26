using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DanaProcessing.Ide.Agent
{
    /// <summary>
    /// The four tools Agent mode gives the model for v1 -- read/edit/run the
    /// active sketch, and search the API reference. Each ExecuteAsync call is
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
                "search_docs",
                "Busca en la referencia de la API de DanaProcessing (métodos de Sketch: dibujo 2D/3D, input, matemática, etc.) entradas que coincidan con una palabra clave, y devuelve su descripción.",
                """{"type":"object","properties":{"query":{"type":"string","description":"Un nombre de método o palabra clave para buscar, por ejemplo 'PVector' o 'lights'."}},"required":["query"]}"""),
        };

        public static async Task<AgentToolResult> ExecuteAsync(AgentToolCall call, MainWindow owner, CancellationToken ct)
        {
            try
            {
                string result = call.Name switch
                {
                    "read_sketch" => ReadSketch(owner),
                    "edit_sketch" => EditSketch(owner, call.ArgumentsJson),
                    "run_sketch" => await RunSketchAsync(owner, ct),
                    "search_docs" => SearchDocs(call.ArgumentsJson),
                    _ => $"Herramienta desconocida: '{call.Name}'.",
                };
                return new AgentToolResult(call.Id, result);
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

        private static string EditSketch(MainWindow owner, string argumentsJson)
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(argumentsJson) ? "{}" : argumentsJson);
            if (!doc.RootElement.TryGetProperty("source", out var sourceEl) || sourceEl.ValueKind != JsonValueKind.String)
                return "Falta el argumento 'source' (debe ser un string con el código completo del sketch).";

            if (owner.EditorView.ActiveTab is null)
                return "No hay ningún sketch abierto para editar.";

            owner.EditorView.ReplaceActiveSourceText(sourceEl.GetString() ?? "");
            return "Listo -- el sketch fue reemplazado con el nuevo código.";
        }

        private static async Task<string> RunSketchAsync(MainWindow owner, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(owner.EditorView.ActiveSourceText))
                return "No hay ningún sketch abierto para ejecutar.";

            await owner.RunCurrentSketchAsync();

            if (owner.LastCompileErrors.Count > 0)
                return "Error de compilación:\n" + string.Join("\n", owner.LastCompileErrors);

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
