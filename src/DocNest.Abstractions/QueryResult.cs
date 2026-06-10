namespace DocNest;

/// <summary>
/// The result of answering a question over a document via the layered answer engine.
/// <paramref name="LayerUsed"/>: 0 = pre-computed, 1 = extractive, 2–4 = LLM, -1 = no answer.
/// </summary>
public sealed record QueryResult(
    string Answer,
    IReadOnlyList<string> Citations,
    int LayerUsed,
    int TokensUsed,
    double Confidence);
