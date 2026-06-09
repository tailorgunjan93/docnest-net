using System;
using System.Collections.Generic;
using System.Linq;
using DocNest;
using DocNest.Pipeline;
using FluentAssertions;
using Xunit;

namespace DocNest.Tests.Pipeline;

public class SectionNormaliserTests
{
    private static RawDocument Raw(params (int Level, string Title, string Text)[] heads) => new()
    {
        DocId = "d",
        Title = "t",
        Source = "s.md",
        Format = "md",
        Sections = heads.Select(h => new Section { Id = "", Title = h.Title, Level = h.Level, Text = h.Text }).ToList(),
    };

    private static Document Norm(params (int, string, string)[] heads)
        => new SectionNormaliser().Normalise(Raw(heads));

    [Fact]
    public void Assigns_sequential_top_level_and_nested_ids()
    {
        var doc = Norm((1, "A", ""), (2, "A1", ""), (2, "A2", ""), (1, "B", ""));
        doc.Sections.Select(s => s.Id).Should().Equal("§1", "§1.1", "§1.2", "§2");
    }

    [Fact]
    public void Builds_three_level_nesting_with_links()
    {
        var doc = Norm((1, "A", ""), (2, "A1", ""), (3, "A11", ""));
        doc.Sections.Select(s => s.Id).Should().Equal("§1", "§1.1", "§1.1.1");
        doc.Sections[0].Children.Should().Equal("§1.1");
        doc.Sections[1].Children.Should().Equal("§1.1.1");
        doc.Sections[1].ParentId.Should().Be("§1");
        doc.Sections[2].ParentId.Should().Be("§1.1");
    }

    [Fact]
    public void Compacts_skipped_heading_levels()
    {
        var doc = Norm((1, "A", ""), (3, "deep", ""));
        doc.Sections.Select(s => s.Id).Should().Equal("§1", "§1.1"); // not §1.0.1
        doc.Sections[1].ParentId.Should().Be("§1");
    }

    [Fact]
    public void Clamps_levels_out_of_range()
    {
        var doc = Norm((0, "A", ""), (9, "B", ""));
        doc.Sections[0].Id.Should().Be("§1");
        doc.Sections[1].Id.Should().Be("§1.1");
    }

    [Theory]
    [InlineData("", 0)]
    [InlineData("one two three", 3)]
    [InlineData("a b c d e f g h i j", 13)]
    public void Token_count_is_truncated_words_times_1_3(string text, int expected)
        => Norm((1, "t", text)).Sections[0].TokenCount.Should().Be(expected);

    [Fact]
    public void Normalises_table_row_widths()
    {
        var raw = new RawDocument
        {
            DocId = "d", Title = "t", Source = "s.md", Format = "md",
            Sections = new[]
            {
                new Section
                {
                    Id = "", Title = "T", Level = 1, Text = "",
                    Tables = new[]
                    {
                        new TableData
                        {
                            TableId = "t1", Headers = new[] { "A", "B" },
                            Rows = new[]
                            {
                                (IReadOnlyList<string>)new[] { "1" },
                                new[] { "2", "3", "4" },
                                new[] { "5", "6" },
                            },
                        },
                    },
                },
            },
        };

        var section = new SectionNormaliser().Normalise(raw).Sections[0];
        section.Tables[0].Rows.Should().HaveCount(3);
        section.Tables[0].Rows[0].Should().Equal("1", "");
        section.Tables[0].Rows[1].Should().Equal("2", "3");
        section.Tables[0].Rows[2].Should().Equal("5", "6");
    }

    [Fact]
    public void Zero_header_table_is_unchanged()
    {
        var raw = new RawDocument
        {
            DocId = "d", Title = "t", Source = "s.md", Format = "md",
            Sections = new[]
            {
                new Section
                {
                    Id = "", Title = "T", Level = 1, Text = "",
                    Tables = new[]
                    {
                        new TableData
                        {
                            TableId = "t1", Headers = Array.Empty<string>(),
                            Rows = new[] { (IReadOnlyList<string>)new[] { "x", "y", "z" } },
                        },
                    },
                },
            },
        };

        var section = new SectionNormaliser().Normalise(raw).Sections[0];
        section.Tables[0].Rows[0].Should().Equal("x", "y", "z");
    }
}
