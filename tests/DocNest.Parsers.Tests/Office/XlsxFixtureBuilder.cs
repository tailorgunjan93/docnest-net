using System;
using System.Globalization;
using System.IO;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace DocNest.Parsers.Tests;

/// <summary>
/// Builds a known <c>.xlsx</c> in a temp file (OpenXML SDK) for the Excel parser tests. Text cells use
/// the shared-strings table; numeric cells are inline numbers; one row is blank (filtered); one sheet
/// is empty (skipped). The "Sales" sheet holds two stacked logical tables.
/// </summary>
internal static class XlsxFixtureBuilder
{
    private static readonly (string Name, string?[][] Rows)[] Data =
    {
        ("Sales", new[]
        {
            new string?[] { "Year", "Revenue", "Profit" },
            new string?[] { "2021", "100", "10" },
            new string?[] { "2022", "200", "20" },
            new string?[] { null, null, null },        // blank row → filtered out
            new string?[] { "Region", "Sales" },
            new string?[] { "North", "300" },
        }),
        ("Notes", new[]
        {
            new string?[] { "Note" },
            new string?[] { "Hello" },
        }),
        ("Empty", Array.Empty<string?[]>()),
    };

    public static string Create()
    {
        var path = Path.Combine(Path.GetTempPath(), "docnest-xlsx-" + Guid.NewGuid().ToString("N") + ".xlsx");

        using var document = SpreadsheetDocument.Create(path, SpreadsheetDocumentType.Workbook);
        var workbookPart = document.AddWorkbookPart();
        workbookPart.Workbook = new Workbook();
        var sharedPart = workbookPart.AddNewPart<SharedStringTablePart>();
        sharedPart.SharedStringTable = new SharedStringTable();
        var sheets = workbookPart.Workbook.AppendChild(new Sheets());

        uint sheetId = 1;
        foreach (var (name, rows) in Data)
        {
            var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
            var sheetData = new SheetData();

            uint rowIndex = 1;
            foreach (var cells in rows)
            {
                var row = new Row { RowIndex = rowIndex };
                var col = 0;
                foreach (var value in cells)
                {
                    var cell = new Cell { CellReference = $"{ColumnLetter(col)}{rowIndex}" };
                    if (value is not null)
                    {
                        if (IsNumeric(value))
                        {
                            cell.DataType = CellValues.Number;
                            cell.CellValue = new CellValue(value);
                        }
                        else
                        {
                            cell.DataType = CellValues.SharedString;
                            cell.CellValue = new CellValue(AddShared(sharedPart, value).ToString(CultureInfo.InvariantCulture));
                        }
                    }
                    row.Append(cell);
                    col++;
                }
                sheetData.Append(row);
                rowIndex++;
            }

            worksheetPart.Worksheet = new Worksheet(sheetData);
            sheets.Append(new Sheet { Id = workbookPart.GetIdOfPart(worksheetPart), SheetId = sheetId, Name = name });
            sheetId++;
        }

        workbookPart.Workbook.Save();
        return path;
    }

    private static int AddShared(SharedStringTablePart part, string text)
    {
        var index = 0;
        foreach (var item in part.SharedStringTable.Elements<SharedStringItem>())
        {
            if (item.InnerText == text)
            {
                return index;
            }
            index++;
        }
        part.SharedStringTable.Append(new SharedStringItem(new Text(text)));
        return index;
    }

    private static bool IsNumeric(string s)
        => double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out _);

    private static string ColumnLetter(int index)
    {
        var letters = string.Empty;
        index++;
        while (index > 0)
        {
            var remainder = (index - 1) % 26;
            letters = (char)('A' + remainder) + letters;
            index = (index - 1) / 26;
        }
        return letters;
    }
}
