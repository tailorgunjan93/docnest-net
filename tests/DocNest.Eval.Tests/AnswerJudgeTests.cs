using System.Threading;
using System.Threading.Tasks;
using DocNest;          // ILlmProvider
using DocNest.Eval;     // IAnswerJudge, JudgeVerdict, LocalAnswerJudge, LlmAnswerJudge, JudgeFactory, LocalJudge
using FluentAssertions;
using Xunit;

namespace DocNest.Eval.Tests;

/// <summary>
/// SLICE-09: optional LLM-as-judge for the accuracy eval. The judge grades 0–10 with the Python-parity
/// rubric when <c>DOCNEST_JUDGE_API_KEY</c> is set, else falls back to the zero-cost <see cref="LocalJudge"/>.
/// All tests are offline (a scripted <see cref="ILlmProvider"/> fake — no network).
/// </summary>
public class AnswerJudgeTests
{
    // ── LlmAnswerJudge.ParseScore — faithful port of the Python _judge parse loop ────────────────
    [Theory]
    [InlineData("SCORE: 8\nREASONING: captures the core claim", 8)]
    [InlineData("SCORE: 8/10\nREASONING: minor gap", 8)]   // first match wins (8, not the later 10)
    [InlineData("Score:7", 7)]
    [InlineData("score: 9 out of 10", 9)]
    [InlineData("SCORE: 10\nREASONING: perfect", 10)]
    [InlineData("SCORE: 0\nREASONING: hallucinated", 0)]
    public void ParseScore_parses_score_variants(string response, int expected)
    {
        LlmAnswerJudge.ParseScore(response).Score.Should().Be(expected);
    }

    [Theory]
    [InlineData("no score in here at all")]
    [InlineData("SCORE: banana")]
    [InlineData("SCORE: 99")]   // \b([0-9]|10)\b matches neither digit of "99" nor "10"
    [InlineData("")]
    public void ParseScore_defaults_to_5_with_parse_error_when_unparseable(string response)
    {
        var v = LlmAnswerJudge.ParseScore(response);
        v.Score.Should().Be(5);
        v.Reason.Should().Be("parse error");
    }

    [Fact]
    public void ParseScore_extracts_reasoning_after_first_colon()
    {
        var v = LlmAnswerJudge.ParseScore("SCORE: 9\nREASONING: correct: a minor omission");
        v.Score.Should().Be(9);
        v.Reason.Should().Be("correct: a minor omission");
    }

    // ── LlmAnswerJudge.ScoreAsync — uses the provider, embeds the inputs + rubric in the prompt ──
    [Fact]
    public async Task LlmAnswerJudge_scores_via_provider_and_embeds_inputs()
    {
        var llm = new CapturingLlm("SCORE: 9\nREASONING: correct");
        var judge = new LlmAnswerJudge(llm);

        var v = await judge.ScoreAsync("In which month?", "November.", "November 2015");

        v.Score.Should().Be(9);
        llm.Calls.Should().Be(1);
        llm.LastPrompt.Should().Contain("In which month?");
        llm.LastPrompt.Should().Contain("November.");
        llm.LastPrompt.Should().Contain("November 2015");
        llm.LastPrompt.Should().ContainEquivalentOf("score");   // rubric / format present
        judge.Name.Should().Contain("LLM");
    }

    // ── JudgeFactory — env-var gating without touching real environment ──────────────────────────
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void JudgeFactory_falls_back_to_local_when_no_key(string? apiKey)
    {
        var judge = JudgeFactory.Create(apiKey, "openai/gpt-oss-120b", "https://api.groq.com/openai/v1");
        judge.Should().BeOfType<LocalAnswerJudge>();
        judge.Name.Should().Contain("local");
    }

    [Fact]
    public void JudgeFactory_uses_llm_when_key_present()
    {
        var judge = JudgeFactory.Create("sk-test", "openai/gpt-oss-120b", "https://api.groq.com/openai/v1");
        judge.Should().BeOfType<LlmAnswerJudge>();
        judge.Name.Should().Contain("LLM");
    }

    // ── Regression anchor — LocalAnswerJudge must mirror the existing LocalJudge exactly ─────────
    [Theory]
    [InlineData("In which month?", "November.", "November 2015")]
    [InlineData("Total Q1 revenue?", "12,550 USD thousands", "12,550 USD thousands")]
    [InlineData("Anything?", "not found in context", "the answer is 42")]
    public async Task LocalAnswerJudge_matches_LocalJudge(string q, string cand, string truth)
    {
        var (localScore, localReason) = LocalJudge.Score(q, cand, truth);
        var via = await new LocalAnswerJudge().ScoreAsync(q, cand, truth);
        via.Score.Should().Be(localScore);
        via.Reason.Should().Be(localReason);
    }

    [Fact]
    public async Task LocalJudge_scores_correct_numberless_answer_below_hit_bar()
    {
        // Documents the comparability gap SLICE-09 addresses: a correct month answer whose ground
        // truth contains a number scores < 7 locally, where the LLM judge awards >= 7.
        var v = await new LocalAnswerJudge().ScoreAsync(
            "In which month was the agreement signed?", "November.", "November 2015");
        v.Score.Should().BeLessThan(7);
    }

    /// <summary>Offline <see cref="ILlmProvider"/> that returns a fixed reply and records the prompt.</summary>
    private sealed class CapturingLlm : ILlmProvider
    {
        private readonly string _reply;
        public CapturingLlm(string reply) => _reply = reply;
        public int Calls { get; private set; }
        public string LastPrompt { get; private set; } = "";
        public string ProviderName => "fake";
        public string ModelName => "fake-judge";

        public Task<string> CompleteAsync(string prompt, string system = "", double temperature = 0.1, int maxTokens = 512, CancellationToken cancellationToken = default)
        {
            Calls++;
            LastPrompt = prompt;
            return Task.FromResult(_reply);
        }
    }
}
