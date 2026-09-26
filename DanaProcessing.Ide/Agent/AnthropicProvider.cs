using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace DanaProcessing.Ide.Agent
{
    /// <summary>
    /// Talks to Anthropic's Messages API (https://api.anthropic.com/v1/messages).
    /// Tools are a flat {name, description, input_schema} array; a tool-use
    /// response comes back as a "tool_use" content block ({id, name, input} --
    /// input already a JSON object, not a string); replying requires a "user"
    /// message containing a "tool_result" block referencing that same id.
    /// Built with JsonNode/JsonObject rather than hand-written DTOs -- the
    /// request/response shapes here and in OpenAiProvider differ enough
    /// (flat vs. nested, object vs. stringified arguments) that sharing DTOs
    /// wouldn't actually save code.
    /// </summary>
    internal sealed class AnthropicProvider : ILlmProvider
    {
        public string Name => "Anthropic";

        // A real, current model id (confirmed, not guessed) -- Model in
        // AgentSettings can override this if a newer one ships later.
        private const string DefaultModel = "claude-sonnet-5";
        private const int MaxTokens = 4096;

        public async Task<AgentTurnResult> SendAsync(AgentConversation conversation, IReadOnlyList<AgentTool> tools, string apiKey, string? model, CancellationToken ct)
        {
            var body = new JsonObject
            {
                ["model"] = string.IsNullOrWhiteSpace(model) ? DefaultModel : model,
                ["max_tokens"] = MaxTokens,
                ["system"] = conversation.SystemPrompt,
                ["messages"] = BuildMessages(conversation.Messages),
            };

            if (tools.Count > 0)
            {
                var toolsArray = new JsonArray();
                foreach (var tool in tools)
                {
                    toolsArray.Add(new JsonObject
                    {
                        ["name"] = tool.Name,
                        ["description"] = tool.Description,
                        ["input_schema"] = JsonNode.Parse(tool.JsonSchema),
                    });
                }
                body["tools"] = toolsArray;
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
            request.Headers.Add("x-api-key", apiKey);
            request.Headers.Add("anthropic-version", "2023-06-01");
            request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");

            using var response = await AgentHttpClient.Shared.SendAsync(request, ct);
            var responseText = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Anthropic API error ({(int)response.StatusCode}): {responseText}");

            return ParseResponse(responseText);
        }

        private static JsonArray BuildMessages(IReadOnlyList<AgentMessage> messages)
        {
            var array = new JsonArray();
            foreach (var message in messages)
            {
                switch (message.Role)
                {
                    case AgentRole.User:
                        array.Add(new JsonObject { ["role"] = "user", ["content"] = message.Text ?? "" });
                        break;

                    case AgentRole.Assistant:
                        var content = new JsonArray();
                        if (!string.IsNullOrEmpty(message.Text))
                            content.Add(new JsonObject { ["type"] = "text", ["text"] = message.Text });
                        foreach (var call in message.ToolCalls ?? Array.Empty<AgentToolCall>())
                        {
                            content.Add(new JsonObject
                            {
                                ["type"] = "tool_use",
                                ["id"] = call.Id,
                                ["name"] = call.Name,
                                ["input"] = JsonNode.Parse(string.IsNullOrWhiteSpace(call.ArgumentsJson) ? "{}" : call.ArgumentsJson),
                            });
                        }
                        array.Add(new JsonObject { ["role"] = "assistant", ["content"] = content });
                        break;

                    case AgentRole.ToolResult:
                        var results = new JsonArray();
                        foreach (var result in message.ToolResults ?? Array.Empty<AgentToolResult>())
                        {
                            results.Add(new JsonObject
                            {
                                ["type"] = "tool_result",
                                ["tool_use_id"] = result.ToolCallId,
                                ["content"] = result.ResultText,
                            });
                        }
                        array.Add(new JsonObject { ["role"] = "user", ["content"] = results });
                        break;
                }
            }
            return array;
        }

        private static AgentTurnResult ParseResponse(string json)
        {
            using var doc = JsonDocument.Parse(json);
            var content = doc.RootElement.GetProperty("content");

            string? text = null;
            var toolCalls = new List<AgentToolCall>();

            foreach (var block in content.EnumerateArray())
            {
                var type = block.GetProperty("type").GetString();
                if (type == "text")
                {
                    text = (text ?? "") + block.GetProperty("text").GetString();
                }
                else if (type == "tool_use")
                {
                    var id = block.GetProperty("id").GetString()!;
                    var name = block.GetProperty("name").GetString()!;
                    var input = block.GetProperty("input").GetRawText();
                    toolCalls.Add(new AgentToolCall(id, name, input));
                }
            }

            return new AgentTurnResult(text, toolCalls);
        }
    }
}
