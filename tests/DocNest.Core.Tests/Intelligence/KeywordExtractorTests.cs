using DocNest;
using DocNest.Intelligence;
using FluentAssertions;
using Xunit;

namespace DocNest.Tests.Intelligence;

public class KeywordExtractorTests
{
    [Fact]
    public void Removes_stopwords_and_prioritises_title_terms()
    {
        var kw = KeywordExtractor.Extract("the revenue grew because revenue was strong", "Revenue Outlook");
        kw.Should().NotContain("the");
        kw.Should().NotContain("was");
        kw[0].Should().Be("revenue");
    }

    [Fact]
    public void Orders_remaining_by_frequency()
    {
        var kw = KeywordExtractor.Extract("alpha beta beta gamma gamma gamma");
        kw.Should().Equal("gamma", "beta", "alpha");
    }

    [Fact]
    public void Limits_to_k()
    {
        var kw = KeywordExtractor.Extract("alpha beta gamma delta epsilon zeta", k: 3);
        kw.Should().HaveCount(3);
    }

    [Fact]
    public void Enrich_fills_only_empty_sections()
    {
        var doc = new Document
        {
            DocId = "d", Title = "t", Source = "s", Format = "md",
            Sections = new[]
            {
                new Section { Id = "§1", Title = "Revenue", Level = 1, Text = "revenue grew strongly revenue" },
                new Section { Id = "§2", Title = "Costs", Level = 1, Text = "costs fell", Keywords = new[] { "preset" } },
            },
        };

        var outDoc = KeywordExtractor.Enrich(doc);
        outDoc.Sections[0].Keywords.Should().Contain("revenue");
        outDoc.Sections[1].Keywords.Should().Equal("preset");
    }
}
