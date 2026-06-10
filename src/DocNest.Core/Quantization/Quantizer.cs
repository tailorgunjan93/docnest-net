using System.Buffers.Binary;

namespace DocNest.Quantization;

/// <summary>
/// Compresses/decompresses float32 embedding vectors for <c>.udf</c> storage. Ports the Python
/// <c>Quantizer</c> byte-for-byte (the <c>embeddings.bin</c> cross-ecosystem contract):
/// <c>float32</c> raw LE, <c>float16</c> IEEE-half LE, <c>int8</c> scaled+truncated, <c>binary</c>
/// sign bit packed MSB-first (numpy <c>packbits</c>).
/// </summary>
public sealed class Quantizer
{
    /// <summary>The supported quantization modes.</summary>
    public static readonly IReadOnlyList<string> SupportedModes = new[] { "float32", "float16", "int8", "binary" };

    /// <summary>The active quantization mode.</summary>
    public string Mode { get; }

    /// <summary>Create a quantizer for the given mode (default <c>float16</c>).</summary>
    /// <exception cref="QuantizationException">If the mode is not supported.</exception>
    public Quantizer(string mode = "float16")
    {
        if (!SupportedModes.Contains(mode))
        {
            throw new QuantizationException(
                $"Unsupported quantization mode '{mode}'. Choose from: {string.Join(", ", SupportedModes)}");
        }
        Mode = mode;
    }

    /// <summary>Compress a float32 vector to bytes (length = <see cref="Stride"/>).</summary>
    /// <exception cref="QuantizationException">If compression fails.</exception>
    public byte[] Quantize(float[] vector)
    {
        ArgumentNullException.ThrowIfNull(vector);
        try
        {
            switch (Mode)
            {
                case "float32":
                    {
                        var bytes = new byte[vector.Length * 4];
                        Buffer.BlockCopy(vector, 0, bytes, 0, bytes.Length);
                        return bytes;
                    }
                case "float16":
                    {
                        var bytes = new byte[vector.Length * 2];
                        for (var i = 0; i < vector.Length; i++)
                        {
                            BinaryPrimitives.WriteHalfLittleEndian(bytes.AsSpan(i * 2, 2), (Half)vector[i]);
                        }
                        return bytes;
                    }
                case "int8":
                    {
                        var absMax = 0.0f;
                        foreach (var v in vector)
                        {
                            absMax = Math.Max(absMax, Math.Abs(v));
                        }
                        var scale = 127.0f / (absMax + 1e-8f);
                        var bytes = new byte[vector.Length];
                        for (var i = 0; i < vector.Length; i++)
                        {
                            var scaled = Math.Clamp(vector[i] * scale, -127.0f, 127.0f);
                            bytes[i] = (byte)(sbyte)(int)scaled; // truncate toward zero (numpy astype(int8))
                        }
                        return bytes;
                    }
                case "binary":
                    {
                        var bytes = new byte[(vector.Length + 7) / 8];
                        for (var i = 0; i < vector.Length; i++)
                        {
                            if (vector[i] > 0)
                            {
                                bytes[i / 8] |= (byte)(1 << (7 - (i % 8))); // MSB-first (numpy packbits)
                            }
                        }
                        return bytes;
                    }
                default:
                    throw new QuantizationException($"Unknown mode: {Mode}");
            }
        }
        catch (Exception ex) when (ex is not QuantizationException)
        {
            throw new QuantizationException($"Quantization ({Mode}) failed: {ex.Message}", ex);
        }
    }

    /// <summary>Decompress bytes back to an approximate float32 vector of <paramref name="dims"/> values.</summary>
    /// <exception cref="QuantizationException">If decompression fails.</exception>
    public float[] Dequantize(byte[] data, int dims)
    {
        ArgumentNullException.ThrowIfNull(data);
        try
        {
            switch (Mode)
            {
                case "float32":
                    {
                        var vector = new float[data.Length / 4];
                        Buffer.BlockCopy(data, 0, vector, 0, vector.Length * 4);
                        return vector;
                    }
                case "float16":
                    {
                        var vector = new float[data.Length / 2];
                        for (var i = 0; i < vector.Length; i++)
                        {
                            vector[i] = (float)BinaryPrimitives.ReadHalfLittleEndian(data.AsSpan(i * 2, 2));
                        }
                        return vector;
                    }
                case "int8":
                    {
                        var vector = new float[data.Length];
                        for (var i = 0; i < data.Length; i++)
                        {
                            vector[i] = (sbyte)data[i] / 127.0f;
                        }
                        return vector;
                    }
                case "binary":
                    {
                        var vector = new float[dims];
                        for (var i = 0; i < dims; i++)
                        {
                            var bit = (data[i / 8] >> (7 - (i % 8))) & 1;
                            vector[i] = (bit * 2.0f) - 1.0f;
                        }
                        return vector;
                    }
                default:
                    throw new QuantizationException($"Unknown mode: {Mode}");
            }
        }
        catch (Exception ex) when (ex is not QuantizationException)
        {
            throw new QuantizationException($"Dequantization ({Mode}) failed: {ex.Message}", ex);
        }
    }

    /// <summary>Total bytes for one embedding vector of <paramref name="dims"/> dimensions in this mode.</summary>
    public int Stride(int dims) => Mode == "binary" ? (dims + 7) / 8 : dims * BytesPerElement;

    /// <summary>Bytes per dimension (binary returns 0 — use <see cref="Stride"/> instead).</summary>
    public int BytesPerElement => Mode switch
    {
        "float32" => 4,
        "float16" => 2,
        "int8" => 1,
        _ => 0,
    };

    /// <summary>Cosine similarity between two float32 vectors (0 if either is a zero vector).</summary>
    public static double CosineSimilarity(float[] a, float[] b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        var n = Math.Min(a.Length, b.Length);
        double dot = 0, normA = 0, normB = 0;
        for (var i = 0; i < n; i++)
        {
            dot += (double)a[i] * b[i];
            normA += (double)a[i] * a[i];
            normB += (double)b[i] * b[i];
        }
        normA = Math.Sqrt(normA);
        normB = Math.Sqrt(normB);
        return normA == 0.0 || normB == 0.0 ? 0.0 : dot / (normA * normB);
    }
}
