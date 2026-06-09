using System.Text;

namespace DocNest.Parsers;

/// <summary>
/// Parses <c>.csv</c> / <c>.tsv</c> into a <see cref="RawDocument"/> with a single
/// <see cref="TableData"/> section (zero dependency). Delimiter auto-detected (<c>,</c> <c>\t</c>
/// <c>;</c> <c>|</c>; <c>.tsv</c> = tab); encoding cascade UTF-8-BOM → UTF-8 → Latin-1. Ports the
/// Python <c>CSVParser</c>.
/// </summary>
public sealed class CsvParser : IParser
{
    private static readonly string[] Suffixes = { ".csv", ".tsv" };
    private static readonly char[] CandidateDelimiters = { ',', '\t', ';', '|' };

    /// <inheritdoc/>
    public bool Supports(string path) => Suffixes.Contains(Path.GetExtension(path).ToLowerInvariant());

    /// <inheritdoc/>
    public async Task<RawDocument> ParseAsync(string path, CancellationToken cancellationToken = default)
    {
        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            throw new ParseException($"CSV/TSV file not found: {fullPath}");
        }

        byte[] bytes;
        try
        {
            bytes = await File.ReadAllBytesAsync(fullPath, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new ParseException($"Cannot read {fullPath}: {ex.Message}", ex);
        }

        if (bytes.Length == 0)
        {
            throw new ParseException($"CSV/TSV file is empty: {fullPath}");
        }

        var text = Decode(bytes);
        var suffix = Path.GetExtension(fullPath).ToLowerInvariant();
        var delimiter = DetectDelimiter(text, suffix);

        var rows = CsvReader.Parse(text, delimiter)
            .Where(r => r.Any(cell => cell.Trim().Length > 0))
            .ToList();
        if (rows.Count == 0)
        {
            throw new ParseException($"CSV/TSV file contains no data rows: {Path.GetFileName(fullPath)}");
        }

        var headers = rows[0].Select(h => h.Trim()).ToList();
        while (headers.Count > 0 && headers[^1].Length == 0)
        {
            headers.RemoveAt(headers.Count - 1);
        }
        if (headers.Count == 0)
        {
            throw new ParseException($"CSV/TSV file has no valid column headers: {Path.GetFileName(fullPath)}");
        }

        var width = headers.Count;
        var dataRows = new List<IReadOnlyList<string>>(rows.Count - 1);
        foreach (var row in rows.Skip(1))
        {
            if (row.Count < width)
            {
                var padded = new List<string>(row);
                padded.AddRange(Enumerable.Repeat(string.Empty, width - row.Count));
                dataRows.Add(padded);
            }
            else if (row.Count > width)
            {
                dataRows.Add(row.Take(width).ToList());
            }
            else
            {
                dataRows.Add(row);
            }
        }

        var table = new TableData { TableId = "tbl_001", Caption = null, Headers = headers, Rows = dataRows };
        var stem = Path.GetFileNameWithoutExtension(fullPath);
        var title = ParserText.FilenameToTitle(stem);
        var sectionText = TableTextSummary(table, stem);
        var section = new Section
        {
            Id = "",
            Title = title,
            Level = 1,
            Text = sectionText,
            Tables = new[] { table },
            TokenCount = Math.Max(1, ParserText.WordCount(sectionText)),
        };

        return new RawDocument
        {
            DocId = DocId.FromPath(path),
            Title = title,
            Source = fullPath,
            Format = suffix.TrimStart('.'),
            Sections = new[] { section },
        };
    }

    private static string Decode(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            return new UTF8Encoding(false).GetString(bytes, 3, bytes.Length - 3);
        }
        try
        {
            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return Encoding.Latin1.GetString(bytes);
        }
    }

    private static char DetectDelimiter(string text, string suffix)
    {
        if (suffix == ".tsv")
        {
            return '\t';
        }

        var sample = text.Length > 8192 ? text[..8192] : text;
        var firstLine = sample.Split('\n', 2)[0];

        var best = ',';
        var bestCount = 0;
        foreach (var candidate in CandidateDelimiters)
        {
            var count = CountOutsideQuotes(firstLine, candidate);
            if (count > bestCount)
            {
                bestCount = count;
                best = candidate;
            }
        }
        return best;
    }

    private static int CountOutsideQuotes(string line, char delimiter)
    {
        var inQuotes = false;
        var count = 0;
        foreach (var c in line)
        {
            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == delimiter && !inQuotes)
            {
                count++;
            }
        }
        return count;
    }

    private static string TableTextSummary(TableData table, string stem)
    {
        var parts = new List<string> { $"File: {stem}", $"Columns: {string.Join(", ", table.Headers)}" };
        if (table.Rows.Count > 0)
        {
            var lines = table.Rows.Select(r => string.Join(" | ", r));
            parts.Add("Data:\n" + string.Join("\n", lines));
        }
        return string.Join("\n", parts);
    }
}
