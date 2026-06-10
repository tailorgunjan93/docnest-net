using System.Text.RegularExpressions;

namespace DocNest.Query;

/// <summary>
/// Layer-1 extractive answering: returns the question-relevant sentence(s) from a section (0 tokens).
/// Returns empty when there is no token overlap (never fabricates). Ports the Python <c>_best_sentences</c>.
/// </summary>
internal static partial class Extractive
{
    [GeneratedRegex(@"(?<=[.!?])\s+")]
    private static partial Regex SentenceSplitRe();

    [GeneratedRegex("[a-z0-9]+")]
    private static partial Regex TokenRe();

    public static string BestSentences(string text, string question, int n = 2)
    {
        var sentences = SentenceSplitRe().Split(text ?? string.Empty)
            .Select(s => s.Trim())
            .Where(s => s.Length > 15)
            .ToList();
        if (sentences.Count == 0)
        {
            return string.Empty;
        }

        var questionTokens = TokenRe().Matches(question.ToLowerInvariant())
            .Select(m => m.Value)
            .Where(t => t.Length > 2 && !QueryConstants.Fillers.Contains(t))
            .ToHashSet();
        if (questionTokens.Count == 0)
        {
            return string.Empty;
        }

        int Score(string sentence)
            => TokenRe().Matches(sentence.ToLowerInvariant()).Count(m => questionTokens.Contains(m.Value));

        var order = Enumerable.Range(0, sentences.Count).OrderByDescending(i => Score(sentences[i])).ToList();
        if (Score(sentences[order[0]]) == 0)
        {
            return string.Empty;
        }

        var keep = order.Take(n).OrderBy(i => i).ToList();
        return string.Join(" ", keep.Select(i => sentences[i]));
    }
}
