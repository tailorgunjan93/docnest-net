using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace DocNest.Embeddings;

/// <summary>
/// Local cross-encoder reranker via ONNX Runtime running ms-marco-MiniLM-L-6-v2 (no Python, no cloud).
/// Tokenizes each (query, passage) pair with WordPiece (<c>[CLS] q [SEP] p [SEP]</c> + segment ids), runs
/// inference, and reads the single relevance logit per pair (higher = more relevant). ONNX Runtime is kept
/// behind this <see cref="IReranker"/> wrapper (ADR-0013).
/// </summary>
public sealed class OnnxCrossEncoderReranker : IReranker, IDisposable
{
    private readonly InferenceSession _session;
    private readonly WordPieceTokenizer _tokenizer;
    private readonly int _maxLength;
    private readonly int _batchSize;
    private readonly bool _hasTokenTypeIds;
    private readonly string _outputName;

    /// <inheritdoc/>
    public string ModelName => "cross-encoder/ms-marco-MiniLM-L-6-v2";

    /// <summary>Create a reranker from a cross-encoder ONNX model and its WordPiece <c>vocab.txt</c>.</summary>
    public OnnxCrossEncoderReranker(string modelPath, string vocabPath, int maxLength = 320, int batchSize = 16)
    {
        ArgumentException.ThrowIfNullOrEmpty(modelPath);
        ArgumentException.ThrowIfNullOrEmpty(vocabPath);
        _session = new InferenceSession(modelPath);
        _tokenizer = WordPieceTokenizer.FromVocabFile(vocabPath);
        _maxLength = maxLength;
        _batchSize = batchSize;
        _hasTokenTypeIds = _session.InputMetadata.ContainsKey("token_type_ids");
        _outputName = _session.OutputMetadata.Keys.First();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<double>> ScoreAsync(string query, IReadOnlyList<string> passages, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(passages);
        if (passages.Count == 0)
        {
            return Array.Empty<double>();
        }
        var result = new List<double>(passages.Count);
        for (var offset = 0; offset < passages.Count; offset += _batchSize)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var batch = passages.Skip(offset).Take(_batchSize).ToList();
            var scores = await Task.Run(() => ScoreBatch(query, batch), cancellationToken).ConfigureAwait(false);
            result.AddRange(scores);
        }
        return result;
    }

    private List<double> ScoreBatch(string query, List<string> passages)
    {
        var batch = passages.Count;
        var encoded = passages.Select(p => _tokenizer.EncodePair(query, p, _maxLength)).ToList();
        var seqLength = Math.Max(1, encoded.Max(e => e.Ids.Count));

        var ids = new DenseTensor<long>(new[] { batch, seqLength });
        var mask = new DenseTensor<long>(new[] { batch, seqLength });
        var types = new DenseTensor<long>(new[] { batch, seqLength });
        for (var i = 0; i < batch; i++)
        {
            var (eids, etypes) = encoded[i];
            for (var j = 0; j < seqLength; j++)
            {
                if (j < eids.Count)
                {
                    ids[i, j] = eids[j];
                    mask[i, j] = 1;
                    types[i, j] = etypes[j];
                }
                else
                {
                    ids[i, j] = _tokenizer.PadId;
                    mask[i, j] = 0;
                    types[i, j] = 0;
                }
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
        var logits = results.First(r => r.Name == _outputName).AsTensor<float>();
        var rank = logits.Dimensions.Length;

        var scores = new List<double>(batch);
        for (var i = 0; i < batch; i++)
        {
            // ms-marco-MiniLM emits one relevance logit per pair: shape [batch, 1] (or [batch]).
            scores.Add(rank >= 2 ? logits[i, 0] : logits[i]);
        }
        return scores;
    }

    /// <summary>Disposes the underlying ONNX inference session.</summary>
    public void Dispose() => _session.Dispose();
}
