using System;
using System.IO;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;

namespace DocNest.Parsers.Tests;

/// <summary>
/// Builds a known text <c>.pdf</c> in a temp file (PdfSharp) for the PDF parser tests: a 24pt title,
/// a 16pt and a 13pt heading, and several 11pt body lines (so the median size is the body size and the
/// thresholds bite predictably). Parsed back with PdfPig.
/// </summary>
internal static class PdfFixtureBuilder
{
    public static string Create()
    {
        if (OperatingSystem.IsWindows())
        {
            GlobalFontSettings.UseWindowsFontsUnderWindows = true;
        }

        var path = Path.Combine(Path.GetTempPath(), "docnest-pdf-" + Guid.NewGuid().ToString("N") + ".pdf");

        using var document = new PdfDocument();
        var page = document.AddPage();
        using var gfx = XGraphics.FromPdfPage(page);

        var title = new XFont("Arial", 24, XFontStyleEx.Bold);
        var heading = new XFont("Arial", 16, XFontStyleEx.Bold);
        var subheading = new XFont("Arial", 13, XFontStyleEx.Bold);
        var body = new XFont("Arial", 11, XFontStyleEx.Regular);

        var y = 60.0;
        void Draw(string text, XFont font)
        {
            gfx.DrawString(text, font, XBrushes.Black, new XPoint(50, y));
            y += font.Height + 8;
        }

        Draw("Quarterly Report", title);
        Draw("Revenue", heading);
        Draw("Revenue grew strongly this year.", body);
        Draw("It was a record quarter overall.", body);
        Draw("Margins improved as well.", body);
        Draw("Details", subheading);
        Draw("Costs fell across the board.", body);
        Draw("Headcount remained stable.", body);
        Draw("Outlook is positive.", body);

        document.Save(path);
        return path;
    }
}
