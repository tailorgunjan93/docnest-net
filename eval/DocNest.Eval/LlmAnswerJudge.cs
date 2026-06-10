using System.Text.RegularExpressions;
using DocNest;

namespace DocNest.Eval;

/// <summary>
/// LLM-as-judge: grades a candidate answer against the ground truth on 0–10 using the same prompt,
/// rubric, and robust SCORE/REASONING parse as the Python reference eval's <c>_judge</c>
/// (<c>D:\Learning\docnest\eval\rag_accuracy_eval.py</c>). All <c>cases.json</c> references are authored
/// ground truth, so the generous-approximate-number clause is always applied. The external LLM stays
/// behind <see cref="ILlmProvider"/> (typically wrapped by <see cref="RetryingLlmProvider"/> for 429s).
/// </summary>
internal sealed partial class LlmAnswerJudge : IAnswerJudge
{
    private readonly ILlmProvider _llm;

    public LlmAnswerJudge(ILlmProvider llm) => _llm = llm;

    public string Name => $"LLM ({_llm.ProviderName}/{_llm.ModelName})";

    public async Task<JudgeVerdict> ScoreAsync(string question, string candidate, string reference, CancellationToken cancellationToken = default)
    {
        var prompt = BuildPrompt(question, candidate, reference);
        // Low temperature for a stable grade; a generous token budget so reasoning models (e.g.
        // gpt-oss) still emit the SCORE/REASONING lines after their chain of thought.
        var response = await _llm.CompleteAsync(prompt, system: "", temperature: 0.0, maxTokens: 1024, cancellationToken)
            .ConfigureAwait(false);
        return ParseScore(response);
    }

    private static string BuildPrompt(string question, string candidate, string reference) =>
        $"""
        Score the CANDIDATE answer for factual accuracy against the GROUND TRUTH.
        IMPORTANT: When scoring against GROUND TRUTH, be generous with approximate numbers (e.g.,
        '~1.1°C' matches '1.09°C', '~500 GtCO2' matches '500 GtCO2'). Award 8-9 if the candidate
        captures the core factual claim correctly even with minor omissions or slightly different
        phrasing. Reserve 6 for partially correct, 4 for mostly wrong, 0 for completely wrong or hallucinated.

        QUESTION: {question}
        GROUND TRUTH: {reference}
        CANDIDATE: {candidate}

        Rubric: 10=perfect match, 9=correct with trivial omission, 8=mostly correct minor gaps,
                6=partially correct key facts missing, 4=mostly wrong, 2=almost entirely wrong,
                0=completely wrong or hallucinated.

        Respond EXACTLY:
        SCORE: <0-10>
        REASONING: <one sentence>
        """;

    /// <summary>
    /// Faithful port of the Python parse loop: a line starting "SCORE" yields the first
    /// <c>\b([0-9]|10)\b</c> match (handles "8", "8/10", "9 out of 10"); a line starting "REASONING"
    /// yields the text after the first colon. Unparseable output defaults to <c>(5, "parse error")</c>.
    /// Pure and deterministic — the offline unit-test seam.
    /// </summary>
    internal static JudgeVerdict ParseScore(string response)
    {
        var score = 5;
        var reason = "parse error";
        foreach (var raw in (response ?? string.Empty).Split('\r', '\n'))
        {
            var line = raw.Trim();
            var upper = line.ToUpperInvariant();
            if (upper.StartsWith("SCORE", StringComparison.Ordinal))
            {
                var m = ScoreRe().Match(line);
                if (m.Success && int.TryParse(m.Groups[1].Value, out var val) && val is >= 0 and <= 10)
                {
                    score = val;
                }
            }
            else if (upper.StartsWith("REASONING", StringComparison.Ordinal))
            {
                var colon = line.IndexOf(':', StringComparison.Ordinal);
                reason = colon >= 0 ? line[(colon + 1)..].Trim() : line;
            }
        }
        return new JudgeVerdict(score, reason);
    }

    [GeneratedRegex(@"\b([0-9]|10)\b")]
    private static partial Regex ScoreRe();
}
