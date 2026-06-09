using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using DocNest.Storage;
using DocNest.Udf;
using FluentAssertions;
using Xunit;

namespace DocNest.Tests.Udf;

public class WireFormatTests
{
    private static readonly string[] ManifestKeys =
    {
        "udf_version", "doc_id", "title", "source_format", "created_at", "embedding_model",
        "embedding_dims", "quantization", "section_count", "intelligence", "embedding_format",
        "owner", "department", "tags", "access_roles", "version", "last_updated", "producer",
    };

    private static readonly string[] CatalogueKeys =
    {
        "doc_id", "title", "source", "language", "summary", "insights", "owner", "department",
        "tags", "access_roles", "version", "last_updated", "key_numbers", "section_index",
        "embedding_model", "embedding_dims", "quantization",
    };

    private static readonly string[] ContentKeys = { "doc_id", "sections" };

    private static async Task<JsonDocument> ReadEntryJson(string udf, string name)
        => JsonDocument.Parse(await new ZipStorageBackend().ReadEntryAsync(udf, name));

    private static IEnumerable<string> TopKeys(JsonDocument d)
        => d.RootElement.EnumerateObject().Select(p => p.Name);

    [Fact]
    public async Task Wire_entries_have_the_python_key_sets()
    {
        var path = UdfTestData.TempUdf();
        try
        {
            await new UdfWriter().WriteAsync(UdfTestData.Sample(), path);

            using var manifest = await ReadEntryJson(path, "manifest.json");
            using var catalogue = await ReadEntryJson(path, "catalogue.json");
            using var content = await ReadEntryJson(path, "content.json");

            TopKeys(manifest).Should().BeEquivalentTo(ManifestKeys);
            TopKeys(catalogue).Should().BeEquivalentTo(CatalogueKeys);
            TopKeys(content).Should().BeEquivalentTo(ContentKeys);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Content_sections_is_a_map_keyed_by_section_id()
    {
        var path = UdfTestData.TempUdf();
        try
        {
            await new UdfWriter().WriteAsync(UdfTestData.Sample(), path);
            using var content = await ReadEntryJson(path, "content.json");

            var sections = content.RootElement.GetProperty("sections");
            sections.ValueKind.Should().Be(JsonValueKind.Object);
            sections.TryGetProperty("§1", out _).Should().BeTrue();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Non_ascii_is_written_raw_not_escaped()
    {
        var path = UdfTestData.TempUdf();
        try
        {
            await new UdfWriter().WriteAsync(UdfTestData.Sample(), path);
            var text = Encoding.UTF8.GetString(await new ZipStorageBackend().ReadEntryAsync(path, "catalogue.json"));

            text.Should().Contain("§");
            text.Should().Contain("Café");
            text.Should().NotContain("\\u00A7");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData("C:\\Users\\me\\report.pdf", "report.pdf")]
    [InlineData("/home/me/report.pdf", "report.pdf")]
    [InlineData("report.pdf", "report.pdf")]
    [InlineData("https://example.com/x/report.pdf", "https://example.com/x/report.pdf")]
    public void SourceSanitiser_matches_python(string input, string expected)
        => SourceSanitiser.Sanitise(input).Should().Be(expected);

    [Fact]
    public void SourceSanitiser_keepFull_returns_unchanged()
        => SourceSanitiser.Sanitise("C:\\a\\b.pdf", keepFull: true).Should().Be("C:\\a\\b.pdf");
}
