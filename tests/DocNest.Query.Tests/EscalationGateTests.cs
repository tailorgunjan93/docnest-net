using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DocNest;
using DocNest.Query;
using FluentAssertions;
using Xunit;

namespace DocNest.Query.Tests;

/// <summary>
/// SLICE-08 (ADR-0011): the engine must escalate to the LLM when the top section is only weakly
/// related to the question (low confidence), instead of returning a 0-token extractive snippet.
/// </summary>
public class EscalationGateTests
{
    [Fact]
    public async Task LowConfidence_section_escalates_to_llm()
    {
        // Top section shares only 2 of many question tokens — enough for extractive to return a
        // snippet, which the pre-fix engine wrongly returned at Layer 1 (0 tokens, never escalating).
        var llm = new FakeLlm();
        // Section shares 2 of 7 query tokens ("variant", "sizes") — extractive returns a snippet,
        // which the pre-fix engine wrongly returned at Layer 1 (0 tokens) instead of escalating.
        var top = Sec("§1", "Introduction", "The model variant sizes are listed in the appendix.");
        var other = Sec("§2", "Appendix", "Unrelated appendix content about licensing terms.");
        var engine = new DocNestQueryEngine(new FakeRetriever(top, other), llm);

        var result = await engine.AnswerAsync(
            Doc(top, other),
            "what are all the parameter sizes list every variant from smallest to largest");

        result.LayerUsed.Should().BeGreaterThanOrEqualTo(2);
        result.TokensUsed.Should().BeGreaterThan(0);
        llm.Calls.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task HighConfidence_section_stays_layer1_zero_tokens()
    {
        var llm = new FakeLlm();
        var top = Sec("§1", "Reliability", "The platform uptime was 99.9% this quarter overall.");
        var engine = new DocNestQueryEngine(new FakeRetriever(top), llm);

        var result = await engine.AnswerAsync(Doc(top), "what is the platform uptime");

        result.LayerUsed.Should().Be(1);
        result.TokensUsed.Should().Be(0);
        llm.Calls.Should().Be(0);
    }

    [Fact]
    public async Task LowConfidence_without_provider_returns_empty()
    {
        var top = Sec("§1", "Introduction", "The model variant sizes are listed in the appendix.");
        var engine = new DocNestQueryEngine(new FakeRetriever(top), llm: null);

        var result = await engine.AnswerAsync(
            Doc(top), "what are all the parameter sizes list every variant from smallest to largest", allowLlm: true);

        result.LayerUsed.Should().Be(-1);
        result.Answer.Should().BeEmpty();
    }

    [Fact]
    public async Task Layer2_not_found_escalates_to_layer3()
    {
        // Mid-confidence top section → Layer 2; the LLM reports "Not found" → escalate to multi-section.
        var llm = new ScriptedLlm("Not found in §1", "Synthesised from multiple sections.");
        var top = Sec("§1", "Overview", "Quarterly revenue details.");
        var other = Sec("§2", "Detail", "More quarterly revenue context here.");
        var engine = new DocNestQueryEngine(new FakeRetriever(top, other), llm);

        var result = await engine.AnswerAsync(Doc(top, other), "explain the quarterly revenue figures here please");

        result.LayerUsed.Should().Be(3);
        result.Answer.Should().Be("Synthesised from multiple sections.");
        llm.Calls.Should().Be(2);
    }

    // ---- helpers ----

    private static Document Doc(params Section[] sections) => new()
    {
        DocId = "d", Title = "t", Source = "s", Format = "md",
        Sections = sections.ToList(),
    };

    private static Section Sec(string id, string title, string text, string? summary = null)
        => new() { Id = id, Title = title, Level = 1, Text = text, Summary = summary };

    private sealed class FakeRetriever : IRetriever
    {
        private readonly IReadOnlyList<RetrievalHit> _hits;
        public FakeRetriever(params Section[] sections)
            => _hits = sections.Select((s, i) => new RetrievalHit(s, 1.0 / (i + 1))).ToList();

        public Task<IReadOnlyList<RetrievalHit>> RetrieveAsync(Document doc, string query, int k = 8, CancellationToken cancellationToken = default)
            => Task.FromResult(_hits);
    }

    private sealed class FakeLlm : ILlmProvider
    {
        public int Calls { get; private set; }
        public string ProviderName => "fake";
        public string ModelName => "fake-1";

        public Task<string> CompleteAsync(string prompt, string system = "", double temperature = 0.1, int maxTokens = 512, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult("LLM answer");
        }
    }

    private sealed class ScriptedLlm : ILlmProvider
    {
        private readonly string[] _replies;
        public int Calls { get; private set; }
        public string ProviderName => "fake";
        public string ModelName => "fake-1";

        public ScriptedLlm(params string[] replies) => _replies = replies;

        public Task<string> CompleteAsync(string prompt, string system = "", double temperature = 0.1, int maxTokens = 512, CancellationToken cancellationToken = default)
        {
            var reply = _replies[System.Math.Min(Calls, _replies.Length - 1)];
            Calls++;
            return Task.FromResult(reply);
        }
    }
}
