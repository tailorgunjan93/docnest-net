using System.Globalization;
using System.Text.RegularExpressions;

namespace DocNest.Eval;

/// <summary>
/// Zero-API answer judge ported from the Python eval's <c>_local_judge</c>: scores a candidate answer
/// 0–10 against a ground-truth reference using number overlap (±6 %), keyword overlap, and phrase
/// overlap, with fast-paths. A score ≥ 7 counts as a "hit".
/// </summary>
internal static partial class LocalJudge
{
    private static readonly HashSet<string> StopWords = new(StringComparer.Ordinal)
    {
        "what", "which", "how", "the", "a", "an", "is", "are", "was", "were", "does", "did", "do",
        "in", "on", "at", "to", "for", "of", "and", "or", "by", "with", "from", "this", "that",
        "these", "those", "it", "its", "be", "been", "being", "have", "has", "had", "will", "would",
        "could", "should", "may", "might", "must", "shall", "can", "cannot", "dont", "doesnt",
        "report", "describe", "say", "says", "said", "describes", "according", "year", "annual", "global",
    };

    public static (int Score, string Reason) Score(string question, string candidate, string reference)
    {
        var cand = (candidate ?? string.Empty).ToLowerInvariant().Trim();
        var reff = (reference ?? string.Empty).ToLowerInvariant().Trim();

        if (cand.StartsWith("not found in context", StringComparison.Ordinal) ||
            cand.StartsWith("not found in the context", StringComparison.Ordinal) ||
            cand.Length == 0)
        {
            return (0, "retrieval-miss");
        }

        // 1. Number match (±6 %)
        var refForNums = SectionLocatorRe().Replace(reff, " ");
        var refNums = Numbers(refForNums);
        var candNums = Numbers(cand);
        var numHits = refNums.Count(rn => candNums.Any(cn => Close(rn, cn)));
        var recall = numHits / (double)Math.Max(refNums.Count, 1);

        var resultMatch = 0.0;
        var mRes = ResultRe().Match(reff);
        if (mRes.Success && candNums.Any(cn => Close(Denorm(mRes.Groups[1].Value), cn)))
        {
            resultMatch = 1.0;
        }
        var numRatio = Math.Max(recall, resultMatch);

        // 2. Keyword overlap
        var refKws = Tokens(reff).Where(w => !StopWords.Contains(w) && w.Length > 2).ToHashSet();
        var candWords = Tokens(cand).ToHashSet();
        var kwRatio = refKws.Count(candWords.Contains) / (double)Math.Max(refKws.Count, 1);

        // 3. Short-phrase overlap
        var refPhrases = PhraseSplitRe().Split(reff)
            .Select(p => p.Trim())
            .Where(p => p.Length is > 4 and < 60)
            .ToList();
        var phraseHits = refPhrases.Count(p => cand.Contains(p, StringComparison.Ordinal));
        var phraseRatio = phraseHits / (double)Math.Max(refPhrases.Count, 1);

        if (numRatio >= 0.75 && kwRatio >= 0.45)
        {
            return (10, $"fast✓ num={numRatio:F2} kw={kwRatio:F2}");
        }
        if (refNums.Count == 0 && kwRatio >= 0.60)
        {
            return (10, $"text✓ kw={kwRatio:F2}");
        }
        if (refNums.Count == 0 && kwRatio >= 0.40)
        {
            return (9, $"text~ kw={kwRatio:F2}");
        }

        var combined = (0.50 * numRatio) + (0.30 * kwRatio) + (0.20 * phraseRatio);
        var score = combined switch
        {
            >= 0.70 => 10,
            >= 0.55 => 9,
            >= 0.40 => 8,
            >= 0.28 => 7,
            >= 0.18 => 6,
            >= 0.10 => 5,
            >= 0.04 => 3,
            _ => 0,
        };
        return (score, $"num={numRatio:F2} kw={kwRatio:F2} phrase={phraseRatio:F2}");
    }

    private static string Denorm(string text)
    {
        text = text.Replace(",", string.Empty, StringComparison.Ordinal);
        return ThousandsSpaceRe().Replace(text, string.Empty);
    }

    private static List<string> Numbers(string text)
        => NumberRe().Matches(Denorm(text)).Select(m => m.Value).ToList();

    private static bool Close(string a, string b)
    {
        if (double.TryParse(a, NumberStyles.Any, CultureInfo.InvariantCulture, out var va) &&
            double.TryParse(b, NumberStyles.Any, CultureInfo.InvariantCulture, out var vb))
        {
            return Math.Abs(va - vb) / Math.Max(Math.Abs(va), 0.001) < 0.06;
        }
        return a == b;
    }

    private static IEnumerable<string> Tokens(string text)
        => NonAlnumRe().Replace(text, " ").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

    [GeneratedRegex(@"§\s*[\d\.]+|\bsection\s+[\d\.]+")]
    private static partial Regex SectionLocatorRe();

    [GeneratedRegex(@"\b\d[\d\.]*")]
    private static partial Regex NumberRe();

    [GeneratedRegex(@"=\s*([\d][\d, ]*\d|\d)")]
    private static partial Regex ResultRe();

    [GeneratedRegex(@"(?<=\d)\s+(?=\d{3}\b)")]
    private static partial Regex ThousandsSpaceRe();

    [GeneratedRegex("[^a-z0-9]")]
    private static partial Regex NonAlnumRe();

    [GeneratedRegex(@"[;|:\-–]")]
    private static partial Regex PhraseSplitRe();
}
