using System;
using System.Buffers.Binary;
using DocNest;
using DocNest.Quantization;
using FluentAssertions;
using Xunit;

namespace DocNest.Tests.Quantization;

public class QuantizerTests
{
    private static readonly float[] Sample = { 0.5f, -0.25f, 0.0f, 1.0f, -1.0f, 0.125f };

    [Fact]
    public void Float32_round_trips_exactly()
    {
        var q = new Quantizer("float32");
        q.Dequantize(q.Quantize(Sample), Sample.Length).Should().Equal(Sample);
    }

    [Fact]
    public void Float16_round_trips_within_tolerance()
    {
        var q = new Quantizer("float16");
        var back = q.Dequantize(q.Quantize(Sample), Sample.Length);
        for (var i = 0; i < Sample.Length; i++)
        {
            back[i].Should().BeApproximately(Sample[i], 1e-3f);
        }
    }

    [Fact]
    public void Int8_round_trips_within_one_percent()
    {
        var q = new Quantizer("int8");
        var back = q.Dequantize(q.Quantize(Sample), Sample.Length);
        for (var i = 0; i < Sample.Length; i++)
        {
            back[i].Should().BeApproximately(Sample[i], 0.02f); // max=1.0 → step ≈ 1/127
        }
    }

    [Fact]
    public void Binary_produces_a_sign_vector()
    {
        var q = new Quantizer("binary");
        var back = q.Dequantize(q.Quantize(Sample), Sample.Length);
        // 0.5>0 → +1; -0.25 → -1; 0.0 not >0 → -1; 1.0 → +1; -1.0 → -1; 0.125 → +1
        back.Should().Equal(1f, -1f, -1f, 1f, -1f, 1f);
    }

    [Theory]
    [InlineData("float32", 4)]
    [InlineData("float16", 2)]
    [InlineData("int8", 1)]
    [InlineData("binary", 0)]
    public void BytesPerElement_matches_python(string mode, int expected)
        => new Quantizer(mode).BytesPerElement.Should().Be(expected);

    [Theory]
    [InlineData("float32", 384, 1536)]
    [InlineData("float16", 384, 768)]
    [InlineData("int8", 384, 384)]
    [InlineData("binary", 384, 48)]
    [InlineData("binary", 10, 2)] // ceil(10/8)
    public void Stride_matches_python(string mode, int dims, int expected)
        => new Quantizer(mode).Stride(dims).Should().Be(expected);

    [Fact]
    public void Float16_bytes_are_ieee_half_little_endian()
    {
        var q = new Quantizer("float16");
        var bytes = q.Quantize(new[] { 1.5f });
        var expected = new byte[2];
        BinaryPrimitives.WriteHalfLittleEndian(expected, (Half)1.5f);
        bytes.Should().Equal(expected);
    }

    [Fact]
    public void Unsupported_mode_throws()
    {
        var act = () => new Quantizer("float64");
        act.Should().Throw<QuantizationException>();
    }

    [Fact]
    public void CosineSimilarity_matches_definition_and_handles_zero()
    {
        Quantizer.CosineSimilarity(new[] { 1f, 0f }, new[] { 1f, 0f }).Should().BeApproximately(1.0, 1e-9);
        Quantizer.CosineSimilarity(new[] { 1f, 0f }, new[] { 0f, 1f }).Should().BeApproximately(0.0, 1e-9);
        Quantizer.CosineSimilarity(new[] { 0f, 0f }, new[] { 1f, 1f }).Should().Be(0.0);
    }
}
