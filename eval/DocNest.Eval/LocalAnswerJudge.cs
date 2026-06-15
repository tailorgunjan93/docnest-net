namespace DocNest.Eval;

/// <summary>
/// The zero-cost default judge: delegates to the existing <see cref="LocalJudge"/> heuristic
/// (number ±6% + keyword + phrase overlap). No network, no tokens — used whenever
/// <c>DOCNEST_JUDGE_API_KEY</c> is unset, preserving the eval's deterministic behaviour exactly.
/// </summary>
internal sealed class LocalAnswerJudge : IAnswerJudge
{
    public string Name => "local (number ±6% + keyword + phrase overlap)";

    public Task<JudgeVerdict> ScoreAsync(string question, string candidate, string reference, CancellationToken cancellationToken = default)
    {
        var (score, reason) = LocalJudge.Score(question, candidate, reference);
        return Task.FromResult(new JudgeVerdict(score, reason));
    }
}
