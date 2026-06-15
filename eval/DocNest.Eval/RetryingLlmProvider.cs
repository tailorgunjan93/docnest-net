using System.Globalization;
using System.Text.RegularExpressions;
using DocNest;
using DocNest.Query;

namespace DocNest.Eval;

/// <summary>
/// Eval-only decorator that makes an <see cref="ILlmProvider"/> resilient to HTTP 429 rate limits
/// (Groq free tier is 8000 TPM) <em>and</em> to transient transport faults (e.g. a remote connection
/// reset / <c>SocketException 10054</c>, common on long batch runs against hosted endpoints). Retries
/// rate limits honouring the server's "try again in Xs" hint when present, else exponential backoff;
/// retries transient transport errors with exponential backoff. Keeps the engine's LLM path pure —
/// retry lives here.
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
            catch (IntelligenceException ex) when (IsTransient(ex) && attempt < _maxRetries)
            {
                var wait = TimeSpan.FromSeconds(Math.Min(Math.Pow(2, attempt), 15));
                Console.Error.WriteLine($"  [transient] {InnermostName(ex)}: retrying in {wait.TotalSeconds:F1}s (attempt {attempt + 1}/{_maxRetries})…");
                await Task.Delay(wait, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static bool IsRateLimit(IntelligenceException ex)
        => ex.Message.Contains("429", StringComparison.Ordinal)
        || ex.Message.Contains("rate_limit", StringComparison.OrdinalIgnoreCase);

    /// <summary>A transient transport fault worth retrying: connection reset/refused, I/O error, or timeout.</summary>
    private static bool IsTransient(IntelligenceException ex)
    {
        for (Exception? inner = ex; inner is not null; inner = inner.InnerException)
        {
            if (inner is System.Net.Http.HttpRequestException
                or System.IO.IOException
                or System.Net.Sockets.SocketException
                or TaskCanceledException
                or TimeoutException)
            {
                return true;
            }
        }
        return false;
    }

    private static string InnermostName(Exception ex)
    {
        var inner = ex;
        while (inner.InnerException is not null) { inner = inner.InnerException; }
        return inner.GetType().Name;
    }

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
