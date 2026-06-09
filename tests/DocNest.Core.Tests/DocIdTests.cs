using System.IO;
using DocNest;
using FluentAssertions;
using Xunit;

namespace DocNest.Tests;

/// <summary>U4 — DocId.FromPath matches the Python _make_doc_id slug rules.</summary>
public class DocIdTests
{
    [Theory]
    [InlineData("GunjanTailor", "gunjan-tailor")]
    [InlineData("Report2024", "report-2024")]
    [InlineData("2024Report", "2024-report")]
    [InlineData("my_file name", "my-file-name")]
    [InlineData("AnnualReport2024Final", "annual-report-2024-final")]
    [InlineData("_Hello_", "hello")]
    [InlineData("Q4_Budget", "q-4-budget")]
    public void FromPath_slugifies_stem(string stem, string expected)
        => DocId.FromPath(stem).Should().Be(expected);

    [Fact]
    public void FromPath_strips_directory_and_extension()
        => DocId.FromPath(Path.Combine("reports", "GunjanTailor.pdf")).Should().Be("gunjan-tailor");
}
