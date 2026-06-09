using System.Text.Json.Serialization;

namespace DocNest;

/// <summary>
/// A structured table extracted from a document section. Tables are never flattened to text —
/// column headers are always preserved so the LLM receives full context.
/// </summary>
public sealed record TableData
{
    /// <summary>Unique ID within the document, e.g. <c>tbl_001</c>.</summary>
    [JsonPropertyName("table_id")] public required string TableId { get; init; }

    /// <summary>Table title or caption.</summary>
    [JsonPropertyName("caption")] public string? Caption { get; init; }

    private readonly IReadOnlyList<string> _headers = [];
    /// <summary>Column header labels.</summary>
    [JsonPropertyName("headers")]
    public IReadOnlyList<string> Headers { get => _headers; init => _headers = value ?? []; }

    private readonly IReadOnlyList<IReadOnlyList<string>> _rows = [];
    /// <summary>Data rows — each row length should equal the header count (enforced by the normaliser).</summary>
    [JsonPropertyName("rows")]
    public IReadOnlyList<IReadOnlyList<string>> Rows { get => _rows; init => _rows = value ?? []; }
}

/// <summary>Reference to an image asset extracted from a section.</summary>
public sealed record ImageRef
{
    /// <summary>Unique image ID within the document.</summary>
    [JsonPropertyName("image_id")] public required string ImageId { get; init; }

    /// <summary>Alt text, if any.</summary>
    [JsonPropertyName("alt")] public string? Alt { get; init; }

    /// <summary>Relative path inside the .udf zip, e.g. <c>assets/img_001.png</c>.</summary>
    [JsonPropertyName("asset_path")] public required string AssetPath { get; init; }
}

/// <summary>
/// A single navigable section within a document — the fundamental unit of retrieval. Every heading
/// in the source becomes one section. The LLM receives one section, not a blind chunk.
/// </summary>
public sealed record Section
{
    /// <summary>Section identifier, e.g. <c>§3.1</c>. Assigned by the normaliser (parsers leave it empty).</summary>
    [JsonPropertyName("id")] public required string Id { get; init; }

    /// <summary>Original heading text.</summary>
    [JsonPropertyName("title")] public required string Title { get; init; }

    /// <summary>Heading level (1 = H1 … 6 = H6).</summary>
    [JsonPropertyName("level")] public required int Level { get; init; }

    /// <summary>Full normalised section text.</summary>
    [JsonPropertyName("text")] public required string Text { get; init; }

    private readonly IReadOnlyList<TableData> _tables = [];
    /// <summary>Tables contained in this section.</summary>
    [JsonPropertyName("tables")]
    public IReadOnlyList<TableData> Tables { get => _tables; init => _tables = value ?? []; }

    private readonly IReadOnlyList<ImageRef> _images = [];
    /// <summary>Images contained in this section.</summary>
    [JsonPropertyName("images")]
    public IReadOnlyList<ImageRef> Images { get => _images; init => _images = value ?? []; }

    /// <summary>Parent section id; <see langword="null"/> for top-level sections.</summary>
    [JsonPropertyName("parent_id")] public string? ParentId { get; init; }

    private readonly IReadOnlyList<string> _children = [];
    /// <summary>Child section ids.</summary>
    [JsonPropertyName("children")]
    public IReadOnlyList<string> Children { get => _children; init => _children = value ?? []; }

    /// <summary>Approximate token count of <see cref="Text"/>.</summary>
    [JsonPropertyName("token_count")] public int TokenCount { get; init; }

    /// <summary>One-sentence section summary (filled by the intelligence stage).</summary>
    [JsonPropertyName("summary")] public string? Summary { get; init; }

    private readonly IReadOnlyList<string> _keywords = [];
    /// <summary>Keyword index terms (filled by the intelligence/keyword stage).</summary>
    [JsonPropertyName("keywords")]
    public IReadOnlyList<string> Keywords { get => _keywords; init => _keywords = value ?? []; }

    /// <summary>Quantised embedding bytes (filled by the embedder + quantizer stage).</summary>
    [JsonPropertyName("embedding")] public byte[]? Embedding { get; init; }

    /// <summary>Alias for <see cref="Id"/> — convenience accessor; never serialised.</summary>
    [JsonIgnore] public string SectionId => Id;
}
