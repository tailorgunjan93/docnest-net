using DocNest.Storage;

namespace DocNest.Udf;

/// <summary>
/// Loads a <c>.udf</c> archive and exposes its wire DTOs. Validates the format version and can
/// reconstruct a domain <see cref="Document"/>. Reads through an <see cref="IStorageBackend"/>
/// (default <see cref="ZipStorageBackend"/>).
/// </summary>
public static class UdfReader
{
    /// <summary>Load and validate a <c>.udf</c> file.</summary>
    /// <exception cref="UdfReadException">If the file is missing, invalid, or the wrong version.</exception>
    public static async Task<UdfPackage> LoadAsync(
        string udfPath,
        IStorageBackend? storage = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(udfPath);
        storage ??= new ZipStorageBackend();

        if (!File.Exists(udfPath))
        {
            throw new UdfReadException($"UDF file not found: {udfPath}");
        }

        try
        {
            var names = await storage.ListEntriesAsync(udfPath, cancellationToken).ConfigureAwait(false);
            if (!names.Contains("manifest.json"))
            {
                throw new UdfReadException($"Invalid .udf — missing manifest.json: {udfPath}");
            }

            var manifest = UdfSerializer.Deserialize<ManifestDto>(
                await storage.ReadEntryAsync(udfPath, "manifest.json", cancellationToken).ConfigureAwait(false));
            if (!string.Equals(manifest.UdfVersion, UdfFormat.Version, StringComparison.Ordinal))
            {
                throw new UdfReadException(
                    $"Unsupported .udf version '{manifest.UdfVersion}'. Expected '{UdfFormat.Version}'.");
            }

            var catalogue = UdfSerializer.Deserialize<CatalogueDto>(
                await storage.ReadEntryAsync(udfPath, "catalogue.json", cancellationToken).ConfigureAwait(false));
            var content = UdfSerializer.Deserialize<ContentDto>(
                await storage.ReadEntryAsync(udfPath, "content.json", cancellationToken).ConfigureAwait(false));

            byte[]? embeddings = names.Contains("embeddings.bin")
                ? await storage.ReadEntryAsync(udfPath, "embeddings.bin", cancellationToken).ConfigureAwait(false)
                : null;

            return new UdfPackage(manifest, catalogue, content, embeddings);
        }
        catch (Exception ex) when (ex is not UdfReadException and not OperationCanceledException)
        {
            throw new UdfReadException($"Failed to open '{udfPath}': {ex.Message}", ex);
        }
    }
}

/// <summary>An opened <c>.udf</c>: its three wire DTOs, optional raw embeddings, and a domain view.</summary>
public sealed class UdfPackage
{
    internal UdfPackage(ManifestDto manifest, CatalogueDto catalogue, ContentDto content, byte[]? embeddings)
    {
        Manifest = manifest;
        Catalogue = catalogue;
        Content = content;
        Embeddings = embeddings;
    }

    /// <summary>The <c>manifest.json</c> DTO.</summary>
    public ManifestDto Manifest { get; }

    /// <summary>The <c>catalogue.json</c> DTO.</summary>
    public CatalogueDto Catalogue { get; }

    /// <summary>The <c>content.json</c> DTO.</summary>
    public ContentDto Content { get; }

    /// <summary>Raw <c>embeddings.bin</c> bytes, or <see langword="null"/> when absent.</summary>
    public byte[]? Embeddings { get; }

    /// <summary>
    /// Reconstruct a domain <see cref="Document"/> by joining <c>section_index</c> (hierarchy,
    /// summary, keywords) with <c>content.sections</c> (text, tables, images) on §id.
    /// </summary>
    public Document ToDocument()
    {
        var sections = new List<Section>(Catalogue.SectionIndex.Count);
        foreach (var si in Catalogue.SectionIndex)
        {
            Content.Sections.TryGetValue(si.Id, out var c);
            sections.Add(new Section
            {
                Id = si.Id,
                Title = si.Title,
                Level = si.Level,
                Text = c?.Text ?? "",
                Tables = c is null
                    ? []
                    : c.Tables.Select(t => new TableData
                    {
                        TableId = t.TableId,
                        Caption = t.Caption,
                        Headers = t.Headers,
                        Rows = t.Rows.Select(r => (IReadOnlyList<string>)r.ToList()).ToList(),
                    }).ToList(),
                Images = c is null
                    ? []
                    : c.Images.Select(i => new ImageRef { ImageId = i.ImageId, Alt = i.Alt, AssetPath = i.AssetPath }).ToList(),
                ParentId = si.ParentId,
                Children = si.Children,
                TokenCount = si.TokenCount,
                Summary = string.IsNullOrEmpty(si.Summary) ? null : si.Summary,
                Keywords = si.Keywords,
            });
        }

        return new Document
        {
            DocId = Manifest.DocId,
            Title = Manifest.Title,
            Source = Catalogue.Source,
            Format = Manifest.SourceFormat,
            Sections = sections,
            Summary = string.IsNullOrEmpty(Catalogue.Summary) ? null : Catalogue.Summary,
            Insights = Catalogue.Insights,
            KeyNumbers = Catalogue.KeyNumbers
                .Select(k => new KeyNumber { Label = k.Label, Value = k.Value, Unit = k.Unit, Section = k.Section })
                .ToList(),
            Meta = new DocMeta
            {
                Owner = Catalogue.Owner,
                Department = Catalogue.Department,
                Tags = Catalogue.Tags,
                AccessRoles = Catalogue.AccessRoles,
                Version = Catalogue.Version,
                LastUpdated = Catalogue.LastUpdated,
            },
        };
    }
}
