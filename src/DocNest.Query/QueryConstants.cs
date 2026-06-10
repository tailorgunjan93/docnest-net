namespace DocNest.Query;

/// <summary>Token sets shared by the deterministic answer layers (ported from the Python reader).</summary>
internal static class QueryConstants
{
    public static readonly HashSet<string> Fillers = new(StringComparer.Ordinal)
    {
        "the", "a", "an", "of", "to", "from", "in", "on", "at", "by", "for", "and", "or", "is", "was",
        "are", "were", "what", "which", "how", "many", "much", "does", "did", "do", "this", "that",
        "it", "its", "with", "as", "be", "you", "your", "we", "our", "there", "per",
    };

    public static readonly HashSet<string> Soft = new(StringComparer.Ordinal)
    {
        "avg", "average", "mean", "median", "total", "overall", "gross", "net", "monthly", "annual",
        "yearly", "daily", "number", "count", "amount", "value",
    };

    public static readonly string[] SummaryKeywords =
        { "summarise", "summarize", "summary", "what is this", "overview", "about" };

    public static readonly string[] InsightKeywords =
        { "insight", "finding", "key finding", "takeaway", "conclusion" };
}
