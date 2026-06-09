using System.IO.Compression;

namespace DocNest.Storage;

/// <summary>
/// ZIP archive backend — the standard <c>.udf</c> file format. Wraps
/// <see cref="System.IO.Compression"/> behind the <see cref="IStorageBackend"/> contract.
/// JSON/text entries are DEFLATE-compressed; binary blobs and already-compressed images are stored
/// uncompressed (high entropy compresses poorly). Compression level is not part of the interop
/// contract — any valid ZIP both runtimes read is sufficient.
/// </summary>
public sealed class ZipStorageBackend : IStorageBackend
{
    private static readonly HashSet<string> PrecompressedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg", ".webp", ".gif" };

    /// <inheritdoc/>
    public string BackendName => "zip";

    /// <inheritdoc/>
    public async Task<string> WriteArchiveAsync(
        IReadOnlyDictionary<string, byte[]> entries,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentException.ThrowIfNullOrEmpty(outputPath);

        var fullPath = Path.GetFullPath(outputPath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        try
        {
            await using var file = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);
            using var archive = new ZipArchive(file, ZipArchiveMode.Create);
            foreach (var (name, data) in entries)
            {
                var entry = archive.CreateEntry(name, CompressionFor(name));
                await using var entryStream = entry.Open();
                await entryStream.WriteAsync(data.AsMemory(), cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is not UdfWriteException and not OperationCanceledException)
        {
            throw new UdfWriteException(
                $"ZipStorageBackend failed to write '{Path.GetFileName(fullPath)}': {ex.Message}", ex);
        }

        return fullPath;
    }

    /// <inheritdoc/>
    public async Task<byte[]> ReadEntryAsync(string archivePath, string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(archivePath);
        ArgumentException.ThrowIfNullOrEmpty(name);

        try
        {
            await using var file = new FileStream(archivePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var archive = new ZipArchive(file, ZipArchiveMode.Read);
            var entry = archive.GetEntry(name)
                ?? throw new UdfReadException($"Entry '{name}' not found in '{archivePath}'.");
            await using var entryStream = entry.Open();
            using var buffer = new MemoryStream(entry.Length is > 0 and < int.MaxValue ? (int)entry.Length : 0);
            await entryStream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
            return buffer.ToArray();
        }
        catch (Exception ex) when (ex is not UdfReadException and not OperationCanceledException)
        {
            throw new UdfReadException(
                $"ZipStorageBackend failed to read '{name}' from '{archivePath}': {ex.Message}", ex);
        }
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<string>> ListEntriesAsync(string archivePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(archivePath);

        try
        {
            using var file = new FileStream(archivePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var archive = new ZipArchive(file, ZipArchiveMode.Read);
            IReadOnlyList<string> names = archive.Entries.Select(e => e.FullName).ToList();
            return Task.FromResult(names);
        }
        catch (Exception ex) when (ex is not UdfReadException)
        {
            throw new UdfReadException(
                $"ZipStorageBackend failed to list entries in '{archivePath}': {ex.Message}", ex);
        }
    }

    private static CompressionLevel CompressionFor(string entryName)
    {
        var ext = Path.GetExtension(entryName);
        if (ext.Equals(".bin", StringComparison.OrdinalIgnoreCase) || PrecompressedExtensions.Contains(ext))
        {
            return CompressionLevel.NoCompression;
        }
        return CompressionLevel.SmallestSize;
    }
}
