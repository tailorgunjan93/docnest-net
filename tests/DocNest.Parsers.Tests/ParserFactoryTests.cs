using DocNest;
using FluentAssertions;
using Xunit;

namespace DocNest.Parsers.Tests;

public class ParserFactoryTests
{
    [Fact]
    public void Routes_by_extension()
    {
        var factory = new ParserFactory();
        factory.Get("a.md").Should().BeOfType<MarkdownParser>();
        factory.Get("a.csv").Should().BeOfType<CsvParser>();
        factory.Get("a.html").Should().BeOfType<HtmlParser>();
    }

    [Fact]
    public void Unknown_extension_throws()
    {
        var act = () => new ParserFactory().Get("a.xyz");
        act.Should().Throw<UnsupportedFormatException>().WithMessage("*xyz*");
    }

    [Fact]
    public void Supports_reflects_registry()
    {
        var factory = new ParserFactory();
        factory.Supports("a.md").Should().BeTrue();
        factory.Supports("a.xyz").Should().BeFalse();
    }

    [Fact]
    public void Register_and_unregister()
    {
        var factory = new ParserFactory();
        factory.Unregister<MarkdownParser>();
        factory.Supports("a.md").Should().BeFalse();

        factory.Register(new MarkdownParser());
        factory.Supports("a.md").Should().BeTrue();
    }
}
