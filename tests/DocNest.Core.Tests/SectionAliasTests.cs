using System.Text.Json;
using DocNest;
using FluentAssertions;
using Xunit;

namespace DocNest.Tests;

/// <summary>U3 — SectionId aliases Id and is never serialised.</summary>
public class SectionAliasTests
{
    [Fact]
    public void SectionId_aliases_Id()
    {
        var s = new Section { Id = "§3.1", Title = "x", Level = 2, Text = "y" };
        s.SectionId.Should().Be("§3.1");
    }

    [Fact]
    public void SectionId_is_not_serialised()
    {
        var s = new Section { Id = "§3.1", Title = "x", Level = 2, Text = "y" };
        var json = JsonSerializer.Serialize(s, DocNestJson.Default.Section);
        json.Should().NotContain("section_id");
        json.Should().NotContain("SectionId");
    }
}
