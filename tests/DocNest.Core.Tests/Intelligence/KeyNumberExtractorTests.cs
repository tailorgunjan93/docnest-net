using System.Collections.Generic;
using System.Linq;
using DocNest;
using DocNest.Intelligence;
using FluentAssertions;
using Xunit;

namespace DocNest.Tests.Intelligence;

public class KeyNumberExtractorTests
{
    private static IReadOnlyList<KeyNumber> Ex(string text) => KeyNumberExtractor.Extract(text, "§1");

    [Fact]
    public void Extracts_money_percent_duration_ratio_count_with_labels()
    {
        var kn = Ex("Revenue: $142M\nMargin: 23%\nLatency: 142ms\nSpeedup: 8x\nHeadcount: 1200");
        kn.Should().Contain(k => k.Label == "Revenue" && k.Value == "$142M" && k.Unit == "USD");
        kn.Should().Contain(k => k.Value == "23%" && k.Unit == "%");
        kn.Should().Contain(k => k.Value == "142ms" && k.Unit == null);
        kn.Should().Contain(k => k.Value == "8x");
        kn.Should().Contain(k => k.Label == "Headcount" && k.Value == "1200");
    }

    [Fact]
    public void Drops_bare_years()
        => Ex("Founded in 2024").Should().NotContain(k => k.Value == "2024");

    [Fact]
    public void Drops_identifiers_and_acronym_codes()
        => Ex("Certified AZ-204 and ISO 27001 standards").Should().BeEmpty();

    [Fact]
    public void Skips_ordered_list_markers()
    {
        var kn = Ex("1. Revenue grew to $30M");
        kn.Should().ContainSingle();
        kn[0].Value.Should().Be("$30M");
    }

    [Fact]
    public void Dedups_same_label_and_value()
        => Ex("Uptime: 99.9%\nUptime: 99.9%").Where(k => k.Value == "99.9%").Should().HaveCount(1);

    [Theory]
    [InlineData("$1.2 billion", 1.2e9)]
    [InlineData("18,400", 18400)]
    [InlineData("5k", 5000)]
    [InlineData("23%", 23)]
    public void ParseNumber_canonicalises(string raw, double expected)
        => KeyNumberExtractor.ParseNumber(raw)!.Value.Should().BeApproximately(expected, 1e-6);

    [Fact]
    public void Enrich_is_noop_when_already_populated()
    {
        var doc = new Document
        {
            DocId = "d", Title = "t", Source = "s", Format = "md",
            Sections = new[] { new Section { Id = "§1", Title = "T", Level = 1, Text = "Revenue $5M" } },
            KeyNumbers = new[] { new KeyNumber { Label = "X", Value = "1", Section = "§1" } },
        };

        KeyNumberExtractor.Enrich(doc).KeyNumbers.Should().ContainSingle().Which.Label.Should().Be("X");
    }

    [Fact]
    public void Enrich_populates_from_section_text_when_empty()
    {
        var doc = new Document
        {
            DocId = "d", Title = "t", Source = "s", Format = "md",
            Sections = new[] { new Section { Id = "§1", Title = "T", Level = 1, Text = "Revenue: $142M" } },
        };

        KeyNumberExtractor.Enrich(doc).KeyNumbers.Should().Contain(k => k.Value == "$142M");
    }
}
