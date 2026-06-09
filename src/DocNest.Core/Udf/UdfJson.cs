using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DocNest.Udf;

/// <summary>
/// Source-generated <see cref="JsonSerializerContext"/> for the <c>.udf</c> wire DTOs. Unlike the
/// domain <c>DocNestJson</c>, this writes <c>null</c>s (<see cref="JsonIgnoreCondition.Never"/>) to
/// match Python's <c>json.dumps</c>, and the consuming options use
/// <see cref="JavaScriptEncoder.UnsafeRelaxedJsonEscaping"/> so non-ASCII (e.g. <c>§</c>) is emitted
/// raw — matching Python's <c>ensure_ascii=False</c>. Compact (no indentation). Wire ≠ domain.
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = false,
    DefaultIgnoreCondition = JsonIgnoreCondition.Never)]
[JsonSerializable(typeof(ManifestDto))]
[JsonSerializable(typeof(CatalogueDto))]
[JsonSerializable(typeof(ContentDto))]
public partial class UdfJson : JsonSerializerContext;

/// <summary>UTF-8 (de)serialisation for the <c>.udf</c> wire DTOs using <see cref="UdfJson"/>.</summary>
internal static class UdfSerializer
{
    // Copy the source-gen context's options and add the relaxed encoder (the encoder is not a
    // source-generation option, so it must be set on the JsonSerializerOptions instance).
    private static readonly JsonSerializerOptions Options = new(UdfJson.Default.Options)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    internal static byte[] SerializeToUtf8Bytes<T>(T value)
        => JsonSerializer.SerializeToUtf8Bytes(value, typeof(T), Options);

    internal static T Deserialize<T>(byte[] utf8)
        => (T)JsonSerializer.Deserialize(utf8, typeof(T), Options)!;
}
