using System.Collections.Generic;
using System.IO;
using System.Text;
using DocNest;
using DocNest.Storage;
using DocNest.Udf;
using FluentAssertions;
using Xunit;

namespace DocNest.Tests.Udf;

public class UdfWriterReaderTests
{
    [Fact]
    public async Task Document_round_trips_through_udf()
    {
        var doc = UdfTestData.Sample();
        var path = UdfTestData.TempUdf();
        try
        {
            await new UdfWriter().WriteAsync(doc, path);
            var pkg = await UdfReader.LoadAsync(path);
            pkg.ToDocument().Should().BeEquivalentTo(doc);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Missing_manifest_is_rejected()
    {
        var path = UdfTestData.TempUdf();
        try
        {
            await new ZipStorageBackend().WriteArchiveAsync(
                new Dictionary<string, byte[]> { ["catalogue.json"] = Encoding.UTF8.GetBytes("{}") }, path);

            var act = async () => await UdfReader.LoadAsync(path);
            await act.Should().ThrowAsync<UdfReadException>().WithMessage("*manifest*");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Wrong_version_is_rejected()
    {
        var path = UdfTestData.TempUdf();
        try
        {
            var manifest = Encoding.UTF8.GetBytes("{\"udf_version\":\"9.9\",\"doc_id\":\"d\",\"title\":\"t\"}");
            await new ZipStorageBackend().WriteArchiveAsync(
                new Dictionary<string, byte[]> { ["manifest.json"] = manifest }, path);

            var act = async () => await UdfReader.LoadAsync(path);
            await act.Should().ThrowAsync<UdfReadException>().WithMessage("*version*");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Embeddings_present_and_absent_round_trip()
    {
        var doc = UdfTestData.Sample();
        var withEmb = UdfTestData.TempUdf();
        var noEmb = UdfTestData.TempUdf();
        try
        {
            var emb = new EmbeddingBlock("test-model", 2, "float16", new[]
            {
                new byte[] { 1, 2, 3, 4 },
                new byte[] { 5, 6, 7, 8 },
                new byte[] { 9, 10, 11, 12 },
            });

            await new UdfWriter().WriteAsync(doc, withEmb, embeddings: emb);
            var pkg = await UdfReader.LoadAsync(withEmb);
            pkg.Embeddings.Should().NotBeNull();
            pkg.Embeddings!.Length.Should().Be(12);
            pkg.Manifest.EmbeddingModel.Should().Be("test-model");
            pkg.Manifest.EmbeddingDims.Should().Be(2);

            await new UdfWriter().WriteAsync(doc, noEmb);
            var pkg2 = await UdfReader.LoadAsync(noEmb);
            pkg2.Embeddings.Should().BeNull();
            pkg2.Manifest.EmbeddingDims.Should().Be(0);
        }
        finally
        {
            File.Delete(withEmb);
            File.Delete(noEmb);
        }
    }
}
