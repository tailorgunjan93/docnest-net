using System.Text.Json.Serialization;

namespace DocNest;

/// <summary>A metric or key figure extracted from the document.</summary>
public sealed record KeyNumber
{
    /// <summary>Human-readable label, e.g. <c>Revenue</c>.</summary>
    [JsonPropertyName("label")] public required string Label { get; init; }

    /// <summary>The value, e.g. <c>$142M</c>.</summary>
    [JsonPropertyName("value")] public required string Value { get; init; }

    /// <summary>Unit of measurement, e.g. <c>USD</c>, <c>percent</c>.</summary>
    [JsonPropertyName("unit")] public string? Unit { get; init; }

    /// <summary>Source section id, e.g. <c>§3.1</c>.</summary>
    [JsonPropertyName("section")] public required string Section { get; init; }
}

/// <summary>Human-facing metadata for a document — ownership, access, versioning.</summary>
public sealed record DocMeta
{
    /// <summary>Person or team that owns this document.</summary>
    [JsonPropertyName("owner")] public string Owner { get; init; } = "";

    /// <summary>Business department, e.g. <c>Engineering</c>, <c>HR</c>.</summary>
    [JsonPropertyName("department")] public string Department { get; init; } = "";

    private readonly IReadOnlyList<string> _tags = [];
    /// <summary>Searchable topic tags.</summary>
    [JsonPropertyName("tags")]
    public IReadOnlyList<string> Tags { get => _tags; init => _tags = value ?? []; }

    private readonly IReadOnlyList<string> _accessRoles = ["*"];
    /// <summary>Roles that may access this doc. <c>["*"]</c> means everyone.</summary>
    [JsonPropertyName("access_roles")]
    public IReadOnlyList<string> AccessRoles { get => _accessRoles; init => _accessRoles = value ?? ["*"]; }

    /// <summary>Document version string, e.g. <c>2.1</c>.</summary>
    [JsonPropertyName("version")] public string Version { get; init; } = "1.0";

    /// <summary>ISO date of last content update, e.g. <c>2025-04-22</c>.</summary>
    [JsonPropertyName("last_updated")] public string LastUpdated { get; init; } = "";
}

/// <summary>
/// Output of Stage 1 (parsing) — unstructured, before §id assignment. Parsers produce this; the
/// normaliser consumes it.
/// </summary>
public sealed record RawDocument
{
    /// <summary>Stable, slug-friendly document id.</summary>
    [JsonPropertyName("doc_id")] public required string DocId { get; init; }

    /// <summary>Document title.</summary>
    [JsonPropertyName("title")] public required string Title { get; init; }

    /// <summary>Absolute file path or source URL.</summary>
    [JsonPropertyName("source")] public required string Source { get; init; }

    /// <summary>File format: pdf, docx, xlsx, html, md, etc.</summary>
    [JsonPropertyName("format")] public required string Format { get; init; }

    private readonly IReadOnlyList<Section> _sections = [];
    /// <summary>Sections without §ids yet.</summary>
    [JsonPropertyName("sections")]
    public IReadOnlyList<Section> Sections { get => _sections; init => _sections = value ?? []; }

    /// <summary>Full raw text if section extraction failed.</summary>
    [JsonPropertyName("raw_text")] public string? RawText { get; init; }
}

/// <summary>A fully normalised document — output of the complete pipeline.</summary>
public sealed record Document
{
    /// <summary>Stable, slug-friendly document id.</summary>
    [JsonPropertyName("doc_id")] public required string DocId { get; init; }

    /// <summary>Document title.</summary>
    [JsonPropertyName("title")] public required string Title { get; init; }

    /// <summary>Source path or URL.</summary>
    [JsonPropertyName("source")] public required string Source { get; init; }

    /// <summary>File format.</summary>
    [JsonPropertyName("format")] public required string Format { get; init; }

    private readonly IReadOnlyList<Section> _sections = [];
    /// <summary>Sections with assigned §ids and linked hierarchy.</summary>
    [JsonPropertyName("sections")]
    public IReadOnlyList<Section> Sections { get => _sections; init => _sections = value ?? []; }

    /// <summary>Document-level summary (filled by the intelligence stage).</summary>
    [JsonPropertyName("summary")] public string? Summary { get; init; }

    private readonly IReadOnlyList<string> _insights = [];
    /// <summary>3–5 non-obvious findings (filled by the intelligence stage).</summary>
    [JsonPropertyName("insights")]
    public IReadOnlyList<string> Insights { get => _insights; init => _insights = value ?? []; }

    private readonly IReadOnlyList<KeyNumber> _keyNumbers = [];
    /// <summary>Extracted key numbers.</summary>
    [JsonPropertyName("key_numbers")]
    public IReadOnlyList<KeyNumber> KeyNumbers { get => _keyNumbers; init => _keyNumbers = value ?? []; }

    /// <summary>Human-facing metadata.</summary>
    [JsonPropertyName("meta")] public DocMeta Meta { get; init; } = new();
}
