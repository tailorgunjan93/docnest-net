using System.Globalization;
using DocNest.Storage;

namespace DocNest.Udf;

/// <summary>
/// Optional pre-computed embeddings to embed in a <c>.udf</c> as <c>embeddings.bin</c>. One
/// quantised byte vector per section, in section order. (Vector generation is Slice 6; this slice
/// only carries supplied bytes.)
/// </summary>
public sealed record EmbeddingBlock(string Model, int Dims, string Quantization, IReadOnlyList<byte[]> Vectors);

/// <summary>
/// Builds a <c>.udf</c> archive from a normalised <see cref="Document"/>. Maps the domain to the wire
/// DTOs (<c>manifest</c>/<c>catalogue</c>/<c>content</c>) and writes them through an
/// <see cref="IStorageBackend"/> (default <see cref="ZipStorageBackend"/>). Design pattern: Builder.
/// </summary>
public sealed class UdfWriter
{
    private const string ProducerTag = "docnest-dotnet 0.1";
    private readonly IStorageBackend _storage;

    /// <summary>Create a writer over the given storage backend (default ZIP <c>.udf</c>).</summary>
    public UdfWriter(IStorageBackend? storage = null) => _storage = storage ?? new ZipStorageBackend();

    /// <summary>Write <paramref name="doc"/> to a <c>.udf</c> at <paramref name="outputPath"/>.</summary>
    /// <param name="doc">Fully normalised document.</param>
    /// <param name="outputPath">Destination <c>.udf</c> path.</param>
    /// <param name="includeSourcePath">Store the full source path instead of just the basename.</param>
    /// <param name="embeddings">Optional pre-computed embeddings → <c>embeddings.bin</c>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Absolute path to the created <c>.udf</c>.</returns>
    /// <exception cref="UdfWriteException">If building or writing fails.</exception>
    public async Task<string> WriteAsync(
        Document doc,
        string outputPath,
        bool includeSourcePath = false,
        EmbeddingBlock? embeddings = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(doc);
        ArgumentException.ThrowIfNullOrEmpty(outputPath);

        try
        {
            var entries = new Dictionary<string, byte[]>(StringComparer.Ordinal)
            {
                ["manifest.json"] = UdfSerializer.SerializeToUtf8Bytes(BuildManifest(doc, embeddings)),
                ["catalogue.json"] = UdfSerializer.SerializeToUtf8Bytes(BuildCatalogue(doc, includeSourcePath, embeddings)),
                ["content.json"] = UdfSerializer.SerializeToUtf8Bytes(BuildContent(doc)),
            };
            if (embeddings is { Vectors.Count: > 0 })
            {
                entries["embeddings.bin"] = Concat(embeddings.Vectors);
            }

            return await _storage.WriteArchiveAsync(entries, outputPath, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not UdfWriteException and not OperationCanceledException)
        {
            throw new UdfWriteException($"Failed to write '{Path.GetFileName(outputPath)}': {ex.Message}", ex);
        }
    }

    private static ManifestDto BuildManifest(Document doc, EmbeddingBlock? emb) => new()
    {
        UdfVersion = UdfFormat.Version,
        DocId = doc.DocId,
        Title = doc.Title,
        SourceFormat = doc.Format,
        CreatedAt = DateTimeOffset.UtcNow.ToString("o", CultureInfo.InvariantCulture),
        EmbeddingModel = emb?.Model ?? "",
        EmbeddingDims = emb?.Dims ?? 0,
        Quantization = emb?.Quantization ?? "float16",
        SectionCount = doc.Sections.Count,
        Intelligence = true,
        EmbeddingFormat = "binary",
        Owner = doc.Meta.Owner,
        Department = doc.Meta.Department,
        Tags = doc.Meta.Tags,
        AccessRoles = doc.Meta.AccessRoles,
        Version = doc.Meta.Version,
        LastUpdated = doc.Meta.LastUpdated,
        Producer = ProducerTag,
    };

    private static CatalogueDto BuildCatalogue(Document doc, bool includeSourcePath, EmbeddingBlock? emb) => new()
    {
        DocId = doc.DocId,
        Title = doc.Title,
        Source = SourceSanitiser.Sanitise(doc.Source, includeSourcePath),
        Language = "en",
        Summary = doc.Summary ?? "",
        Insights = doc.Insights,
        Owner = doc.Meta.Owner,
        Department = doc.Meta.Department,
        Tags = doc.Meta.Tags,
        AccessRoles = doc.Meta.AccessRoles,
        Version = doc.Meta.Version,
        LastUpdated = doc.Meta.LastUpdated,
        KeyNumbers = doc.KeyNumbers
            .Select(k => new KeyNumberDto { Label = k.Label, Value = k.Value, Unit = k.Unit, Section = k.Section })
            .ToList(),
        SectionIndex = doc.Sections
            .Select(s => new SectionIndexDto
            {
                Id = s.Id,
                Title = s.Title,
                Level = s.Level,
                ParentId = s.ParentId,
                Children = s.Children,
                Summary = s.Summary ?? "",
                Keywords = s.Keywords,
                TokenCount = s.TokenCount,
            })
            .ToList(),
        EmbeddingModel = emb?.Model ?? "",
        EmbeddingDims = emb?.Dims ?? 0,
        Quantization = emb?.Quantization ?? "float16",
    };

    private static ContentDto BuildContent(Document doc)
    {
        var sections = new Dictionary<string, ContentSectionDto>(StringComparer.Ordinal);
        foreach (var s in doc.Sections)
        {
            sections[s.Id] = new ContentSectionDto
            {
                Title = s.Title,
                Level = s.Level,
                Text = s.Text,
                Tables = s.Tables
                    .Select(t => new TableDto
                    {
                        TableId = t.TableId,
                        Caption = t.Caption,
                        Headers = t.Headers,
                        Rows = t.Rows.Select(r => (IReadOnlyList<string>)r.ToList()).ToList(),
                    })
                    .ToList(),
                Images = s.Images
                    .Select(i => new ImageDto { ImageId = i.ImageId, Alt = i.Alt, AssetPath = i.AssetPath })
                    .ToList(),
            };
        }
        return new ContentDto { DocId = doc.DocId, Sections = sections };
    }

    private static byte[] Concat(IReadOnlyList<byte[]> vectors)
    {
        var total = 0;
        foreach (var v in vectors)
        {
            total += v.Length;
        }
        var blob = new byte[total];
        var offset = 0;
        foreach (var v in vectors)
        {
            Buffer.BlockCopy(v, 0, blob, offset, v.Length);
            offset += v.Length;
        }
        return blob;
    }
}
