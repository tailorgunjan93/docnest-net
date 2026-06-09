using System.Text.Json.Serialization;

namespace DocNest;

/// <summary>
/// One lightweight entry in a catalogue's <c>section_index</c> — a projection of <see cref="Section"/>
/// without the heavy text/tables/embedding (those live in <c>content.json</c> / <c>embeddings.bin</c>).
/// </summary>
public sealed record SectionIndexEntry
{
    /// <summary>Section identifier, e.g. <c>§3.1</c>.</summary>
    [JsonPropertyName("id")] public required string Id { get; init; }

    /// <summary>Heading text.</summary>
    [JsonPropertyName("title")] public required string Title { get; init; }

    /// <summary>Heading level (1–6).</summary>
    [JsonPropertyName("level")] public required int Level { get; init; }

    /// <summary>Parent section id; <see langword="null"/> for top-level.</summary>
    [JsonPropertyName("parent_id")] public string? ParentId { get; init; }

    private readonly IReadOnlyList<string> _children = [];
    /// <summary>Child section ids.</summary>
    [JsonPropertyName("children")]
    public IReadOnlyList<string> Children { get => _children; init => _children = value ?? []; }

    /// <summary>One-sentence summary (empty string when absent).</summary>
    [JsonPropertyName("summary")] public string Summary { get; init; } = "";

    private readonly IReadOnlyList<string> _keywords = [];
    /// <summary>Keyword index terms.</summary>
    [JsonPropertyName("keywords")]
    public IReadOnlyList<string> Keywords { get => _keywords; init => _keywords = value ?? []; }

    /// <summary>Approximate token count.</summary>
    [JsonPropertyName("token_count")] public int TokenCount { get; init; }
}

/// <summary>
/// Lightweight document catalogue stored in <c>catalogue.json</c> inside the <c>.udf</c>. Loaded into
/// memory on file open; <c>content.json</c> is fetched section-by-section on demand.
/// </summary>
public sealed record Catalogue
{
    /// <summary>Stable, slug-friendly document id.</summary>
    [JsonPropertyName("doc_id")] public required string DocId { get; init; }

    /// <summary>Document title.</summary>
    [JsonPropertyName("title")] public required string Title { get; init; }

    /// <summary>Source label (basename by default — privacy-safe).</summary>
    [JsonPropertyName("source")] public required string Source { get; init; }

    /// <summary>Document language code.</summary>
    [JsonPropertyName("language")] public string Language { get; init; } = "en";

    /// <summary>Document-level summary.</summary>
    [JsonPropertyName("summary")] public string Summary { get; init; } = "";

    private readonly IReadOnlyList<string> _insights = [];
    /// <summary>Non-obvious findings.</summary>
    [JsonPropertyName("insights")]
    public IReadOnlyList<string> Insights { get => _insights; init => _insights = value ?? []; }

    /// <summary>Person or team that owns this document.</summary>
    [JsonPropertyName("owner")] public string Owner { get; init; } = "";

    /// <summary>Business department.</summary>
    [JsonPropertyName("department")] public string Department { get; init; } = "";

    private readonly IReadOnlyList<string> _tags = [];
    /// <summary>Searchable topic tags.</summary>
    [JsonPropertyName("tags")]
    public IReadOnlyList<string> Tags { get => _tags; init => _tags = value ?? []; }

    private readonly IReadOnlyList<string> _accessRoles = ["*"];
    /// <summary>Roles that may access this doc. <c>["*"]</c> means everyone.</summary>
    [JsonPropertyName("access_roles")]
    public IReadOnlyList<string> AccessRoles { get => _accessRoles; init => _accessRoles = value ?? ["*"]; }

    /// <summary>Document version string.</summary>
    [JsonPropertyName("version")] public string Version { get; init; } = "1.0";

    /// <summary>ISO date of last content update.</summary>
    [JsonPropertyName("last_updated")] public string LastUpdated { get; init; } = "";

    private readonly IReadOnlyList<KeyNumber> _keyNumbers = [];
    /// <summary>Extracted key numbers.</summary>
    [JsonPropertyName("key_numbers")]
    public IReadOnlyList<KeyNumber> KeyNumbers { get => _keyNumbers; init => _keyNumbers = value ?? []; }

    private readonly IReadOnlyList<SectionIndexEntry> _sectionIndex = [];
    /// <summary>Lightweight per-section index entries.</summary>
    [JsonPropertyName("section_index")]
    public IReadOnlyList<SectionIndexEntry> SectionIndex { get => _sectionIndex; init => _sectionIndex = value ?? []; }

    /// <summary>Canonical embedding model identifier.</summary>
    [JsonPropertyName("embedding_model")] public string EmbeddingModel { get; init; } = "";

    /// <summary>Embedding dimensionality.</summary>
    [JsonPropertyName("embedding_dims")] public int EmbeddingDims { get; init; }

    /// <summary>Quantization mode, e.g. <c>float16</c>.</summary>
    [JsonPropertyName("quantization")] public string Quantization { get; init; } = "float16";
}
