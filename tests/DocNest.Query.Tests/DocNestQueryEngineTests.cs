using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DocNest;
using DocNest.Query;
using FluentAssertions;
using Xunit;

namespace DocNest.Query.Tests;

public class DocNestQueryEngineTests
{
    [Fact]
    public async Task Layer0_key_number_answers_with_zero_tokens_no_llm()
    {
        var llm = new FakeLlm();
        var doc = Doc(
            keyNumbers: new[] { new KeyNumber { Label = "Uptime", Value = "99.9%", Unit = "percent", Section = "§1" } },
            sections: Sec("§1", "Ops", "Operational details."));
        var engine = new DocNestQueryEngine(new FakeRetriever(), llm);

        var result = await engine.AnswerAsync(doc, "what is the uptime?");

        result.LayerUsed.Should().Be(0);
        result.TokensUsed.Should().Be(0);
        result.Answer.Should().Contain("Uptime: 99.9%");
        llm.Calls.Should().Be(0);
    }

    [Fact]
    public async Task Layer0_summary_routing()
    {
        var doc = Doc(summary: "This is the overview.", sections: Sec("§1", "A", "x"));
        var engine = new DocNestQueryEngine(new FakeRetriever(), new FakeLlm());

        var result = await engine.AnswerAsync(doc, "summarise this");

        result.LayerUsed.Should().Be(0);
        result.Answer.Should().Be("This is the overview.");
    }

    [Fact]
    public async Task Layer1_returns_section_summary_zero_tokens()
    {
        var llm = new FakeLlm();
        // Confident match (both query tokens present) → Layer 1 summary, 0 tokens (SLICE-08 gate).
        var section = Sec("§3", "Revenue", "Revenue growth was strong.", summary: "Revenue increased 20%.");
        var engine = new DocNestQueryEngine(new FakeRetriever(section), llm);

        var result = await engine.AnswerAsync(Doc(sections: section), "revenue growth");

        result.LayerUsed.Should().Be(1);
        result.TokensUsed.Should().Be(0);
        result.Citations.Should().Equal("§3");
        llm.Calls.Should().Be(0);
    }

    [Fact]
    public async Task Layer2_uses_llm_when_no_deterministic_answer()
    {
        var llm = new FakeLlm();
        // Partial match (2 of 4 query tokens) → mid confidence → single-section LLM (Layer 2).
        var section = Sec("§1", "X", "Quarterly revenue details.");
        var engine = new DocNestQueryEngine(new FakeRetriever(section), llm);

        var result = await engine.AnswerAsync(Doc(sections: section), "explain the quarterly revenue figures");

        result.LayerUsed.Should().Be(2);
        result.TokensUsed.Should().BeGreaterThan(0);
        result.Answer.Should().Be("LLM answer");
        result.Citations.Should().Equal("§1");
        llm.Calls.Should().Be(1);
    }

    [Fact]
    public async Task AllowLlm_false_stops_after_layer1()
    {
        var llm = new FakeLlm();
        var section = Sec("§1", "X", "Lorem ipsum dolor sit amet.");
        var engine = new DocNestQueryEngine(new FakeRetriever(section), llm);

        var result = await engine.AnswerAsync(Doc(sections: section), "explain quarterly revenue", allowLlm: false);

        result.LayerUsed.Should().Be(-1);
        llm.Calls.Should().Be(0);
    }

    // ---- helpers ----

    private static Document Doc(string? summary = null, IEnumerable<string>? insights = null, IEnumerable<KeyNumber>? keyNumbers = null, params Section[] sections) => new()
    {
        DocId = "d",
        Title = "t",
        Source = "s",
        Format = "md",
        Summary = summary,
        Insights = insights?.ToList() ?? new List<string>(),
        KeyNumbers = keyNumbers?.ToList() ?? new List<KeyNumber>(),
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
}
