namespace DocNest;

/// <summary>
/// Abstract interface for language-model completions. Implement this to add a new LLM backend
/// without changing any pipeline code. Mirrors the Python <c>ILLMProvider</c> ABC.
/// </summary>
public interface ILlmProvider
{
    /// <summary>Generate a completion for the given prompt.</summary>
    /// <param name="prompt">User message / instruction.</param>
    /// <param name="system">System prompt (role/persona). Empty = no system message.</param>
    /// <param name="temperature">Sampling temperature (0.0 = deterministic, 1.0 = creative).</param>
    /// <param name="maxTokens">Maximum tokens in the generated response.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Generated text.</returns>
    /// <exception cref="IntelligenceException">If the call fails (auth, network, quota, etc.).</exception>
    Task<string> CompleteAsync(
        string prompt,
        string system = "",
        double temperature = 0.1,
        int maxTokens = 512,
        CancellationToken cancellationToken = default);

    /// <summary>Canonical provider name — e.g. <c>groq</c>, <c>openai</c>, <c>ollama</c>.</summary>
    string ProviderName { get; }

    /// <summary>Model identifier as passed to the provider.</summary>
    string ModelName { get; }
}
