using System.Text;

namespace DocNest.Query;

/// <summary>
/// The 5-layer answer engine: pre-computed (Layer 0) → extractive (Layer 1) → single-section /
/// multi-section / full-document LLM (Layers 2–4). Answers most factual questions at 0 tokens and only
/// escalates to the LLM when needed. Ports the Python <c>reader.py</c> query stack.
/// </summary>
public sealed class DocNestQueryEngine
{
    private const int SectionProseChars = 2000;
    private const int MultiProseChars = 600;
    private const int FullDocChars = 6000;

    private readonly IRetriever _retriever;
    private readonly ILlmProvider? _llm;

    /// <summary>Create the engine. Pass an <see cref="ILlmProvider"/> to enable the LLM layers (2–4).</summary>
    public DocNestQueryEngine(IRetriever retriever, ILlmProvider? llm = null)
    {
        ArgumentNullException.ThrowIfNull(retriever);
        _retriever = retriever;
        _llm = llm;
    }

    /// <summary>Answer <paramref name="question"/> over <paramref name="doc"/>, escalating only as needed.</summary>
    public async Task<QueryResult> AnswerAsync(
        Document doc, string question, bool allowLlm = true, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(doc);
        var q = question.ToLowerInvariant().Trim();

        // ── Layer 0: pre-computed intelligence (0 tokens) ──────────────────────
        var precomputed = Precomputed(doc, q);
        if (precomputed is not null)
        {
            return new QueryResult(precomputed, [], 0, 0, 1.0);
        }

        // ── Layer 1: extractive from the top retrieved section (0 tokens) ──────
        var hits = await _retriever.RetrieveAsync(doc, question, 8, cancellationToken).ConfigureAwait(false);
        if (hits.Count > 0)
        {
            var top = hits[0].Section;
            if (!string.IsNullOrEmpty(top.Summary))
            {
                return new QueryResult(top.Summary!, new[] { top.Id }, 1, 0, 0.7);
            }
            var extract = Extractive.BestSentences(top.Text, question);
            if (extract.Length > 0)
            {
                return new QueryResult(extract, new[] { top.Id }, 1, 0, 0.6);
            }
        }

        if (!allowLlm || _llm is null)
        {
            return new QueryResult(string.Empty, [], -1, 0, 0.0);
        }

        // ── Layer 2: single-section LLM ────────────────────────────────────────
        if (hits.Count > 0)
        {
            var top = hits[0].Section;
            var body = SectionBody(top);
            if (body.Trim().Length > 0)
            {
                var prompt =
                    $"Answer the question using ONLY the section below. If the answer is not in the section, " +
                    $"say 'Not found in {top.Id}'.\n\nSection {top.Id}:\n{body}\n\nQuestion: {question}";
                var answer = await _llm.CompleteAsync(prompt, cancellationToken: cancellationToken).ConfigureAwait(false);
                return new QueryResult(answer, new[] { top.Id }, 2, Tokens(prompt, answer), 0.7);
            }
        }

        // ── Layer 3: multi-section synthesis ───────────────────────────────────
        if (hits.Count >= 2)
        {
            var sections = hits.Take(3).Select(h => h.Section).ToList();
            var context = string.Join("\n\n", sections.Select(s => $"[{s.Id}]\n{Truncate(s.Text, MultiProseChars)}"));
            var prompt = $"Synthesise an answer from the sections below.\n\n{context}\n\nQuestion: {question}";
            var answer = await _llm.CompleteAsync(prompt, cancellationToken: cancellationToken).ConfigureAwait(false);
            return new QueryResult(answer, sections.Select(s => s.Id).ToList(), 3, Tokens(prompt, answer), 0.6);
        }

        // ── Layer 4: full-document fallback ────────────────────────────────────
        var fullText = string.Join("\n\n", doc.Sections.Select(s => $"{s.Title}\n{s.Text}"));
        var fullPrompt = $"Using the document below, answer: {question}\n\nDocument:\n{Truncate(fullText, FullDocChars)}";
        var fullAnswer = await _llm.CompleteAsync(fullPrompt, cancellationToken: cancellationToken).ConfigureAwait(false);
        return new QueryResult(fullAnswer, [], 4, Tokens(fullPrompt, fullAnswer), 0.4);
    }

    private static string? Precomputed(Document doc, string q)
    {
        if (QueryConstants.SummaryKeywords.Any(kw => q.Contains(kw, StringComparison.Ordinal)))
        {
            return string.IsNullOrEmpty(doc.Summary) ? null : doc.Summary;
        }
        if (QueryConstants.InsightKeywords.Any(kw => q.Contains(kw, StringComparison.Ordinal)))
        {
            return doc.Insights.Count > 0 ? string.Join("\n", doc.Insights.Select(i => $"• {i}")) : null;
        }
        var keyNumber = KeyNumberMatcher.Match(q, doc.KeyNumbers);
        if (keyNumber is not null)
        {
            var unit = string.IsNullOrEmpty(keyNumber.Unit) ? string.Empty : $" {keyNumber.Unit}";
            return $"{keyNumber.Label}: {keyNumber.Value}{unit} (source: {keyNumber.Section})";
        }
        return null;
    }

    private static string SectionBody(Section section)
    {
        var body = new StringBuilder(Truncate(section.Text, SectionProseChars));
        foreach (var table in section.Tables)
        {
            body.Append("\n\nTable ").Append(table.TableId).Append(":\n");
            body.Append(string.Join(" | ", table.Headers)).Append('\n');
            foreach (var row in table.Rows)
            {
                body.Append(string.Join(" | ", row)).Append('\n');
            }
        }
        return body.ToString();
    }

    private static int Tokens(string prompt, string answer) => WordCount(prompt) + WordCount(answer);

    private static int WordCount(string text)
        => string.IsNullOrEmpty(text) ? 0 : text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;

    private static string Truncate(string text, int max) => text.Length <= max ? text : text[..max];
}
