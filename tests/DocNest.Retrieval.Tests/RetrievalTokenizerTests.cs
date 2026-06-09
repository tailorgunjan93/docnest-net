using DocNest.Retrieval;
using FluentAssertions;
using Xunit;

namespace DocNest.Retrieval.Tests;

public class RetrievalTokenizerTests
{
    [Fact]
    public void Tokenize_lowercases_strips_symbols_and_drops_single_chars()
    {
        RetrievalTokenizer.Tokenize("Carbon Budget: 2030 (x)!")
            .Should().Equal("carbon", "budget", "2030");
    }

    [Fact]
    public void QueryTokens_keywords_drop_stopwords_and_short_tokens()
    {
        var (full, keywords) = RetrievalTokenizer.QueryTokens("what is the carbon budget");
        full.Should().Contain("what").And.Contain("the");
        keywords.Should().Equal("carbon", "budget"); // "what"/"is"/"the" dropped
    }
}
