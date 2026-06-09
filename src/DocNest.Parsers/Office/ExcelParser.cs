using System.Globalization;

namespace DocNest.Parsers;

/// <summary>
/// Parses <c>.xlsx</c> Excel workbooks (OpenXML SDK, behind this <see cref="IParser"/> wrapper).
/// Each worksheet becomes a section; rows are split into logical tables (a header row after
/// predominantly-numeric data starts a new table). Ports the Python <c>ExcelParser</c>.
/// <c>.xls</c> (legacy BIFF) is routed here but rejected with a clear message.
/// </summary>
public sealed class ExcelParser : IParser
{
    private static readonly string[] Suffixes = { ".xlsx", ".xls" };

    /// <inheritdoc/>
    public bool Supports(string path) => Suffixes.Contains(Path.GetExtension(path).ToLowerInvariant());

    /// <inheritdoc/>
    public async Task<RawDocument> ParseAsync(string path, CancellationToken cancellationToken = default)
    {
        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            throw new ParseException($"Excel file not found: {fullPath}");
        }
        if (new FileInfo(fullPath).Length == 0)
        {
            throw new ParseException($"Excel file is empty: {fullPath}");
        }
        if (Path.GetExtension(fullPath).Equals(".xls", StringComparison.OrdinalIgnoreCase))
        {
            throw new ParseException(
                $".xls (BIFF) format is not supported — only .xlsx. Convert '{Path.GetFileName(fullPath)}' to .xlsx first.");
        }

        return await Task.Run(() => Build(fullPath, path), cancellationToken).ConfigureAwait(false);
    }

    private static RawDocument Build(string fullPath, string originalPath)
    {
        List<(string Name, List<List<string>> Rows)> sheets;
        try
        {
            sheets = XlsxReader.ReadSheets(fullPath);
        }
        catch (ParseException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ParseException($"Failed to open '{Path.GetFileName(fullPath)}': {ex.Message}", ex);
        }

        var sections = new List<Section>();
        var tableCounter = 0;

        foreach (var (name, allRows) in sheets)
        {
            var rows = allRows.Where(r => r.Any(c => c.Trim().Length > 0)).ToList();
            if (rows.Count == 0)
            {
                continue;
            }

            var sectionTables = new List<TableData>();
            var textParts = new List<string>();
            foreach (var (header, data) in SplitIntoTables(rows))
            {
                tableCounter++;
                var table = BuildTable(header, data, $"tbl_{tableCounter:D3}");
                if (table is not null)
                {
                    sectionTables.Add(table);
                    textParts.Add(TableTextSummary(table, name));
                }
            }

            if (sectionTables.Count == 0)
            {
                continue;
            }

            sections.Add(new Section
            {
                Id = "",
                Title = name,
                Level = 1,
                Text = string.Join("\n", textParts),
                Tables = sectionTables,
            });
        }

        if (sections.Count == 0)
        {
            throw new ParseException($"Excel file contains no data sheets: {Path.GetFileName(fullPath)}");
        }

        return new RawDocument
        {
            DocId = DocId.FromPath(originalPath),
            Title = ParserText.FilenameToTitle(Path.GetFileNameWithoutExtension(fullPath)),
            Source = fullPath,
            Format = "xlsx",
            Sections = sections,
        };
    }

    private static List<(List<string> Header, List<List<string>> Data)> SplitIntoTables(List<List<string>> rows)
    {
        var result = new List<(List<string>, List<List<string>>)>();
        if (rows.Count == 0)
        {
            return result;
        }

        int? firstMultiCell = null;
        for (var i = 0; i < rows.Count; i++)
        {
            if (rows[i].Count(c => c.Trim().Length > 0) >= 2)
            {
                firstMultiCell = i;
                break;
            }
        }
        var headerStart = firstMultiCell ?? 0;

        List<string>? currentHeader = null;
        var currentData = new List<List<string>>();
        var previousWasData = false;

        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            if (currentHeader is null)
            {
                if (i < headerStart)
                {
                    continue;
                }
                currentHeader = row;
            }
            else if (LooksLikeHeader(row) && previousWasData && currentData.Count > 0)
            {
                result.Add((currentHeader, currentData));
                currentHeader = row;
                currentData = new List<List<string>>();
                previousWasData = false;
            }
            else
            {
                currentData.Add(row);
                previousWasData = IsMostlyNumeric(row);
            }
        }

        if (currentHeader is not null)
        {
            result.Add((currentHeader, currentData));
        }
        return result;
    }

    private static bool LooksLikeHeader(List<string> row)
    {
        var nonEmpty = row.Select(c => c.Trim()).Where(c => c.Length > 0).ToList();
        if (nonEmpty.Count < 2)
        {
            return false;
        }
        var textCells = nonEmpty.Count(c => !IsNumeric(c));
        if (textCells < nonEmpty.Count * 0.5)
        {
            return false;
        }
        return nonEmpty.Average(c => c.Length) <= 50;
    }

    private static bool IsMostlyNumeric(List<string> row)
    {
        var nonEmpty = row.Select(c => c.Trim()).Where(c => c.Length > 0).ToList();
        if (nonEmpty.Count == 0)
        {
            return false;
        }
        return nonEmpty.Count(IsNumeric) >= nonEmpty.Count * 0.5;
    }

    private static bool IsNumeric(string s)
    {
        s = s.Trim().Replace(",", "", StringComparison.Ordinal);
        return s.Length > 0
            && double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out _);
    }

    private static TableData? BuildTable(List<string> headerRow, List<List<string>> dataRows, string tableId)
    {
        var headers = headerRow.Select(h => h.Trim()).ToList();
        if (headers.Count == 0 || !headers.Any(h => h.Length > 0))
        {
            return null;
        }
        while (headers.Count > 0 && headers[^1].Length == 0)
        {
            headers.RemoveAt(headers.Count - 1);
        }
        if (headers.Count == 0)
        {
            return null;
        }

        var width = headers.Count;
        var normalised = new List<IReadOnlyList<string>>(dataRows.Count);
        foreach (var row in dataRows)
        {
            if (row.Count < width)
            {
                var padded = new List<string>(row);
                padded.AddRange(Enumerable.Repeat(string.Empty, width - row.Count));
                normalised.Add(padded);
            }
            else if (row.Count > width)
            {
                normalised.Add(row.Take(width).ToList());
            }
            else
            {
                normalised.Add(row);
            }
        }

        return new TableData { TableId = tableId, Caption = null, Headers = headers, Rows = normalised };
    }

    private static string TableTextSummary(TableData table, string sheetName)
    {
        var parts = new List<string> { $"Sheet: {sheetName}" };
        if (!string.IsNullOrEmpty(table.Caption))
        {
            parts.Add($"Table: {table.Caption}");
        }
        parts.Add($"Columns: {string.Join(", ", table.Headers)}");
        if (table.Rows.Count > 0)
        {
            parts.Add("Data:\n" + string.Join("\n", table.Rows.Select(r => string.Join(" | ", r))));
        }
        return string.Join("\n", parts);
    }
}
