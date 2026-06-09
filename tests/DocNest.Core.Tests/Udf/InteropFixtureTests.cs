using System;
using System.IO;
using DocNest.Udf;
using FluentAssertions;
using Xunit;

namespace DocNest.Tests.Udf;

/// <summary>
/// Cross-ecosystem interop. E1 reads a Python-produced golden <c>.udf</c>; E2 checks a .NET-written
/// <c>.udf</c> opens in Python. Both <see cref="SkippableFact"/> — they skip with a clear reason
/// (never a silent pass) when the fixture or a Python env is unavailable.
/// </summary>
public class InteropFixtureTests
{
    private static string FixturePath(string name)
        => Path.Combine(AppContext.BaseDirectory, "fixtures", name);

    [SkippableFact]
    public async Task E1_loads_python_produced_golden_udf()
    {
        var fixture = FixturePath("sample.udf");
        Skip.IfNot(
            File.Exists(fixture),
            $"Golden fixture not present ({fixture}). Run tools/make_fixture.py in a Python env with " +
            "docnest installed and commit tests/fixtures/sample.udf.");

        var pkg = await UdfReader.LoadAsync(fixture);

        pkg.Manifest.UdfVersion.Should().Be("1.0");
        pkg.Catalogue.SectionIndex.Should().NotBeEmpty();
        pkg.Content.Sections.Should().NotBeEmpty();
        foreach (var si in pkg.Catalogue.SectionIndex)
        {
            pkg.Content.Sections.Should().ContainKey(si.Id);
        }
        pkg.ToDocument().Sections.Should().HaveCount(pkg.Catalogue.SectionIndex.Count);
    }

    [SkippableFact]
    public async Task E2_dotnet_written_udf_is_readable_by_python()
    {
        var python = PythonLocator.Find();
        Skip.IfNot(python is not null, "No Python on PATH — cross-runtime write test skipped.");

        var path = UdfTestData.TempUdf();
        try
        {
            await new UdfWriter().WriteAsync(UdfTestData.Sample(), path);

            const string script =
                "import sys; from docnest.reader import UDFIndex; " +
                "idx = UDFIndex.load(sys.argv[1]); " +
                "print(len(idx._catalogue.get('section_index', [])))";
            var (ok, output) = PythonLocator.TryRun(python!, "-c", script, path);

            Skip.IfNot(ok, $"Python could not load the .udf (env likely missing docnest): {output}");
            output.Trim().Should().Be(UdfTestData.Sample().Sections.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
