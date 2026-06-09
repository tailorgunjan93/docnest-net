using System.Text.Json.Serialization;

namespace DocNest;

/// <summary>
/// Source-generated <see cref="JsonSerializerContext"/> for the DocNest domain records — reflection-free
/// and AOT-friendly. Property names are pinned via <c>[JsonPropertyName]</c> on the records, which lock
/// the JSON keys. The authoritative <c>.udf</c> wire shape is produced by the Slice-2 wire DTOs; this
/// context serialises the in-memory domain for tests, debugging, and interchange.
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = false,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(TableData))]
[JsonSerializable(typeof(ImageRef))]
[JsonSerializable(typeof(Section))]
[JsonSerializable(typeof(KeyNumber))]
[JsonSerializable(typeof(DocMeta))]
[JsonSerializable(typeof(RawDocument))]
[JsonSerializable(typeof(Document))]
[JsonSerializable(typeof(SectionIndexEntry))]
[JsonSerializable(typeof(Catalogue))]
public partial class DocNestJson : JsonSerializerContext;
