using DocNest;
using FluentAssertions;
using Xunit;

namespace DocNest.Tests;

/// <summary>U1/U2/U6 — domain records expose every Python field with Python-matching defaults.</summary>
public class DomainDefaultsTests
{
    [Fact]
    public void DocMeta_defaults_match_python()
    {
        var m = new DocMeta();
        m.Owner.Should().Be("");
        m.Department.Should().Be("");
        m.Tags.Should().BeEmpty();
        m.AccessRoles.Should().Equal("*");
        m.Version.Should().Be("1.0");
        m.LastUpdated.Should().Be("");
    }

    [Fact]
    public void Catalogue_defaults_match_python()
    {
        var c = new Catalogue { DocId = "d", Title = "t", Source = "s" };
        c.Language.Should().Be("en");
        c.Summary.Should().Be("");
        c.Quantization.Should().Be("float16");
        c.EmbeddingDims.Should().Be(0);
        c.EmbeddingModel.Should().Be("");
        c.AccessRoles.Should().Equal("*");
        c.Version.Should().Be("1.0");
        c.Insights.Should().BeEmpty();
        c.KeyNumbers.Should().BeEmpty();
        c.SectionIndex.Should().BeEmpty();
    }

    [Fact]
    public void Section_defaults_match_python()
    {
        var s = new Section { Id = "§1", Title = "Intro", Level = 1, Text = "hello" };
        s.Tables.Should().BeEmpty();
        s.Images.Should().BeEmpty();
        s.Children.Should().BeEmpty();
        s.Keywords.Should().BeEmpty();
        s.ParentId.Should().BeNull();
        s.Summary.Should().BeNull();
        s.Embedding.Should().BeNull();
        s.TokenCount.Should().Be(0);
    }

    [Fact]
    public void Document_defaults_match_python()
    {
        var d = new Document { DocId = "d", Title = "t", Source = "s", Format = "md" };
        d.Sections.Should().BeEmpty();
        d.Insights.Should().BeEmpty();
        d.KeyNumbers.Should().BeEmpty();
        d.Summary.Should().BeNull();
        d.Meta.Should().NotBeNull();
        d.Meta.AccessRoles.Should().Equal("*");
    }

    [Fact]
    public void Collections_are_never_null_by_default()
    {
        new TableData { TableId = "t" }.Headers.Should().NotBeNull();
        new TableData { TableId = "t" }.Rows.Should().NotBeNull();
        new RawDocument { DocId = "d", Title = "t", Source = "s", Format = "md" }.Sections.Should().NotBeNull();
    }
}
