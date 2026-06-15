namespace DocNest.Eval;

/// <summary>A judge's verdict for one answer: a 0–10 <paramref name="Score"/> and a short reason.</summary>
internal readonly record struct JudgeVerdict(int Score, string Reason);

/// <summary>
/// Strategy for grading a candidate answer against a ground-truth reference on a 0–10 scale
/// (hit = score ≥ 7). Implementations: <see cref="LocalAnswerJudge"/> (zero-cost heuristic, the
/// default) and <see cref="LlmAnswerJudge"/> (LLM-as-judge, Python-parity rubric). Selected by
/// <see cref="JudgeFactory"/> on the <c>DOCNEST_JUDGE_API_KEY</c> env var.
/// </summary>
internal interface IAnswerJudge
{
    /// <summary>Human-readable judge descriptor for the report header (e.g. <c>local (…)</c>, <c>LLM (…)</c>).</summary>
    string Name { get; }

    /// <summary>Grade <paramref name="candidate"/> against <paramref name="reference"/> (the ground truth).</summary>
    Task<JudgeVerdict> ScoreAsync(string question, string candidate, string reference, CancellationToken cancellationToken = default);
}
