using System.Text.RegularExpressions;

namespace DocNest.Retrieval;

/// <summary>Query/section tokenisation for retrieval. Ports the Python <c>_tokenise</c>/<c>_query_tokens</c>.</summary>
internal static partial class RetrievalTokenizer
{
    private static readonly HashSet<string> StopWords = new(StringComparer.Ordinal)
    {
        "what", "which", "how", "the", "a", "an", "is", "are", "was", "were", "does", "did", "do",
        "in", "on", "at", "to", "for", "of", "and", "or", "by", "with", "from", "this", "that",
        "these", "those", "it", "its", "be", "been", "being", "have", "has", "had", "will", "would",
        "could", "should", "may", "might", "must", "shall", "can", "cannot", "dont", "doesnt",
        "report", "describe", "say", "says", "said", "describes", "according", "year", "annual", "global",
    };

    [GeneratedRegex(@"[^a-z0-9\-]")]
    private static partial Regex NonTokenRe();

    public static List<string> Tokenize(string text)
    {
        var clean = NonTokenRe().Replace((text ?? string.Empty).ToLowerInvariant(), " ");
        return clean.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 1)
            .ToList();
    }

    /// <summary>Returns (all tokens, keyword tokens) — keywords drop stop-words and length ≤ 2.</summary>
    public static (List<string> Full, List<string> Keywords) QueryTokens(string question)
    {
        var full = Tokenize(question);
        var keywords = full.Where(w => !StopWords.Contains(w) && w.Length > 2).ToList();
        return (full, keywords);
    }
}
