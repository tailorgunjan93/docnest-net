using System.Text;
using FluentAssertions;
using Xunit;

namespace DocNest.Storage.Tests;

public sealed class ZipStorageBackendTests : IDisposable
{
    private readonly string _dir;

    public ZipStorageBackendTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "docnest-zip-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }

    [Fact]
    public async Task Write_list_read_round_trips_text_and_binary()
    {
        var backend = new ZipStorageBackend();
        var udf = Path.Combine(_dir, "a.udf");
        var entries = new Dictionary<string, byte[]>
        {
            ["manifest.json"] = Encoding.UTF8.GetBytes("{\"k\":1}"),
            ["embeddings.bin"] = new byte[] { 1, 2, 3, 4, 5 },
        };

        var written = await backend.WriteArchiveAsync(entries, udf);

        File.Exists(written).Should().BeTrue();
        (await backend.ListEntriesAsync(udf)).Should().BeEquivalentTo("manifest.json", "embeddings.bin");
        (await backend.ReadEntryAsync(udf, "manifest.json")).Should().Equal(entries["manifest.json"]);
        (await backend.ReadEntryAsync(udf, "embeddings.bin")).Should().Equal(entries["embeddings.bin"]);
    }

    [Fact]
    public async Task ReadEntry_missing_entry_throws()
    {
        var backend = new ZipStorageBackend();
        var udf = Path.Combine(_dir, "b.udf");
        await backend.WriteArchiveAsync(new Dictionary<string, byte[]> { ["x.json"] = new byte[] { 1 } }, udf);

        var act = async () => await backend.ReadEntryAsync(udf, "missing.json");

        await act.Should().ThrowAsync<UdfReadException>();
    }

    [Fact]
    public async Task ReadEntry_missing_file_throws()
    {
        var backend = new ZipStorageBackend();
        var act = async () => await backend.ReadEntryAsync(Path.Combine(_dir, "nope.udf"), "x");
        await act.Should().ThrowAsync<UdfReadException>();
    }

    [Fact]
    public async Task Written_archive_is_a_valid_zip()
    {
        var backend = new ZipStorageBackend();
        var udf = Path.Combine(_dir, "c.udf");
        await backend.WriteArchiveAsync(new Dictionary<string, byte[]> { ["m.json"] = Encoding.UTF8.GetBytes("{}") }, udf);

        var bytes = await File.ReadAllBytesAsync(udf);
        bytes.Length.Should().BeGreaterThan(2);
        bytes[0].Should().Be((byte)'P');
        bytes[1].Should().Be((byte)'K');
    }
}
