using System.Text;

namespace DocNest.Query;

/// <summary>
/// The 5-layer answer engine: pre-computed (Layer 0) → extractive (Layer 1) → single-section /
/// multi-section / full-document LLM (Layers 2–4). Answers most factual questions at 0 tokens and only
/// escalates to the LLM when needed. Ports the Python <c>reader.py</c> query stack.
/// </summary>
public sealed class DocNestQueryEngine
{
    private const int SectionProseChars = 2500;
    private const int MultiSectionCount = 5;      // sections fed to Layer-3 synthesis (was 3)
    private const int MultiProseChars = 1400;     // chars per section in Layer 3 (was 600 — too small)
    private const int FallbackSectionCount = 8;   // retrieved sections fed to the Layer-4 fallback
    private const int FallbackProseChars = 1600;  // chars per section in the Layer-4 fallback
    private const int FullDocChars = 12000;
    private const int AnswerMaxTokens = 1500;     // room for reasoning models (gpt-oss) to emit the answer

    // Escalation thresholds on the [0,1] query-term-recall confidence (ADR-0011). Calibrated on the
    // multi-format eval: above L1 → trust the deterministic 0-token answer; below it escalate to the
    // LLM; below L2 the top section is too weak for a single-section answer → multi/full-doc.
    private const double L1Threshold = 0.6;
    private const double L2Threshold = 0.2;

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

        // ── Retrieve, then gate escalation on an absolute confidence (ADR-0011) ─
        var hits = await _retriever.RetrieveAsync(doc, question, 8, cancellationToken).ConfigureAwait(false);
        var confidence = hits.Count > 0 ? Confidence.Of(question, hits[0].Section) : 0.0;

        // ── Layer 1: confident deterministic answer from the top section (0 tokens) ─
        // Skip for enumeration/explanation questions — a single extractive snippet can't answer "list
        // all X" / "compare" / "what does it say about"; those need LLM synthesis over reranked context.
        if (hits.Count > 0 && confidence >= L1Threshold && !IsComplexQuestion(q))
        {
            var top = hits[0].Section;
            if (!string.IsNullOrEmpty(top.Summary))
            {
                return new QueryResult(top.Summary!, new[] { top.Id }, 1, 0, confidence);
            }
            var extract = Extractive.BestSentences(top.Text, question);
            if (extract.Length > 0)
            {
                return new QueryResult(extract, new[] { top.Id }, 1, 0, confidence);
            }
            // Confident but nothing extractable → fall through to the LLM layers.
        }

        if (!allowLlm || _llm is null)
        {
            return new QueryResult(string.Empty, [], -1, 0, 0.0);
        }

        // ── Layer 2: single-section LLM (top section confident enough to trust) ──
        if (hits.Count > 0 && confidence >= L2Threshold)
        {
            var top = hits[0].Section;
            var body = SectionBody(top);
            if (body.Trim().Length > 0)
            {
                var prompt =
                    $"Answer the question using ONLY the section below. If the answer is not in the section, " +
                    $"say 'Not found in {top.Id}'.\n\nSection {top.Id}:\n{body}\n\nQuestion: {question}";
                var answer = await _llm.CompleteAsync(prompt, maxTokens: AnswerMaxTokens, cancellationToken: cancellationToken).ConfigureAwait(false);
                // If the top section didn't contain the answer (refusal/empty), escalate to multi-section
                // synthesis rather than returning a dead end — the right section is often at rank 2-3.
                if (!(IsRefusalOrEmpty(answer) && hits.Count >= 2))
                {
                    return new QueryResult(answer, new[] { top.Id }, 2, Tokens(prompt, answer), confidence);
                }
            }
        }

        // ── Layer 3: multi-section synthesis ───────────────────────────────────
        if (hits.Count >= 2)
        {
            var sections = hits.Take(MultiSectionCount).Select(h => h.Section).ToList();
            var context = string.Join("\n\n", sections.Select(s => $"[{s.Id}] {s.Title}\n{Truncate(s.Text, MultiProseChars)}"));
            var prompt =
                "Answer the question using the document sections below. They are excerpts from a larger " +
                "document — synthesise across them and give the best supported answer. Only say the answer " +
                "is unavailable if it genuinely cannot be found in any section." + EnumerationHint(q) + "\n\n" +
                $"{context}\n\nQuestion: {question}";
            var answer = await _llm.CompleteAsync(prompt, maxTokens: AnswerMaxTokens, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (!IsRefusalOrEmpty(answer))
            {
                return new QueryResult(answer, sections.Select(s => s.Id).ToList(), 3, Tokens(prompt, answer), 0.6);
            }
            // Refusal/empty → broaden the context in Layer 4 rather than returning a dead end.
        }

        // ── Layer 4: broad fallback over all retrieved sections (or the document) ─
        string fallbackContext;
        IReadOnlyList<string> fallbackCitations;
        if (hits.Count > 0)
        {
            var sections = hits.Take(FallbackSectionCount).Select(h => h.Section).ToList();
            fallbackContext = string.Join("\n\n", sections.Select(s => $"[{s.Id}] {s.Title}\n{Truncate(s.Text, FallbackProseChars)}"));
            fallbackCitations = sections.Select(s => s.Id).ToList();
        }
        else
        {
            var fullText = string.Join("\n\n", doc.Sections.Select(s => $"{s.Title}\n{s.Text}"));
            fallbackContext = Truncate(fullText, FullDocChars);
            fallbackCitations = [];
        }
        var fullPrompt =
            "Answer the question using the document context below. Synthesise across the excerpts and give " +
            "the best supported answer; only say it is unavailable if it truly cannot be found." + EnumerationHint(q) + "\n\n" +
            $"{fallbackContext}\n\nQuestion: {question}";
        var fullAnswer = await _llm.CompleteAsync(fullPrompt, maxTokens: AnswerMaxTokens, cancellationToken: cancellationToken).ConfigureAwait(false);
        return new QueryResult(fullAnswer, fallbackCitations, 4, Tokens(fullPrompt, fullAnswer), 0.4);
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
        // A single key-number can't answer an enumeration/explanation question (e.g. "what are the
        // training corpora") — skip it so the question reaches retrieval + the LLM. Prevents misfires
        // like "corpora: 2" short-circuiting a list question at Layer 0.
        var keyNumber = IsComplexQuestion(q) ? null : KeyNumberMatcher.Match(q, doc.KeyNumbers);
        if (keyNumber is not null)
        {
            var unit = string.IsNullOrEmpty(keyNumber.Unit) ? string.Empty : $" {keyNumber.Unit}";
            return $"{keyNumber.Label}: {keyNumber.Value}{unit} (source: {keyNumber.Section})";
        }
        return null;
    }

    // Enumeration / comparison / explanation questions that a single number or extractive snippet cannot
    // answer — these must reach the LLM over reranked context, not short-circuit at Layer 0/1.
    private static readonly string[] ComplexMarkers =
    {
        " all ", "list ", " every ", "what are", "compare", "how does", "how do ", "say about",
        "explain", "describe", "discuss", "differ", "versus", " vs ",
    };

    private static bool IsComplexQuestion(string q)
        => ComplexMarkers.Any(m => q.Contains(m, StringComparison.Ordinal));

    // Enumeration questions ("list ALL X", "what are the Y") need a complete list — a judge scores a
    // partial enumeration as wrong, so instruct the narrator to emit every matching item.
    private static readonly string[] EnumerationMarkers = { " all ", "list ", " every ", "what are" };

    private static string EnumerationHint(string q)
        => EnumerationMarkers.Any(m => q.Contains(m, StringComparison.Ordinal))
            ? " This is an enumeration question: list EVERY matching item found across the sections " +
              "(every size, name, phase, dataset, etc.); do not omit any."
            : string.Empty;

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

    // Markers a conservative narrator leads with when it can't answer from the given context.
    private static readonly string[] RefusalMarkers =
    {
        "not found", "don't contain", "do not contain", "does not contain", "doesn't contain",
        "didn't contain", "don't include", "do not include", "doesn't include", "does not include",
        "no information", "i'm sorry", "i am sorry", "cannot find", "could not find", "couldn't find",
        "no relevant information", "not provided in", "isn't provided", "is not provided", "no mention",
    };

    /// <summary>
    /// True if an LLM answer is empty or a refusal — i.e. the model declined for lack of context.
    /// Checked over the answer's opening (genuine refusals lead with the disclaimer; real answers lead
    /// with the fact), so a valid answer that merely mentions "does not contain" later is not flagged.
    /// </summary>
    private static bool IsRefusalOrEmpty(string answer)
    {
        if (string.IsNullOrWhiteSpace(answer))
        {
            return true;
        }
        var head = (answer.Length <= 160 ? answer : answer[..160]).ToLowerInvariant();
        return RefusalMarkers.Any(m => head.Contains(m, StringComparison.Ordinal));
    }
}
