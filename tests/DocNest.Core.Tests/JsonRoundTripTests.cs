using System.Collections.Generic;
using System.Text.Json;
using DocNest;
using FluentAssertions;
using Xunit;

namespace DocNest.Tests;

/// <summary>
/// Record-level serialisation self-consistency + key fidelity + forward/optional handling
/// (I3/I4). The authoritative .udf interop fixtures live in Slice 2 (wire DTOs).
/// </summary>
public class JsonRoundTripTests
{
    [Fact]
    public void Section_round_trips_through_json()
    {
        var s = new Section
        {
            Id = "§1.2",
            Title = "Revenue",
            Level = 2,
            Text = "Body text",
            Tables = new[]
            {
                new TableData
                {
                    TableId = "tbl_001",
                    Caption = "Q4",
                    Headers = new[] { "A", "B" },
                    Rows = new[] { (IReadOnlyList<string>)new[] { "1", "2" } },
                },
            },
            Children = new[] { "§1.2.1" },
            Keywords = new[] { "revenue" },
            TokenCount = 42,
            Embedding = new byte[] { 1, 2, 3, 4 },
        };

        var json = JsonSerializer.Serialize(s, DocNestJson.Default.Section);
        var back = JsonSerializer.Deserialize(json, DocNestJson.Default.Section);

        back.Should().BeEquivalentTo(s);
    }

    [Fact]
    public void Section_serialises_with_snake_case_keys()
    {
        var s = new Section { Id = "§1", Title = "t", Level = 1, Text = "x", TokenCount = 5 };
        var json = JsonSerializer.Serialize(s, DocNestJson.Default.Section);
        json.Should().Contain("\"token_count\"");
        json.Should().NotContain("tokenCount");
        json.Should().NotContain("TokenCount");
    }

    [Fact]
    public void Unknown_keys_are_ignored_on_deserialise()
    {
        const string json = """{"id":"§1","title":"t","level":1,"text":"x","future_field":123}""";
        var s = JsonSerializer.Deserialize(json, DocNestJson.Default.Section);
        s.Should().NotBeNull();
        s!.Id.Should().Be("§1");
    }

    [Fact]
    public void Absent_optionals_deserialise_to_null_and_empty_defaults()
    {
        const string json = """{"id":"§1","title":"t","level":1,"text":"x"}""";
        var s = JsonSerializer.Deserialize(json, DocNestJson.Default.Section)!;
        s.Summary.Should().BeNull();
        s.Embedding.Should().BeNull();
        s.ParentId.Should().BeNull();
        s.Tables.Should().BeEmpty();
    }

    [Fact]
    public void Document_round_trips_with_metadata_and_key_numbers()
    {
        var d = new Document
        {
            DocId = "annual-report",
            Title = "Annual Report",
            Source = "annual-report.pdf",
            Format = "pdf",
            Sections = new[] { new Section { Id = "§1", Title = "Intro", Level = 1, Text = "hello" } },
            Insights = new[] { "growth up" },
            KeyNumbers = new[] { new KeyNumber { Label = "Revenue", Value = "$142M", Section = "§1" } },
            Meta = new DocMeta { Owner = "Finance", Tags = new[] { "annual" } },
        };

        var json = JsonSerializer.Serialize(d, DocNestJson.Default.Document);
        json.Should().Contain("\"key_numbers\"");
        var back = JsonSerializer.Deserialize(json, DocNestJson.Default.Document);
        back.Should().BeEquivalentTo(d);
    }
}
