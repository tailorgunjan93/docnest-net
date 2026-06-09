namespace DocNest;

/// <summary>Constants for the Universal Document Format (<c>.udf</c>) contract shared with Python.</summary>
public static class Udf
{
    /// <summary>
    /// The <c>.udf</c> format version. Mirrors the Python <c>UDF_VERSION</c> ("1.0"); a bump is an
    /// ADR-gated, version-bumped event because it is the cross-ecosystem compatibility contract.
    /// </summary>
    public const string Version = "1.0";
}
