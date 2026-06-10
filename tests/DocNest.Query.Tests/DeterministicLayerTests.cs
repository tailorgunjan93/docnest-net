using System.Collections.Generic;
using DocNest;
using DocNest.Query;
using FluentAssertions;
using Xunit;

namespace DocNest.Query.Tests;

public class DeterministicLayerTests
{
    [Fact]
    public void KeyNumberMatcher_matches_core_label_tokens()
    {
        var kns = new[]
        {
            new KeyNumber { Label = "Average response time", Value = "142ms", Section = "§1" },
            new KeyNumber { Label = "Uptime", Value = "99.9%", Section = "§2" },
        };
        KeyNumberMatcher.Match("what is the uptime?", kns)!.Value.Should().Be("99.9%");
        KeyNumberMatcher.Match("what was the response time?", kns)!.Value.Should().Be("142ms"); // 'average' is soft
    }

    [Fact]
    public void KeyNumberMatcher_skips_ambiguous_matches()
    {
        var kns = new[]
        {
            new KeyNumber { Label = "Revenue", Value = "$100M", Section = "§1" },
            new KeyNumber { Label = "Revenue", Value = "$200M", Section = "§2" },
        };
        KeyNumberMatcher.Match("what is revenue?", kns).Should().BeNull();
    }

    [Fact]
    public void Extractive_returns_relevant_sentence_or_empty()
    {
        const string text = "The sky is blue today. Revenue grew strongly to a record high this quarter. Lunch was nice.";
        Extractive.BestSentences(text, "how did revenue grow this quarter").Should().Contain("Revenue grew strongly");
        Extractive.BestSentences(text, "xyzzy plugh frobnicate").Should().BeEmpty(); // no overlap → no fabrication
    }
}
