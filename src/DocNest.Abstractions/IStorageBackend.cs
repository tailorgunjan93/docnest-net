namespace DocNest;

/// <summary>
/// Abstract interface for <c>.udf</c> archive read/write. Implement this to add a new storage
/// format (ZIP, directory, S3, database) without changing writer or reader code.
/// Mirrors the Python <c>IStorageBackend</c> ABC; Python's <c>str | bytes</c> entries collapse to
/// <see cref="byte"/>[] (callers UTF-8 encode text before writing).
/// </summary>
public interface IStorageBackend
{
    /// <summary>Write multiple named entries to an archive.</summary>
    /// <param name="entries">Map of entry name → raw content bytes.</param>
    /// <param name="outputPath">Destination path for the archive.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Absolute path to the created archive.</returns>
    /// <exception cref="UdfWriteException">If writing fails.</exception>
    Task<string> WriteArchiveAsync(
        IReadOnlyDictionary<string, byte[]> entries,
        string outputPath,
        CancellationToken cancellationToken = default);

    /// <summary>Read a single named entry from an archive.</summary>
    /// <exception cref="UdfReadException">If the archive or entry is missing / unreadable.</exception>
    Task<byte[]> ReadEntryAsync(string archivePath, string name, CancellationToken cancellationToken = default);

    /// <summary>List the entry names available in an archive.</summary>
    /// <exception cref="UdfReadException">If the archive is missing / unreadable.</exception>
    Task<IReadOnlyList<string>> ListEntriesAsync(string archivePath, CancellationToken cancellationToken = default);

    /// <summary>Backend identifier — e.g. <c>zip</c>, <c>dir</c>.</summary>
    string BackendName { get; }
}
