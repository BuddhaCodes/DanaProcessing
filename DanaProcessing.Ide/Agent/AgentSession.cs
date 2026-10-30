using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DanaProcessing.Ide.Agent
{
    /// <summary>
    /// Drives one Agent mode conversation: send a user message, call the
    /// chosen provider, execute any tool calls it asks for against the real
    /// IDE, feed results back, and repeat until it produces a final text
    /// answer -- capped at MaxToolIterations so a confused model can't spin
    /// forever (same "never let a loop run unbounded" discipline as this
    /// session's own ML.NET self-play sample, which caps generations).
    ///
    /// Cancel-safety uses a generation counter, not a lock -- the exact idiom
    /// NuGetPackagesWindow.cs's search box already uses (increment at the top
    /// of the async method, re-check after every await, silently no-op if a
    /// newer message/Stop superseded this one) rather than inventing a new
    /// pattern for what's structurally the same problem.
    /// </summary>
    public sealed class AgentSession
    {
        private const int MaxToolIterations = 10;

        private const string SystemPromptTemplate = """
            Sos un asistente de programación para DanaProcessing, un motor de creative coding en C# al estilo Processing/p5.js.
            Un sketch es una clase que hereda de Sketch, con Setup() (corre una vez) y Draw() (corre en cada frame).
            Tenés cinco herramientas: read_sketch (leer el código actual), edit_sketch (reemplazar TODO el código), run_sketch (compilar y correr, devuelve errores de compilación o de ejecución), look_at_canvas (ver una captura del sketch corriendo) y search_docs (buscar en la referencia de la API).
            Para cualquier pedido visual, después de run_sketch mirá el resultado con look_at_canvas y corregí si no se ve como se pidió: compilar sin errores no significa que se vea bien.
            Cuando te pidan cambiar o arreglar un sketch: leé el código actual si no lo tenés, hacé el cambio con edit_sketch, y confirmá que compila y corre con run_sketch antes de darlo por terminado. Si run_sketch devuelve un error, corregilo e intentá de nuevo en vez de asumir que ya funciona.
            """;

        private readonly List<AgentMessage> _messages = new();
        private readonly MainWindow _owner;
        private int _generation;

        public string ProviderName { get; }
        public event Action? MessagesChanged;
        public event Action<string?>? StatusChanged; // null = no tool currently running

        public IReadOnlyList<AgentMessage> Messages => _messages;

        public AgentSession(MainWindow owner, AgentSettings settings)
        {
            _owner = owner;
            ProviderName = settings.Provider;
            _apiKey = settings.ApiKey ?? "";
            _model = settings.Model;
            _provider = AgentProviders.Resolve(settings.Provider);
            _toolContext = new AgentToolContext
            {
                ConfirmPackages = packages => Editor.ConfirmDialog.ShowAsync(
                    owner,
                    Localization.Loc.Tr("El agente quiere usar paquetes NuGet", "The agent wants to use NuGet packages"),
                    Localization.Loc.Tr(
                        "El agente agregó al sketch estos paquetes, que se van a descargar de nuget.org y ejecutar en tu computadora:\n\n",
                        "The agent added these packages to the sketch; they will be downloaded from nuget.org and run on your computer:\n\n")
                        + string.Join("\n", packages.Select(d => "• " + (d.Version is null ? d.Id : $"{d.Id} {d.Version}")))
                        + Localization.Loc.Tr("\n\n¿Permitirlo?", "\n\nAllow it?"),
                    Localization.Loc.Tr("Permitir", "Allow"),
                    Localization.Loc.Tr("No permitir", "Don't allow")),
            };
        }

        private readonly AgentToolContext _toolContext;

        private readonly ILlmProvider _provider;
        private readonly string _apiKey;
        private readonly string? _model;

        /// <summary>Marks any in-flight turn as stale, so its eventual
        /// response is discarded instead of racing to append itself after a
        /// newer message. Call before starting a new SendUserMessageAsync,
        /// or to implement a "Stop" button.</summary>
        public void CancelInFlightTurn() => Interlocked.Increment(ref _generation);

        public async Task SendUserMessageAsync(string userText, CancellationToken ct)
        {
            // A turn interrupted mid-tool (a new message, Stop) leaves an
            // assistant tool call with no result; both APIs reject every later
            // request until each call has one, so close them off first.
            RepairDanglingToolCalls(_messages);

            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                _messages.Add(new AgentMessage(AgentRole.User, userText, null, null));
                _messages.Add(new AgentMessage(AgentRole.Assistant, "No hay una API key configurada -- andá a Settings y cargá la tuya para el proveedor elegido.", null, null));
                MessagesChanged?.Invoke();
                return;
            }

            var myGeneration = Interlocked.Increment(ref _generation);
            _messages.Add(new AgentMessage(AgentRole.User, userText, null, null));
            MessagesChanged?.Invoke();

            try
            {
                for (int i = 0; i < MaxToolIterations; i++)
                {
                    AgentTurnResult result;
                    try
                    {
                        result = await _provider.SendAsync(new AgentConversation(SystemPromptTemplate, _messages), AgentTools.All, _apiKey, _model, ct);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        if (myGeneration != _generation) return;
                        _messages.Add(new AgentMessage(AgentRole.Assistant, $"Error hablando con {_provider.Name}: {ex.Message}", null, null));
                        MessagesChanged?.Invoke();
                        return;
                    }

                    if (myGeneration != _generation)
                        return; // a newer message (or Stop) superseded this in-flight turn

                    if (result.ToolCalls.Count == 0)
                    {
                        _messages.Add(new AgentMessage(AgentRole.Assistant, result.FinalText ?? "", null, null));
                        MessagesChanged?.Invoke();
                        return;
                    }

                    _messages.Add(new AgentMessage(AgentRole.Assistant, result.FinalText, result.ToolCalls, null));
                    MessagesChanged?.Invoke();

                    var toolResults = new List<AgentToolResult>();
                    foreach (var call in result.ToolCalls)
                    {
                        StatusChanged?.Invoke(StatusTextFor(call.Name));
                        toolResults.Add(await AgentTools.ExecuteAsync(call, _owner, _toolContext, ct));
                        if (myGeneration != _generation)
                            return;
                    }

                    _messages.Add(new AgentMessage(AgentRole.ToolResult, null, null, toolResults));
                    MessagesChanged?.Invoke();
                }

                _messages.Add(new AgentMessage(AgentRole.Assistant,
                    "No pude terminar en el número de pasos permitido -- probá pedírmelo de nuevo, de forma más específica.", null, null));
                MessagesChanged?.Invoke();
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // Superseded by a newer message or Stop: nothing to report, and
                // the caller fire-and-forgets this task, so don't let it fault.
            }
            finally
            {
                StatusChanged?.Invoke(null);
            }
        }

        /// <summary>
        /// Gives every assistant tool call that has no matching result a
        /// synthetic "cancelled" one, inserted right after it -- the order both
        /// providers require (tool_use immediately answered by tool_result).
        /// </summary>
        internal static void RepairDanglingToolCalls(List<AgentMessage> messages)
        {
            for (int i = 0; i < messages.Count; i++)
            {
                var message = messages[i];
                if (message.Role != AgentRole.Assistant || message.ToolCalls is not { Count: > 0 } calls)
                    continue;

                var next = i + 1 < messages.Count ? messages[i + 1] : null;
                var existing = next?.Role == AgentRole.ToolResult ? next.ToolResults ?? Array.Empty<AgentToolResult>() : null;
                var answered = new HashSet<string>(existing?.Select(r => r.ToolCallId) ?? Enumerable.Empty<string>());
                var missing = calls.Where(c => !answered.Contains(c.Id))
                                   .Select(c => new AgentToolResult(c.Id, "Cancelado: el usuario interrumpió este paso antes de que terminara."))
                                   .ToList();
                if (missing.Count == 0)
                    continue;

                if (existing != null)
                    messages[i + 1] = next! with { ToolResults = existing.Concat(missing).ToList() };
                else
                    messages.Insert(i + 1, new AgentMessage(AgentRole.ToolResult, null, null, missing));
            }
        }

        private static string StatusTextFor(string toolName) => toolName switch
        {
            "read_sketch" => "Leyendo el sketch...",
            "edit_sketch" => "Editando el sketch...",
            "run_sketch" => "Ejecutando el sketch...",
            "look_at_canvas" => "Mirando el canvas...",
            "search_docs" => "Buscando en la documentación...",
            _ => $"Usando {toolName}...",
        };
    }
}
