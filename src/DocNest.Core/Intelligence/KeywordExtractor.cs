using System.Text.RegularExpressions;

namespace DocNest.Intelligence;

/// <summary>
/// Deterministic keyword extraction — no LLM. Ports the Python <c>keywords.py</c>: salient terms by
/// frequency × a small length (specificity) bonus over non-stopwords, with title terms prioritised.
/// Feeds the BM25/keyword retrieval index.
/// </summary>
public static partial class KeywordExtractor
{
    private static readonly HashSet<string> Stop = new(StringComparer.Ordinal)
    {
        "the", "a", "an", "of", "to", "from", "in", "on", "at", "by", "for", "and", "or", "is",
        "are", "was", "were", "be", "been", "being", "this", "that", "these", "those", "it", "its",
        "with", "as", "it's", "we", "our", "their", "they", "them", "than", "into", "about",
        "above", "below", "report", "say", "says", "will", "would", "can", "could", "should",
        "may", "might", "must", "have", "has", "had", "do", "does", "did", "not", "no", "so",
        "if", "then", "else", "when", "where", "which", "who", "whom", "how", "what", "why",
        "all", "any", "each", "per", "via", "using", "used", "use", "new", "now", "also", "more",
        "most", "other", "some", "such", "only", "over", "under", "between", "within",
    };

    [GeneratedRegex(@"[a-z0-9][a-z0-9\-]{2,}")]
    private static partial Regex TokenRe();

    /// <summary>Up to <paramref name="k"/> salient lowercase keywords (title terms first, then frequent terms).</summary>
    public static IReadOnlyList<string> Extract(string text, string title = "", int k = 8)
    {
        var titleTerms = TokenRe().Matches((title ?? string.Empty).ToLowerInvariant())
            .Select(m => m.Value)
            .Where(t => !Stop.Contains(t))
            .ToList();

        var tokens = TokenRe().Matches((text ?? string.Empty).ToLowerInvariant())
            .Select(m => m.Value)
            .Where(t => !Stop.Contains(t))
            .ToList();

        var freq = new Dictionary<string, int>(StringComparer.Ordinal);
        var firstSeen = new List<string>();
        foreach (var token in tokens)
        {
            if (!freq.TryGetValue(token, out var count))
            {
                firstSeen.Add(token);
                count = 0;
            }
            freq[token] = count + 1;
        }

        // Stable order-by preserves first-seen order for equal scores (matches Python's stable sort).
        var scored = firstSeen.OrderByDescending(w => freq[w] + 0.1 * w.Length);

        var result = new List<string>();
        foreach (var word in titleTerms.Concat(scored))
        {
            if (!result.Contains(word))
            {
                result.Add(word);
            }
            if (result.Count >= k)
            {
                break;
            }
        }

        return result;
    }

    /// <summary>Populate each section's <see cref="Section.Keywords"/> (per-section no-op if already set).</summary>
    public static Document Enrich(Document doc, int k = 8)
    {
        ArgumentNullException.ThrowIfNull(doc);

        var changed = false;
        var sections = new List<Section>(doc.Sections.Count);
        foreach (var section in doc.Sections)
        {
            if (section.Keywords.Count > 0)
            {
                sections.Add(section);
                continue;
            }
            sections.Add(section with { Keywords = Extract(section.Text, section.Title, k) });
            changed = true;
        }

        return changed ? doc with { Sections = sections } : doc;
    }
}
