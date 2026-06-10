using System.Net.Http.Json;

namespace DocNest.Query;

/// <summary>
/// <see cref="ILlmProvider"/> for Anthropic's Messages API (<c>/v1/messages</c>, <c>x-api-key</c> +
/// <c>anthropic-version</c>). HTTP via the BCL.
/// </summary>
public sealed class AnthropicLlmProvider : ILlmProvider
{
    private const string AnthropicVersion = "2023-06-01";
    private readonly HttpClient _http;
    private readonly string _model;
    private readonly string _baseUrl;

    /// <inheritdoc/>
    public string ProviderName => "anthropic";

    /// <inheritdoc/>
    public string ModelName => _model;

    /// <summary>Create a provider. <paramref name="baseUrl"/> defaults to the Anthropic API.</summary>
    public AnthropicLlmProvider(string apiKey, string model, string baseUrl = "https://api.anthropic.com", HttpClient? httpClient = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(model);
        _model = model;
        _baseUrl = baseUrl.TrimEnd('/');
        _http = httpClient ?? new HttpClient();
        if (!string.IsNullOrEmpty(apiKey) && !_http.DefaultRequestHeaders.Contains("x-api-key"))
        {
            _http.DefaultRequestHeaders.TryAddWithoutValidation("x-api-key", apiKey);
        }
        if (!_http.DefaultRequestHeaders.Contains("anthropic-version"))
        {
            _http.DefaultRequestHeaders.TryAddWithoutValidation("anthropic-version", AnthropicVersion);
        }
    }

    /// <inheritdoc/>
    public async Task<string> CompleteAsync(
        string prompt, string system = "", double temperature = 0.1, int maxTokens = 512, CancellationToken cancellationToken = default)
    {
        var request = new AnthropicRequest(
            _model,
            maxTokens,
            temperature,
            string.IsNullOrEmpty(system) ? null : system,
            new[] { new AnthropicMessage("user", prompt) });

        try
        {
            using var response = await _http
                .PostAsJsonAsync($"{_baseUrl}/v1/messages", request, LlmJson.Default.AnthropicRequest, cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                throw new IntelligenceException($"Anthropic request failed ({(int)response.StatusCode}): {error}");
            }

            var parsed = await response.Content
                .ReadFromJsonAsync(LlmJson.Default.AnthropicResponse, cancellationToken)
                .ConfigureAwait(false);
            return parsed?.Content?.FirstOrDefault(b => b.Type == "text")?.Text ?? string.Empty;
        }
        catch (Exception ex) when (ex is not IntelligenceException and not OperationCanceledException)
        {
            throw new IntelligenceException($"Anthropic call failed: {ex.Message}", ex);
        }
    }
}
