using System.Globalization;
using System.Text.RegularExpressions;

namespace DocNest.Intelligence;

/// <summary>
/// Deterministic key-number extraction — no LLM. Ports the Python <c>key_numbers.py</c>: regex
/// figure detection (money / percent / duration / ratio / count), label binding, and noise filters
/// (ordered-list markers, bare years, identifiers/acronyms). Populates the 0-token Layer-0 path.
/// </summary>
public static partial class KeyNumberExtractor
{
    private static readonly HashSet<string> Fillers = new(StringComparer.Ordinal)
    {
        "the", "a", "an", "of", "to", "from", "and", "is", "was", "are", "now", "all", "with",
        "at", "by", "in", "on", "after", "down", "up", "for", "then", "it", "its", "this",
        "that", "we", "our",
    };

    // Ordered most-specific-first so "$18,400" isn't split into 18 and 400.
    [GeneratedRegex(@"(?<money>\$\s?\d[\d,]*(?:\.\d+)?\s?(?:million|billion|trillion|M|B|K|k)?)|(?<percent>\d+(?:\.\d+)?\s?%)|(?<duration>\d+(?:\.\d+)?\s?(?:ms|seconds?|secs?|minutes?|mins?|hours?|hrs?|days?|weeks?|months?|years?)\b)|(?<ratio>\d+(?:\.\d+)?\s?[x×]\b)|(?<count>\b\d[\d,]*(?:\.\d+)?\b)")]
    private static partial Regex NumberRe();

    [GeneratedRegex(@"^\s*\d+[.)]\s")]
    private static partial Regex ListMarkerRe();

    [GeneratedRegex(@"[A-Za-z]-?$")]
    private static partial Regex IdentifierPrefixRe();

    [GeneratedRegex(@"([A-Za-z][A-Za-z0-9 \-/&]{1,40}?)\s*:\s*\**\s*$")]
    private static partial Regex InlineLabelRe();

    [GeneratedRegex(@"[A-Za-z][A-Za-z\-/&]+")]
    private static partial Regex LabelWordRe();

    [GeneratedRegex(@"\S+")]
    private static partial Regex NonSpaceRe();

    [GeneratedRegex(@"^[A-Z]{2,}$")]
    private static partial Regex AcronymRe();

    [GeneratedRegex(@"-?\d+(?:\.\d+)?")]
    private static partial Regex SignedNumberRe();

    [GeneratedRegex(@"\btrillion\b")] private static partial Regex TrillionRe();
    [GeneratedRegex(@"\bbillion\b")] private static partial Regex BillionRe();
    [GeneratedRegex(@"\bmillion\b")] private static partial Regex MillionRe();
    [GeneratedRegex(@"\bthousand\b")] private static partial Regex ThousandRe();

    /// <summary>Extract labelled figures from a section's text. Deterministic; no LLM.</summary>
    public static IReadOnlyList<KeyNumber> Extract(string text, string sectionId)
    {
        var result = new List<KeyNumber>();
        var seen = new HashSet<(string Label, string Raw)>();

        foreach (var line in SplitLines(text))
        {
            var listMatch = ListMarkerRe().Match(line);
            var isListLine = listMatch.Success && listMatch.Index == 0;
            var listEnd = isListLine ? listMatch.Index + listMatch.Length : 0;

            foreach (Match m in NumberRe().Matches(line))
            {
                var raw = m.Value.Trim();
                var kind = KindOf(m);

                if (isListLine && m.Index < listEnd)
                {
                    continue;
                }

                var left = line[..m.Index];
                if (IdentifierPrefixRe().IsMatch(left) || AcronymPrefixed(left))
                {
                    continue;
                }

                var canon = ParseNumber(raw);
                if (kind == "count" && canon is >= 1900 and <= 2099 && !raw.Contains('.', StringComparison.Ordinal))
                {
                    continue;
                }

                var label = LabelFor(line, m.Index);
                if (string.IsNullOrEmpty(label))
                {
                    continue;
                }

                if (!seen.Add((label.ToLowerInvariant(), raw)))
                {
                    continue;
                }

                var unit = kind == "percent"
                    ? "%"
                    : raw.TrimStart().StartsWith('$') ? "USD" : null;
                result.Add(new KeyNumber { Label = label, Value = raw, Unit = unit, Section = sectionId });
            }
        }

        return result;
    }

    /// <summary>Populate <see cref="Document.KeyNumbers"/> deterministically (no-op if already set).</summary>
    public static Document Enrich(Document doc, int maxNumbers = 64)
    {
        ArgumentNullException.ThrowIfNull(doc);
        if (doc.KeyNumbers.Count > 0)
        {
            return doc;
        }

        var collected = new List<KeyNumber>();
        foreach (var section in doc.Sections)
        {
            collected.AddRange(Extract(section.Text, section.Id));
            if (collected.Count >= maxNumbers)
            {
                break;
            }
        }

        if (collected.Count > maxNumbers)
        {
            collected = collected.GetRange(0, maxNumbers);
        }

        return doc with { KeyNumbers = collected };
    }

    /// <summary>Canonical numeric value for a raw figure (strips $, %, commas; applies word/k multipliers).</summary>
    public static double? ParseNumber(string raw)
    {
        var s = raw.Trim().ToLowerInvariant();
        var mult = 1.0;
        foreach (var (regex, factor) in new[]
                 {
                     (TrillionRe(), 1e12), (BillionRe(), 1e9), (MillionRe(), 1e6), (ThousandRe(), 1e3),
                 })
        {
            if (regex.IsMatch(s))
            {
                mult = factor;
                s = regex.Replace(s, " ");
                break;
            }
        }

        var cleaned = s.Replace(",", "", StringComparison.Ordinal);
        var numMatch = SignedNumberRe().Match(cleaned);
        if (!numMatch.Success)
        {
            return null;
        }

        var value = double.Parse(numMatch.Value, CultureInfo.InvariantCulture);
        var afterIndex = numMatch.Index + numMatch.Length;
        if (afterIndex < cleaned.Length && cleaned[afterIndex] == 'k')
        {
            mult *= 1e3;
        }

        return value * mult;
    }

    private static string KindOf(Match m)
    {
        foreach (var name in new[] { "money", "percent", "duration", "ratio", "count" })
        {
            if (m.Groups[name].Success)
            {
                return name;
            }
        }
        return "count";
    }

    private static bool AcronymPrefixed(string head)
    {
        var tokens = NonSpaceRe().Matches(head);
        if (tokens.Count == 0)
        {
            return false;
        }
        var last = tokens[^1].Value.Trim('.', ',');
        return AcronymRe().IsMatch(last);
    }

    private static string LabelFor(string line, int spanStart)
    {
        var head = line[..spanStart];
        var inline = InlineLabelRe().Match(head.TrimEnd());
        if (inline.Success)
        {
            return inline.Groups[1].Value.Trim(' ', '*', '(');
        }

        var words = LabelWordRe().Matches(head)
            .Select(x => x.Value)
            .Where(w => !Fillers.Contains(w.ToLowerInvariant()))
            .ToList();
        var lastFour = words.Count > 4 ? words.GetRange(words.Count - 4, 4) : words;
        return string.Join(" ", lastFour).Trim();
    }

    private static IEnumerable<string> SplitLines(string text)
        => string.IsNullOrEmpty(text) ? [] : text.ReplaceLineEndings("\n").Split('\n');
}
