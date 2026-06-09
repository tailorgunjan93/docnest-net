using System;
using System.Collections.Generic;
using DocNest;
using FluentAssertions;
using Xunit;

namespace DocNest.Tests;

/// <summary>U5/U6 — every specific exception is a DocNestException and preserves message + inner.</summary>
public class ExceptionHierarchyTests
{
    public static IEnumerable<object[]> AllSpecificExceptions() => new[]
    {
        new object[] { new ParseException("x") },
        new object[] { new UnsupportedFormatException("x") },
        new object[] { new EmbedException("x") },
        new object[] { new IntelligenceException("x") },
        new object[] { new UdfWriteException("x") },
        new object[] { new UdfReadException("x") },
        new object[] { new SizeLimitException("x") },
        new object[] { new ConnectorException("x") },
        new object[] { new QuantizationException("x") },
    };

    [Theory]
    [MemberData(nameof(AllSpecificExceptions))]
    public void Specific_exceptions_are_DocNestException(DocNestException ex)
        => ex.Should().BeAssignableTo<DocNestException>();

    [Fact]
    public void Exception_preserves_message_and_inner()
    {
        var inner = new InvalidOperationException("boom");
        var ex = new ParseException("failed", inner);
        ex.Message.Should().Be("failed");
        ex.InnerException.Should().BeSameAs(inner);
    }

    [Fact]
    public void Base_DocNestException_catches_any_specific()
    {
        Action act = () => throw new EmbedException("e");
        act.Should().Throw<DocNestException>();
    }
}
