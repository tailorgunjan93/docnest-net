using System.Text.RegularExpressions;

namespace DocNest.Query;

/// <summary>
/// Absolute, bounded [0,1] confidence that the top retrieved section actually answers the question:
/// the fraction of the question's content tokens that appear in the section's title + keywords + text
/// (query-term recall). Used <b>only</b> for the Layer-1/2 escalation decision — the RRF retriever still
/// owns ranking. This restores the Python reader's confidence-gated escalation on a scale-stable signal
/// (RRF scores are a rank-fusion value, not an absolute confidence). See ADR-0011.
/// </summary>
internal static partial class Confidence
{
    [GeneratedRegex("[a-z0-9]+")]
    private static partial Regex TokenRe();

    /// <summary>Query-term recall of <paramref name="question"/> against <paramref name="section"/>, in [0,1].</summary>
    public static double Of(string question, Section section)
    {
        var queryTokens = TokenRe().Matches(question.ToLowerInvariant())
            .Select(m => m.Value)
            .Where(t => t.Length > 2 && !QueryConstants.Fillers.Contains(t))
            .ToHashSet(StringComparer.Ordinal);
        if (queryTokens.Count == 0)
        {
            return 0.0;
        }

        var sectionTokens = new HashSet<string>(StringComparer.Ordinal);
        AddTokens(sectionTokens, section.Title);
        foreach (var keyword in section.Keywords)
        {
            AddTokens(sectionTokens, keyword);
        }
        AddTokens(sectionTokens, section.Text);

        var hits = queryTokens.Count(sectionTokens.Contains);
        return hits / (double)queryTokens.Count;
    }

    private static void AddTokens(HashSet<string> set, string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }
        foreach (Match match in TokenRe().Matches(text.ToLowerInvariant()))
        {
            if (match.Value.Length > 2)
            {
                set.Add(match.Value);
            }
        }
    }
}
