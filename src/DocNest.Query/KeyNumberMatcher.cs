using System.Text.RegularExpressions;

namespace DocNest.Query;

/// <summary>
/// Layer-0 key-number lookup: returns the <see cref="KeyNumber"/> whose core label tokens are all present
/// in the question (word-order and modifier tolerant), skipping ambiguous matches (different values for the
/// same most-specific label). Ports the Python <c>_match_key_number</c>/<c>_kn_tokens</c>.
/// </summary>
internal static partial class KeyNumberMatcher
{
    [GeneratedRegex("[a-z0-9]+")]
    private static partial Regex TokenRe();

    public static KeyNumber? Match(string question, IReadOnlyList<KeyNumber> keyNumbers)
    {
        var questionTokens = KnTokens(question).ToHashSet();
        if (questionTokens.Count == 0)
        {
            return null;
        }

        var candidates = new List<(int Specificity, KeyNumber KeyNumber)>();
        foreach (var keyNumber in keyNumbers)
        {
            var labelTokens = KnTokens(keyNumber.Label).Where(t => !QueryConstants.Fillers.Contains(t)).ToList();
            var core = labelTokens.Where(t => !QueryConstants.Soft.Contains(t)).ToList();
            if (core.Count == 0)
            {
                core = labelTokens;
            }
            if (core.Count > 0 && core.All(questionTokens.Contains))
            {
                candidates.Add((core.Count, keyNumber));
            }
        }

        if (candidates.Count == 0)
        {
            return null;
        }

        var best = candidates.Max(c => c.Specificity);
        var top = candidates.Where(c => c.Specificity == best).Select(c => c.KeyNumber).ToList();
        if (top.Select(k => k.Value).Distinct(StringComparer.Ordinal).Count() > 1)
        {
            return null; // ambiguous — don't guess
        }
        return top[0];
    }

    private static IEnumerable<string> KnTokens(string text)
    {
        foreach (Match match in TokenRe().Matches(text.ToLowerInvariant()))
        {
            var token = match.Value;
            if (token.Length > 3 && token.EndsWith('s') && !token.EndsWith("ss", StringComparison.Ordinal))
            {
                token = token[..^1];
            }
            yield return token;
        }
    }
}
