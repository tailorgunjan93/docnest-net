using System.Text.Json.Serialization;

namespace DocNest.Udf;

// Wire DTOs — the exact on-disk .udf JSON schema (the cross-ecosystem contract). These are an
// anti-corruption layer, intentionally separate from the persistence-ignorant domain records
// (ADR-0001/0002). Keys mirror the Python writer.py output exactly; nulls are written (Never).

/// <summary><c>manifest.json</c> — format version, embedding config, and flattened metadata.</summary>
public sealed record ManifestDto
{
    [JsonPropertyName("udf_version")] public required string UdfVersion { get; init; }
    [JsonPropertyName("doc_id")] public required string DocId { get; init; }
    [JsonPropertyName("title")] public required string Title { get; init; }
    [JsonPropertyName("source_format")] public string SourceFormat { get; init; } = "";
    [JsonPropertyName("created_at")] public string CreatedAt { get; init; } = "";
    [JsonPropertyName("embedding_model")] public string EmbeddingModel { get; init; } = "";
    [JsonPropertyName("embedding_dims")] public int EmbeddingDims { get; init; }
    [JsonPropertyName("quantization")] public string Quantization { get; init; } = "float16";
    [JsonPropertyName("section_count")] public int SectionCount { get; init; }
    [JsonPropertyName("intelligence")] public bool Intelligence { get; init; }
    [JsonPropertyName("embedding_format")] public string EmbeddingFormat { get; init; } = "binary";
    [JsonPropertyName("owner")] public string Owner { get; init; } = "";
    [JsonPropertyName("department")] public string Department { get; init; } = "";
    [JsonPropertyName("tags")] public IReadOnlyList<string> Tags { get; init; } = [];
    [JsonPropertyName("access_roles")] public IReadOnlyList<string> AccessRoles { get; init; } = ["*"];
    [JsonPropertyName("version")] public string Version { get; init; } = "1.0";
    [JsonPropertyName("last_updated")] public string LastUpdated { get; init; } = "";
    [JsonPropertyName("producer")] public string Producer { get; init; } = "";
}

/// <summary>One <c>key_numbers</c> entry.</summary>
public sealed record KeyNumberDto
{
    [JsonPropertyName("label")] public required string Label { get; init; }
    [JsonPropertyName("value")] public required string Value { get; init; }
    [JsonPropertyName("unit")] public string? Unit { get; init; }
    [JsonPropertyName("section")] public required string Section { get; init; }
}

/// <summary>One <c>section_index</c> entry (lightweight projection of a section).</summary>
public sealed record SectionIndexDto
{
    [JsonPropertyName("id")] public required string Id { get; init; }
    [JsonPropertyName("title")] public required string Title { get; init; }
    [JsonPropertyName("level")] public required int Level { get; init; }
    [JsonPropertyName("parent_id")] public string? ParentId { get; init; }
    [JsonPropertyName("children")] public IReadOnlyList<string> Children { get; init; } = [];
    [JsonPropertyName("summary")] public string Summary { get; init; } = "";
    [JsonPropertyName("keywords")] public IReadOnlyList<string> Keywords { get; init; } = [];
    [JsonPropertyName("token_count")] public int TokenCount { get; init; }
}

/// <summary><c>catalogue.json</c> — the in-RAM section index loaded on open.</summary>
public sealed record CatalogueDto
{
    [JsonPropertyName("doc_id")] public required string DocId { get; init; }
    [JsonPropertyName("title")] public required string Title { get; init; }
    [JsonPropertyName("source")] public string Source { get; init; } = "";
    [JsonPropertyName("language")] public string Language { get; init; } = "en";
    [JsonPropertyName("summary")] public string Summary { get; init; } = "";
    [JsonPropertyName("insights")] public IReadOnlyList<string> Insights { get; init; } = [];
    [JsonPropertyName("owner")] public string Owner { get; init; } = "";
    [JsonPropertyName("department")] public string Department { get; init; } = "";
    [JsonPropertyName("tags")] public IReadOnlyList<string> Tags { get; init; } = [];
    [JsonPropertyName("access_roles")] public IReadOnlyList<string> AccessRoles { get; init; } = ["*"];
    [JsonPropertyName("version")] public string Version { get; init; } = "1.0";
    [JsonPropertyName("last_updated")] public string LastUpdated { get; init; } = "";
    [JsonPropertyName("key_numbers")] public IReadOnlyList<KeyNumberDto> KeyNumbers { get; init; } = [];
    [JsonPropertyName("section_index")] public IReadOnlyList<SectionIndexDto> SectionIndex { get; init; } = [];
    [JsonPropertyName("embedding_model")] public string EmbeddingModel { get; init; } = "";
    [JsonPropertyName("embedding_dims")] public int EmbeddingDims { get; init; }
    [JsonPropertyName("quantization")] public string Quantization { get; init; } = "float16";
}

/// <summary>One table inside <c>content.json</c>.</summary>
public sealed record TableDto
{
    [JsonPropertyName("table_id")] public required string TableId { get; init; }
    [JsonPropertyName("caption")] public string? Caption { get; init; }
    [JsonPropertyName("headers")] public IReadOnlyList<string> Headers { get; init; } = [];
    [JsonPropertyName("rows")] public IReadOnlyList<IReadOnlyList<string>> Rows { get; init; } = [];
}

/// <summary>One image inside <c>content.json</c>.</summary>
public sealed record ImageDto
{
    [JsonPropertyName("image_id")] public required string ImageId { get; init; }
    [JsonPropertyName("alt")] public string? Alt { get; init; }
    [JsonPropertyName("asset_path")] public required string AssetPath { get; init; }
}

/// <summary>The full content of one section inside <c>content.json</c>.</summary>
public sealed record ContentSectionDto
{
    [JsonPropertyName("title")] public required string Title { get; init; }
    [JsonPropertyName("level")] public required int Level { get; init; }
    [JsonPropertyName("text")] public string Text { get; init; } = "";
    [JsonPropertyName("tables")] public IReadOnlyList<TableDto> Tables { get; init; } = [];
    [JsonPropertyName("images")] public IReadOnlyList<ImageDto> Images { get; init; } = [];
}

/// <summary><c>content.json</c> — full section texts, fetched lazily at query time. The
/// <c>sections</c> field is a map keyed by §id (not an array).</summary>
public sealed record ContentDto
{
    [JsonPropertyName("doc_id")] public required string DocId { get; init; }
    [JsonPropertyName("sections")] public IReadOnlyDictionary<string, ContentSectionDto> Sections { get; init; }
        = new Dictionary<string, ContentSectionDto>();
}
