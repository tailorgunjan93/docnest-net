using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace DocNest.Embeddings;

/// <summary>
/// Local sentence embeddings via ONNX Runtime running all-MiniLM-L6-v2 (no Python, no cloud). Tokenizes
/// with WordPiece, runs inference, mean-pools the token embeddings with the attention mask, and
/// L2-normalises → 384-dim vectors. ONNX Runtime is kept behind this <see cref="IEmbedder"/> wrapper.
/// </summary>
public sealed class OnnxEmbedder : IEmbedder, IDisposable
{
    private readonly InferenceSession _session;
    private readonly WordPieceTokenizer _tokenizer;
    private readonly int _maxLength;
    private readonly int _batchSize;
    private readonly bool _hasTokenTypeIds;

    /// <inheritdoc/>
    public int Dims => 384;

    /// <inheritdoc/>
    public string ModelName => "sentence-transformers/all-MiniLM-L6-v2";

    /// <summary>Create an embedder from a MiniLM ONNX model and its WordPiece <c>vocab.txt</c>.</summary>
    public OnnxEmbedder(string modelPath, string vocabPath, int maxLength = 256, int batchSize = 32)
    {
        ArgumentException.ThrowIfNullOrEmpty(modelPath);
        ArgumentException.ThrowIfNullOrEmpty(vocabPath);
        _session = new InferenceSession(modelPath);
        _tokenizer = WordPieceTokenizer.FromVocabFile(vocabPath);
        _maxLength = maxLength;
        _batchSize = batchSize;
        _hasTokenTypeIds = _session.InputMetadata.ContainsKey("token_type_ids");
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(texts);
        var result = new List<float[]>(texts.Count);
        for (var offset = 0; offset < texts.Count; offset += _batchSize)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var batch = texts.Skip(offset).Take(_batchSize).ToList();
            var vectors = await Task.Run(() => EmbedBatch(batch), cancellationToken).ConfigureAwait(false);
            result.AddRange(vectors);
        }
        return result;
    }

    private List<float[]> EmbedBatch(List<string> texts)
    {
        var batch = texts.Count;
        var encoded = texts.Select(t => _tokenizer.Encode(t, _maxLength)).ToList();
        var seqLength = Math.Max(1, encoded.Max(e => e.Count));

        var ids = new DenseTensor<long>(new[] { batch, seqLength });
        var mask = new DenseTensor<long>(new[] { batch, seqLength });
        var types = new DenseTensor<long>(new[] { batch, seqLength });
        for (var i = 0; i < batch; i++)
        {
            for (var j = 0; j < seqLength; j++)
            {
                if (j < encoded[i].Count)
                {
                    ids[i, j] = encoded[i][j];
                    mask[i, j] = 1;
                }
                else
                {
                    ids[i, j] = _tokenizer.PadId;
                    mask[i, j] = 0;
                }
                types[i, j] = 0;
            }
        }

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input_ids", ids),
            NamedOnnxValue.CreateFromTensor("attention_mask", mask),
        };
        if (_hasTokenTypeIds)
        {
            inputs.Add(NamedOnnxValue.CreateFromTensor("token_type_ids", types));
        }

        using var results = _session.Run(inputs);
        var hiddenValue = results.FirstOrDefault(r => r.Name == "last_hidden_state") ?? results.First();
        var hidden = hiddenValue.AsTensor<float>(); // [batch, seq, 384]

        var output = new List<float[]>(batch);
        for (var i = 0; i < batch; i++)
        {
            var pooled = new float[Dims];
            var count = 0;
            for (var j = 0; j < seqLength; j++)
            {
                if (mask[i, j] == 0)
                {
                    continue;
                }
                count++;
                for (var d = 0; d < Dims; d++)
                {
                    pooled[d] += hidden[i, j, d];
                }
            }
            if (count > 0)
            {
                for (var d = 0; d < Dims; d++)
                {
                    pooled[d] /= count;
                }
            }

            var norm = 0.0;
            for (var d = 0; d < Dims; d++)
            {
                norm += (double)pooled[d] * pooled[d];
            }
            norm = Math.Sqrt(norm);
            if (norm > 1e-12)
            {
                for (var d = 0; d < Dims; d++)
                {
                    pooled[d] = (float)(pooled[d] / norm);
                }
            }
            output.Add(pooled);
        }
        return output;
    }

    /// <summary>Disposes the underlying ONNX inference session.</summary>
    public void Dispose() => _session.Dispose();
}
