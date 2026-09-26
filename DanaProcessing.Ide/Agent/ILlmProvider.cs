using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DanaProcessing.Ide.Agent
{
    /// <summary>
    /// One turn of a conversation, provider-agnostic. Anthropic's Messages API
    /// and OpenAI's Chat Completions API have genuinely different wire shapes
    /// for tool use (flat {name,input_schema} content blocks vs. nested
    /// {type:"function",function:{...}} with stringified arguments) -- these
    /// types are the shared shape every ILlmProvider translates its own
    /// provider's JSON into and out of, so AgentSession never has to know
    /// which provider is actually running.
    /// </summary>
    public enum AgentRole { User, Assistant, ToolResult }

    /// <summary>One tool call the model asked for -- Id is the provider's own
    /// call identifier (needed to match a later tool result back to it).</summary>
    public sealed record AgentToolCall(string Id, string Name, string ArgumentsJson);

    /// <summary>The result of actually running one AgentToolCall, always as
    /// plain text (an error message counts as a valid result -- see
    /// AgentTools.ExecuteAsync's own remarks on why a tool never throws out).</summary>
    public sealed record AgentToolResult(string ToolCallId, string ResultText);

    /// <summary>One message in the conversation. Exactly one of (Text,
    /// ToolCalls) is meaningful for an Assistant message; ToolResults is only
    /// meaningful for a ToolResult message; User messages only ever set Text.</summary>
    public sealed record AgentMessage(AgentRole Role, string? Text, IReadOnlyList<AgentToolCall>? ToolCalls, IReadOnlyList<AgentToolResult>? ToolResults);

    /// <summary>One tool definition offered to the model. JsonSchema is the
    /// raw JSON Schema text for the tool's input object (e.g.
    /// {"type":"object","properties":{...},"required":[...]}) -- each
    /// provider implementation slots this into its own tools array shape.</summary>
    public sealed record AgentTool(string Name, string Description, string JsonSchema);

    /// <summary>Everything needed to ask a provider for the next turn.</summary>
    public sealed record AgentConversation(string SystemPrompt, IReadOnlyList<AgentMessage> Messages);

    /// <summary>What the model produced this turn. ToolCalls empty means
    /// FinalText is the real answer to show the user; otherwise FinalText may
    /// still carry accompanying prose (some providers emit text alongside a
    /// tool call) and the caller must execute every call and send results
    /// back before the conversation can finish.</summary>
    public sealed record AgentTurnResult(string? FinalText, IReadOnlyList<AgentToolCall> ToolCalls);

    /// <summary>
    /// A pluggable AI backend for Agent mode -- see AnthropicProvider/
    /// OpenAiProvider for the two concrete implementations. Name is what
    /// SettingsWindow's provider dropdown shows and what AgentSettings.Provider
    /// stores, so it must stay stable once shipped (renaming it would silently
    /// reset anyone's saved selection back to the default).
    /// </summary>
    public interface ILlmProvider
    {
        string Name { get; }

        /// <summary>model null/blank means "use this provider's own built-in
        /// default" -- see AnthropicProvider/OpenAiProvider's own DefaultModel
        /// constants. Exposed as a plain user-editable Settings field rather
        /// than a hardcoded choice because model ids change over time for
        /// both providers and a wrong guess here would just as easily be a
        /// wrong hardcoded constant -- letting the user type whatever their
        /// account currently has access to is more robust than this app
        /// trying to stay perfectly current forever.</summary>
        Task<AgentTurnResult> SendAsync(AgentConversation conversation, IReadOnlyList<AgentTool> tools, string apiKey, string? model, CancellationToken ct);
    }
}
