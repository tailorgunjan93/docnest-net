using DocNest.Query;

namespace DocNest.Cli;

/// <summary>
/// Builds an <see cref="ILlmProvider"/> from CLI flags or environment variables
/// (<c>DOCNEST_LLM_PROVIDER/_API_KEY/_MODEL/_BASE_URL</c>). Returns <see langword="null"/> when nothing
/// is configured (deterministic-only answering).
/// </summary>
public static class LlmProviderFactory
{
    public static ILlmProvider? Create(string? provider, string? model, string? apiKey, string? baseUrl)
    {
        provider ??= Environment.GetEnvironmentVariable("DOCNEST_LLM_PROVIDER");
        apiKey ??= Environment.GetEnvironmentVariable("DOCNEST_LLM_API_KEY");
        model ??= Environment.GetEnvironmentVariable("DOCNEST_LLM_MODEL");
        baseUrl ??= Environment.GetEnvironmentVariable("DOCNEST_LLM_BASE_URL");

        if (string.IsNullOrEmpty(apiKey) && string.IsNullOrEmpty(provider))
        {
            return null;
        }

        return (provider ?? "openai").ToLowerInvariant() == "anthropic"
            ? new AnthropicLlmProvider(apiKey ?? string.Empty, model ?? "claude-3-5-haiku-latest", baseUrl ?? "https://api.anthropic.com")
            : new OpenAiCompatibleLlmProvider(apiKey ?? string.Empty, model ?? "gpt-4o-mini", baseUrl ?? "https://api.openai.com/v1");
    }
}
