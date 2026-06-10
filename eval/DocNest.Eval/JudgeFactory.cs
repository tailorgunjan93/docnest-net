using DocNest.Query;

namespace DocNest.Eval;

/// <summary>
/// Selects the answer judge from the environment: when <c>DOCNEST_JUDGE_API_KEY</c> is set, an
/// <see cref="LlmAnswerJudge"/> over a rate-limit-resilient OpenAI-compatible provider; otherwise the
/// zero-cost <see cref="LocalAnswerJudge"/>. The judge LLM is independent of the answer-generation LLM
/// (<c>DOCNEST_LLM_API_KEY</c>); defaults mirror that wiring but target Groq's gpt-oss-120b for parity
/// with the Slice-8 eval runs (Groq key in <c>D:\Learning\docnest\.env</c>).
/// </summary>
internal static class JudgeFactory
{
    /// <summary>Build the judge from process environment variables.</summary>
    public static IAnswerJudge Create() => Create(
        Environment.GetEnvironmentVariable("DOCNEST_JUDGE_API_KEY"),
        Environment.GetEnvironmentVariable("DOCNEST_JUDGE_MODEL") ?? "openai/gpt-oss-120b",
        Environment.GetEnvironmentVariable("DOCNEST_JUDGE_BASE_URL") ?? "https://api.groq.com/openai/v1");

    /// <summary>Testable overload: no key ⇒ local judge; key present ⇒ LLM judge.</summary>
    public static IAnswerJudge Create(string? apiKey, string model, string baseUrl)
        => string.IsNullOrEmpty(apiKey)
            ? new LocalAnswerJudge()
            : new LlmAnswerJudge(new RetryingLlmProvider(new OpenAiCompatibleLlmProvider(apiKey, model, baseUrl)));
}
