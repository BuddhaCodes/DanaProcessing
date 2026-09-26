using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace DanaProcessing.Ide.Agent
{
    /// <summary>
    /// Talks to OpenAI's Chat Completions API
    /// (https://api.openai.com/v1/chat/completions). Tools are nested
    /// {type:"function", function:{name, description, parameters}}; a tool
    /// call comes back in the assistant message's "tool_calls" array (each
    /// {id, function:{name, arguments}} -- arguments is a JSON STRING here,
    /// not an object, unlike Anthropic's "input"); replying requires a
    /// message with role:"tool", a matching "tool_call_id", and "content" as
    /// a plain string. See AnthropicProvider's own remarks for why this
    /// isn't sharing DTOs with that implementation.
    ///
    /// DefaultModel is deliberately conservative: OpenAI's exact current
    /// flagship model id couldn't be confirmed with real certainty at
    /// implementation time (marketing names and API strings don't always
    /// match), so this leaves Model blank by default rather than asserting a
    /// guess -- AgentSettings' Model field is effectively required for this
    /// provider until the user fills in whatever model id their account
    /// actually has access to.
    /// </summary>
    internal sealed class OpenAiProvider : ILlmProvider
    {
        public string Name => "OpenAI";

        private const int MaxTokens = 4096;

        public async Task<AgentTurnResult> SendAsync(AgentConversation conversation, IReadOnlyList<AgentTool> tools, string apiKey, string? model, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(model))
                throw new InvalidOperationException("Configurá un modelo de OpenAI en Settings (por ejemplo, el que uses hoy en la API de Chat Completions) -- a diferencia de Anthropic, acá no hay un valor por defecto seguro para adivinar.");

            var messages = new JsonArray { new JsonObject { ["role"] = "system", ["content"] = conversation.SystemPrompt } };
            foreach (var node in BuildMessages(conversation.Messages))
                messages.Add(node);

            var body = new JsonObject
            {
                ["model"] = model,
                ["max_tokens"] = MaxTokens,
                ["messages"] = messages,
            };

            if (tools.Count > 0)
            {
                var toolsArray = new JsonArray();
                foreach (var tool in tools)
                {
                    toolsArray.Add(new JsonObject
                    {
                        ["type"] = "function",
                        ["function"] = new JsonObject
                        {
                            ["name"] = tool.Name,
                            ["description"] = tool.Description,
                            ["parameters"] = JsonNode.Parse(tool.JsonSchema),
                        },
                    });
                }
                body["tools"] = toolsArray;
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");

            using var response = await AgentHttpClient.Shared.SendAsync(request, ct);
            var responseText = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"OpenAI API error ({(int)response.StatusCode}): {responseText}");

            return ParseResponse(responseText);
        }

        private static IEnumerable<JsonNode> BuildMessages(IReadOnlyList<AgentMessage> messages)
        {
            foreach (var message in messages)
            {
                switch (message.Role)
                {
                    case AgentRole.User:
                        yield return new JsonObject { ["role"] = "user", ["content"] = message.Text ?? "" };
                        break;

                    case AgentRole.Assistant:
                        var assistantMsg = new JsonObject { ["role"] = "assistant" };
                        if (!string.IsNullOrEmpty(message.Text))
                            assistantMsg["content"] = message.Text;
                        if (message.ToolCalls is { Count: > 0 })
                        {
                            var toolCallsArray = new JsonArray();
                            foreach (var call in message.ToolCalls)
                            {
                                toolCallsArray.Add(new JsonObject
                                {
                                    ["id"] = call.Id,
                                    ["type"] = "function",
                                    ["function"] = new JsonObject
                                    {
                                        ["name"] = call.Name,
                                        ["arguments"] = string.IsNullOrWhiteSpace(call.ArgumentsJson) ? "{}" : call.ArgumentsJson,
                                    },
                                });
                            }
                            assistantMsg["tool_calls"] = toolCallsArray;
                        }
                        yield return assistantMsg;
                        break;

                    case AgentRole.ToolResult:
                        foreach (var result in message.ToolResults ?? Array.Empty<AgentToolResult>())
                        {
                            yield return new JsonObject
                            {
                                ["role"] = "tool",
                                ["tool_call_id"] = result.ToolCallId,
                                ["content"] = result.ResultText,
                            };
                        }
                        break;
                }
            }
        }

        private static AgentTurnResult ParseResponse(string json)
        {
            using var doc = JsonDocument.Parse(json);
            var message = doc.RootElement.GetProperty("choices")[0].GetProperty("message");

            string? text = message.TryGetProperty("content", out var contentEl) && contentEl.ValueKind == JsonValueKind.String
                ? contentEl.GetString()
                : null;

            var toolCalls = new List<AgentToolCall>();
            if (message.TryGetProperty("tool_calls", out var toolCallsEl) && toolCallsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var call in toolCallsEl.EnumerateArray())
                {
                    var id = call.GetProperty("id").GetString()!;
                    var function = call.GetProperty("function");
                    var name = function.GetProperty("name").GetString()!;
                    var arguments = function.GetProperty("arguments").GetString() ?? "{}";
                    toolCalls.Add(new AgentToolCall(id, name, arguments));
                }
            }

            return new AgentTurnResult(text, toolCalls);
        }
    }
}
