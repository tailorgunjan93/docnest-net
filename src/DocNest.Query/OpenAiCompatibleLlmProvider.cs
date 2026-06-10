using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace DocNest.Query;

/// <summary>
/// <see cref="ILlmProvider"/> for any OpenAI-compatible <c>/chat/completions</c> endpoint (OpenAI, Groq,
/// Together, OpenRouter, local servers, …). Configurable base URL, model, and API key. HTTP via the BCL.
/// </summary>
public sealed class OpenAiCompatibleLlmProvider : ILlmProvider
{
    private readonly HttpClient _http;
    private readonly string _model;
    private readonly string _baseUrl;

    /// <inheritdoc/>
    public string ProviderName => "openai-compatible";

    /// <inheritdoc/>
    public string ModelName => _model;

    /// <summary>Create a provider. <paramref name="baseUrl"/> defaults to the OpenAI API.</summary>
    public OpenAiCompatibleLlmProvider(string apiKey, string model, string baseUrl = "https://api.openai.com/v1", HttpClient? httpClient = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(model);
        _model = model;
        _baseUrl = baseUrl.TrimEnd('/');
        _http = httpClient ?? new HttpClient();
        if (!string.IsNullOrEmpty(apiKey))
        {
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }
    }

    /// <inheritdoc/>
    public async Task<string> CompleteAsync(
        string prompt, string system = "", double temperature = 0.1, int maxTokens = 512, CancellationToken cancellationToken = default)
    {
        var messages = new List<OpenAiMessage>();
        if (!string.IsNullOrEmpty(system))
        {
            messages.Add(new OpenAiMessage("system", system));
        }
        messages.Add(new OpenAiMessage("user", prompt));
        var request = new OpenAiRequest(_model, messages, temperature, maxTokens);

        try
        {
            using var response = await _http
                .PostAsJsonAsync($"{_baseUrl}/chat/completions", request, LlmJson.Default.OpenAiRequest, cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                throw new IntelligenceException($"LLM request failed ({(int)response.StatusCode}): {error}");
            }

            var parsed = await response.Content
                .ReadFromJsonAsync(LlmJson.Default.OpenAiResponse, cancellationToken)
                .ConfigureAwait(false);
            return parsed?.Choices?.FirstOrDefault()?.Message?.Content ?? string.Empty;
        }
        catch (Exception ex) when (ex is not IntelligenceException and not OperationCanceledException)
        {
            throw new IntelligenceException($"LLM call failed: {ex.Message}", ex);
        }
    }
}
