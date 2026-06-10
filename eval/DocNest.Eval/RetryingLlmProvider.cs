using System.Globalization;
using System.Text.RegularExpressions;
using DocNest;
using DocNest.Query;

namespace DocNest.Eval;

/// <summary>
/// Eval-only decorator that makes an <see cref="ILlmProvider"/> resilient to HTTP 429 rate limits
/// (Groq free tier is 8000 TPM). Retries on rate-limit errors, honouring the server's "try again in Xs"
/// hint when present, else exponential backoff. Keeps the engine's LLM path pure — retry lives here.
/// </summary>
internal sealed partial class RetryingLlmProvider : ILlmProvider
{
    private readonly ILlmProvider _inner;
    private readonly int _maxRetries;

    public RetryingLlmProvider(ILlmProvider inner, int maxRetries = 8)
    {
        _inner = inner;
        _maxRetries = maxRetries;
    }

    public string ProviderName => _inner.ProviderName;
    public string ModelName => _inner.ModelName;

    [GeneratedRegex(@"try again in ([\d.]+)s")]
    private static partial Regex RetryAfterRe();

    public async Task<string> CompleteAsync(
        string prompt, string system = "", double temperature = 0.1, int maxTokens = 512, CancellationToken cancellationToken = default)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return await _inner.CompleteAsync(prompt, system, temperature, maxTokens, cancellationToken).ConfigureAwait(false);
            }
            catch (IntelligenceException ex) when (IsRateLimit(ex) && attempt < _maxRetries)
            {
                var wait = RetryAfter(ex.Message, attempt);
                Console.Error.WriteLine($"  [rate-limit] waiting {wait.TotalSeconds:F1}s (attempt {attempt + 1}/{_maxRetries})…");
                await Task.Delay(wait, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static bool IsRateLimit(IntelligenceException ex)
        => ex.Message.Contains("429", StringComparison.Ordinal)
        || ex.Message.Contains("rate_limit", StringComparison.OrdinalIgnoreCase);

    private static TimeSpan RetryAfter(string message, int attempt)
    {
        var m = RetryAfterRe().Match(message);
        if (m.Success && double.TryParse(m.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var secs))
        {
            return TimeSpan.FromSeconds(Math.Min(secs + 0.5, 30));
        }
        return TimeSpan.FromSeconds(Math.Min(2 * Math.Pow(2, attempt), 30));
    }
}
