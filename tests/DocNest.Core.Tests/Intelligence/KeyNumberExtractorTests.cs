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

    [Fact] // SLICE-08 D-C: name/version + structural references must not become key numbers.
    public void Skips_name_version_and_structural_reference_numbers()
    {
        var kn = Ex("Llama 2\nFigure 4\nSection 23\nTable 5\nPage 7");
        kn.Should().NotContain(k => k.Label == "Llama");
        kn.Should().NotContain(k => k.Label == "Figure");
        kn.Should().NotContain(k => k.Label == "Section");
        kn.Should().NotContain(k => k.Label == "Table");
        kn.Should().NotContain(k => k.Label == "Page");
    }

    [Fact] // SLICE-08 D-C: colon-bound and unit-bound metrics are still kept alongside references.
    public void Keeps_colon_bound_and_unit_metrics_alongside_references()
    {
        var kn = Ex("See Figure 3 for details.\nTotal engineers: 24\nUptime 99.9%");
        kn.Should().NotContain(k => k.Label == "Figure");
        kn.Should().Contain(k => k.Label.Contains("engineers") && k.Value == "24");
        kn.Should().Contain(k => k.Value == "99.9%");
    }

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
