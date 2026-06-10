using System.Text.Json.Serialization;

namespace DocNest.Query;

internal sealed record OpenAiMessage(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("content")] string Content);

internal sealed record OpenAiRequest(
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("messages")] IReadOnlyList<OpenAiMessage> Messages,
    [property: JsonPropertyName("temperature")] double Temperature,
    [property: JsonPropertyName("max_tokens")] int MaxTokens);

internal sealed record OpenAiChoice([property: JsonPropertyName("message")] OpenAiMessage? Message);

internal sealed record OpenAiResponse([property: JsonPropertyName("choices")] IReadOnlyList<OpenAiChoice>? Choices);

internal sealed record AnthropicMessage(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("content")] string Content);

internal sealed record AnthropicRequest(
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("max_tokens")] int MaxTokens,
    [property: JsonPropertyName("temperature")] double Temperature,
    [property: JsonPropertyName("system")] string? System,
    [property: JsonPropertyName("messages")] IReadOnlyList<AnthropicMessage> Messages);

internal sealed record AnthropicContentBlock(
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("text")] string? Text);

internal sealed record AnthropicResponse([property: JsonPropertyName("content")] IReadOnlyList<AnthropicContentBlock>? Content);

/// <summary>Source-generated JSON for the LLM provider request/response DTOs.</summary>
[JsonSourceGenerationOptions(DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(OpenAiRequest))]
[JsonSerializable(typeof(OpenAiResponse))]
[JsonSerializable(typeof(AnthropicRequest))]
[JsonSerializable(typeof(AnthropicResponse))]
internal partial class LlmJson : JsonSerializerContext;
