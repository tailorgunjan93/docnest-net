using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace DocNest.Parsers;

/// <summary>One reconstructed text line from a PDF: its text, representative font size, and bold flag.</summary>
internal readonly record struct PdfLine(string Text, double Size, bool Bold);

/// <summary>
/// Reconstructs ordered text lines from a PDF's letters (PdfPig gives letters, not lines). Letters are
/// clustered into lines by baseline Y (descending Y = top-to-bottom reading order) and ordered by X;
/// each line gets the rounded median font size and a majority-bold flag — the "span" stream the
/// font-size heading heuristic consumes.
/// </summary>
internal static class PdfLineExtractor
{
    private const double BaselineTolerance = 3.0;

    public static List<PdfLine> Extract(PdfDocument document)
    {
        var result = new List<PdfLine>();

        foreach (var page in document.GetPages())
        {
            var letters = page.Letters;
            if (letters.Count == 0)
            {
                continue;
            }

            // Cluster by text BASELINE (not the glyph bottom — descenders like 'y'/'p' extend below it).
            var ordered = letters.OrderByDescending(l => l.StartBaseLine.Y).ToList();
            var lines = new List<List<Letter>>();
            List<Letter>? current = null;
            var currentY = 0.0;

            foreach (var letter in ordered)
            {
                var y = letter.StartBaseLine.Y;
                if (current is null || Math.Abs(y - currentY) > BaselineTolerance)
                {
                    current = new List<Letter>();
                    lines.Add(current);
                    currentY = y;
                }
                current.Add(letter);
            }

            foreach (var line in lines)
            {
                var sorted = line.OrderBy(l => l.StartBaseLine.X).ToList();
                var text = string.Concat(sorted.Select(l => l.Value)).Trim();
                if (text.Length == 0)
                {
                    continue;
                }

                var size = Math.Round(Median(sorted.Select(l => l.PointSize)), 1);
                var boldCount = sorted.Count(IsBold);
                result.Add(new PdfLine(text, size, boldCount * 2 > sorted.Count));
            }
        }

        return result;
    }

    private static bool IsBold(Letter letter)
        => letter.FontName?.Contains("Bold", StringComparison.OrdinalIgnoreCase) ?? false;

    private static double Median(IEnumerable<double> values)
    {
        var sorted = values.OrderBy(x => x).ToList();
        return sorted.Count == 0 ? 0.0 : sorted[sorted.Count / 2];
    }
}
