using System.Text;

namespace DocNest.Embeddings;

/// <summary>
/// Minimal BERT (uncased) WordPiece tokenizer: basic tokenization (lowercase, whitespace + punctuation
/// split) then greedy longest-match subwording against a <c>vocab.txt</c>. Zero-dependency so the
/// embeddings assembly carries only ONNX Runtime. Adds <c>[CLS]</c>/<c>[SEP]</c> and truncates.
/// </summary>
internal sealed class WordPieceTokenizer
{
    private const int MaxCharsPerWord = 200;
    private readonly Dictionary<string, long> _vocab;

    public long ClsId { get; }
    public long SepId { get; }
    public long PadId { get; }
    public long UnkId { get; }

    public WordPieceTokenizer(IEnumerable<string> vocabLines)
    {
        _vocab = new Dictionary<string, long>(StringComparer.Ordinal);
        long index = 0;
        foreach (var line in vocabLines)
        {
            _vocab[line.Trim()] = index;
            index++;
        }
        ClsId = _vocab.GetValueOrDefault("[CLS]");
        SepId = _vocab.GetValueOrDefault("[SEP]");
        PadId = _vocab.GetValueOrDefault("[PAD]");
        UnkId = _vocab.GetValueOrDefault("[UNK]");
    }

    public static WordPieceTokenizer FromVocabFile(string path) => new(File.ReadLines(path));

    /// <summary>Encode text to token ids: <c>[CLS] … [SEP]</c>, truncated to <paramref name="maxLength"/>.</summary>
    public List<long> Encode(string text, int maxLength)
    {
        var ids = new List<long> { ClsId };
        foreach (var token in BasicTokenize(text ?? string.Empty))
        {
            foreach (var pieceId in WordPiece(token))
            {
                if (ids.Count >= maxLength - 1)
                {
                    break;
                }
                ids.Add(pieceId);
            }
        }
        ids.Add(SepId);
        if (ids.Count > maxLength)
        {
            ids = ids.GetRange(0, maxLength);
        }
        return ids;
    }

    private static IEnumerable<string> BasicTokenize(string text)
    {
        var tokens = new List<string>();
        var current = new StringBuilder();

        void Flush()
        {
            if (current.Length > 0)
            {
                tokens.Add(current.ToString());
                current.Clear();
            }
        }

        foreach (var ch in text.ToLowerInvariant())
        {
            if (char.IsWhiteSpace(ch))
            {
                Flush();
            }
            else if (char.IsLetterOrDigit(ch))
            {
                current.Append(ch);
            }
            else
            {
                Flush();
                tokens.Add(ch.ToString());
            }
        }
        Flush();
        return tokens;
    }

    private List<long> WordPiece(string token)
    {
        if (token.Length > MaxCharsPerWord)
        {
            return new List<long> { UnkId };
        }

        var pieces = new List<long>();
        var start = 0;
        while (start < token.Length)
        {
            var end = token.Length;
            long? matched = null;
            while (start < end)
            {
                var piece = (start > 0 ? "##" : string.Empty) + token[start..end];
                if (_vocab.TryGetValue(piece, out var id))
                {
                    matched = id;
                    break;
                }
                end--;
            }
            if (matched is null)
            {
                return new List<long> { UnkId };
            }
            pieces.Add(matched.Value);
            start = end;
        }
        return pieces;
    }
}
