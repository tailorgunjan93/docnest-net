using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace DocNest.Parsers;

/// <summary>
/// Low-level <c>.xlsx</c> reader (OpenXML SDK). Resolves the shared-strings table, reads cached cell
/// values, and densifies sparse rows by column letter so each sheet becomes a rectangular grid of
/// strings — the shape the Python (openpyxl) path produces. Behind the <see cref="ExcelParser"/> wrapper.
/// </summary>
internal static class XlsxReader
{
    public static List<(string Name, List<List<string>> Rows)> ReadSheets(string path)
    {
        var result = new List<(string, List<List<string>>)>();

        using var document = SpreadsheetDocument.Open(path, false);
        var workbookPart = document.WorkbookPart
            ?? throw new ParseException("Workbook part missing.");

        var sharedStrings = workbookPart.SharedStringTablePart?.SharedStringTable
            ?.Elements<SharedStringItem>().Select(s => s.InnerText).ToList();

        var sheets = workbookPart.Workbook.Sheets?.Elements<Sheet>() ?? Enumerable.Empty<Sheet>();
        foreach (var sheet in sheets)
        {
            var name = sheet.Name?.Value ?? "Sheet";
            if (sheet.Id?.Value is not { } relationshipId)
            {
                result.Add((name, new List<List<string>>()));
                continue;
            }

            var worksheetPart = (WorksheetPart)workbookPart.GetPartById(relationshipId);
            result.Add((name, ReadRows(worksheetPart, sharedStrings)));
        }

        return result;
    }

    private static List<List<string>> ReadRows(WorksheetPart worksheetPart, List<string>? sharedStrings)
    {
        var rows = new List<List<string>>();
        var sheetData = worksheetPart.Worksheet.GetFirstChild<SheetData>();
        if (sheetData is null)
        {
            return rows;
        }

        var parsed = new List<List<(int Col, string Value)>>();
        var maxCol = 0;
        foreach (var row in sheetData.Elements<Row>())
        {
            var cells = new List<(int, string)>();
            foreach (var cell in row.Elements<Cell>())
            {
                var col = ColumnIndex(cell.CellReference?.Value);
                cells.Add((col, CellText(cell, sharedStrings)));
                if (col > maxCol)
                {
                    maxCol = col;
                }
            }
            parsed.Add(cells);
        }

        foreach (var cells in parsed)
        {
            var dense = new string[maxCol + 1];
            Array.Fill(dense, string.Empty);
            foreach (var (col, value) in cells)
            {
                if (col >= 0 && col <= maxCol)
                {
                    dense[col] = value;
                }
            }
            rows.Add(dense.ToList());
        }

        return rows;
    }

    private static string CellText(Cell cell, List<string>? sharedStrings)
    {
        var raw = cell.CellValue?.InnerText ?? string.Empty;
        var type = cell.DataType?.Value;

        if (type == CellValues.SharedString)
        {
            return sharedStrings is not null && int.TryParse(raw, out var index)
                && index >= 0 && index < sharedStrings.Count
                ? sharedStrings[index]
                : string.Empty;
        }
        if (type == CellValues.InlineString)
        {
            return cell.InlineString?.Text?.Text ?? cell.InnerText.Trim();
        }
        if (type == CellValues.Boolean)
        {
            return raw == "1" ? "True" : "False";
        }
        return raw;
    }

    private static int ColumnIndex(string? cellReference)
    {
        if (string.IsNullOrEmpty(cellReference))
        {
            return 0;
        }

        var col = 0;
        var any = false;
        foreach (var ch in cellReference)
        {
            if (ch is >= 'A' and <= 'Z')
            {
                col = (col * 26) + (ch - 'A' + 1);
                any = true;
            }
            else if (ch is >= 'a' and <= 'z')
            {
                col = (col * 26) + (ch - 'a' + 1);
                any = true;
            }
            else
            {
                break;
            }
        }
        return any ? col - 1 : 0;
    }
}
