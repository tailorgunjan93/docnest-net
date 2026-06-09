using System.Text;
using UglyToad.PdfPig;

namespace DocNest.Parsers;

/// <summary>
/// Parses text-native <c>.pdf</c> files (UglyToad.PdfPig, behind this <see cref="IParser"/> wrapper).
/// Detects headings by relative font size (larger = heading), porting the Python fast PDF path.
/// Table extraction and OCR for scanned PDFs are out of scope (later hardening slices).
/// </summary>
public sealed class PdfParser : IParser
{
    private readonly double _headingThreshold;

    /// <summary>Create a parser. <paramref name="headingThreshold"/> = font-size multiple above the
    /// median body size for a line to be treated as a heading (default 1.15 = 15 % larger).</summary>
    public PdfParser(double headingThreshold = 1.15) => _headingThreshold = headingThreshold;

    /// <inheritdoc/>
    public bool Supports(string path) => path.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc/>
    public async Task<RawDocument> ParseAsync(string path, CancellationToken cancellationToken = default)
    {
        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            throw new ParseException($"PDF not found: {fullPath}");
        }
        if (new FileInfo(fullPath).Length == 0)
        {
            throw new ParseException($"PDF is empty: {fullPath}");
        }

        return await Task.Run(() => Build(fullPath, path), cancellationToken).ConfigureAwait(false);
    }

    private RawDocument Build(string fullPath, string originalPath)
    {
        List<PdfLine> lines;
        try
        {
            using var document = PdfDocument.Open(fullPath);
            lines = PdfLineExtractor.Extract(document);
        }
        catch (Exception ex)
        {
            throw new ParseException($"Failed to open '{Path.GetFileName(fullPath)}': {ex.Message}", ex);
        }

        return new RawDocument
        {
            DocId = DocId.FromPath(originalPath),
            Title = ExtractTitle(lines, fullPath),
            Source = fullPath,
            Format = "pdf",
            Sections = BuildSections(lines),
        };
    }

    private static string ExtractTitle(List<PdfLine> lines, string fullPath)
    {
        var fallback = ParserText.FilenameToTitle(Path.GetFileNameWithoutExtension(fullPath));
        if (lines.Count == 0)
        {
            return fallback;
        }
        var largest = lines.MaxBy(l => l.Size);
        return string.IsNullOrEmpty(largest.Text) ? fallback : largest.Text;
    }

    private IReadOnlyList<Section> BuildSections(List<PdfLine> lines)
    {
        if (lines.Count == 0)
        {
            return [];
        }

        var median = MedianSize(lines);
        var headingMin = median * _headingThreshold;

        var headingSizes = lines.Select(l => l.Size).Where(s => s >= headingMin).Distinct().OrderByDescending(s => s).ToList();
        var sizeToLevel = new Dictionary<double, int>();
        for (var i = 0; i < headingSizes.Count; i++)
        {
            sizeToLevel[headingSizes[i]] = Math.Min(i + 1, 6);
        }

        var sections = new List<Section>();
        string? title = null;
        var level = 0;
        var text = new StringBuilder();
        var hasCurrent = false;

        void Flush()
        {
            if (hasCurrent)
            {
                sections.Add(new Section { Id = "", Title = title!, Level = level, Text = text.ToString().Trim() });
            }
        }

        void Start(string newTitle, int newLevel)
        {
            Flush();
            title = newTitle;
            level = newLevel;
            text = new StringBuilder();
            hasCurrent = true;
        }

        foreach (var line in lines)
        {
            var isHeading = line.Size >= headingMin
                || (line.Bold && line.Size >= median * 1.05 && line.Text.Length < 100);

            if (isHeading && line.Text.Length > 0)
            {
                Start(line.Text, sizeToLevel.GetValueOrDefault(line.Size, 1));
            }
            else
            {
                if (line.Text.Length == 0)
                {
                    continue;
                }
                if (!hasCurrent)
                {
                    Start("Introduction", 1);
                }
                text.Append(line.Text).Append('\n');
            }
        }

        Flush();
        return sections;
    }

    private static double MedianSize(List<PdfLine> lines)
    {
        var sizes = lines.Select(l => l.Size).OrderBy(s => s).ToList();
        return sizes.Count == 0 ? 11.0 : sizes[sizes.Count / 2];
    }
}
