using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DocNest;
using DocNest.Eval;
using DocNest.Parsers;
using DocNest.Pipeline;
using DocNest.Query;
using DocNest.Retrieval;

// ── Same documents + question sets as the Python eval (eval/cases.json) ──────────
// Phase 1 = generated files (xlsx/docx/html/md, exact ground truth)
// Phase 2 = real PDFs (IPCC, BIS, GPT-3, Attention, Llama 2, Constitutional AI)
var casesPath = Path.Combine(AppContext.BaseDirectory, "cases.json");
if (!File.Exists(casesPath))
{
    Console.Error.WriteLine($"cases.json not found at {casesPath}");
    return 2;
}

var jsonOpts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
var cases = JsonSerializer.Deserialize<List<EvalCase>>(await File.ReadAllTextAsync(casesPath), jsonOpts)
            ?? throw new InvalidOperationException("cases.json failed to parse");

var missing = cases.Where(c => !File.Exists(c.File)).Select(c => c.File).ToList();
if (missing.Count > 0)
{
    Console.Error.WriteLine("Missing source files (run the Python eval once to generate them):");
    missing.ForEach(m => Console.Error.WriteLine($"  {m}"));
    return 2;
}

var generated = cases.Where(c => !string.Equals(c.Format, "pdf", StringComparison.OrdinalIgnoreCase)).ToList();
var pdfs = cases.Where(c => string.Equals(c.Format, "pdf", StringComparison.OrdinalIgnoreCase)).ToList();

var work = Path.Combine(Path.GetTempPath(), "docnest-eval-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(work);

// ── LLM provider (Layers 2-4). Both phases use it when a key is present. ─────────
var apiKey = Environment.GetEnvironmentVariable("DOCNEST_LLM_API_KEY");
ILlmProvider? llm = string.IsNullOrEmpty(apiKey)
    ? null
    : new RetryingLlmProvider(new OpenAiCompatibleLlmProvider(
        apiKey,
        Environment.GetEnvironmentVariable("DOCNEST_LLM_MODEL") ?? "gpt-4o-mini",
        Environment.GetEnvironmentVariable("DOCNEST_LLM_BASE_URL") ?? "https://api.openai.com/v1"));
var allowLlm = llm is not null;

// ── Answer judge (Layer scoring). LLM-as-judge when DOCNEST_JUDGE_API_KEY is set, else local. ────
var judge = JudgeFactory.Create();

var report = new StringBuilder();
void Line(string s = "") { Console.WriteLine(s); report.AppendLine(s); }

Line("# DocNest .NET — multi-format RAG accuracy eval (Python parity)");
Line($"_Generated {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC · {cases.Count} documents, " +
     $"{cases.Sum(c => c.Questions.Count)} questions · judge: {judge.Name} (hit = score ≥ 7)_");
Line(allowLlm
    ? $"_Mode: **LLM-assisted** (Layers 2-4 enabled via {Environment.GetEnvironmentVariable("DOCNEST_LLM_MODEL") ?? "gpt-4o-mini"})_"
    : "_Mode: **deterministic floor** (0 LLM tokens — set `DOCNEST_LLM_API_KEY` to enable Layers 2-4)_");
Line();

async Task<(double Avg, double Hit)> RunPhase(string title, List<EvalCase> group)
{
    Line($"## {title}");
    Line();
    Line("| File | Question | Layer | Tokens | Score | Answer |");
    Line("|---|---|---|---|---|---|");

    var fileScores = new Dictionary<string, List<int>>(StringComparer.Ordinal);
    var allScores = new List<int>();
    var totalTokens = 0;

    foreach (var c in group)
    {
        var name = Path.GetFileName(c.File);
        DocNest.Document document;
        try
        {
            var raw = await new ParserFactory().Get(c.File).ParseAsync(c.File);
            document = new DocNestPipeline().Process(raw);
        }
        catch (Exception ex)
        {
            Line($"| {name} | _PARSE FAILED_ | — | 0 | 0/10 | {ex.GetType().Name}: {ex.Message.ReplaceLineEndings(" ")} |");
            fileScores[name] = c.Questions.Select(_ => 0).ToList();
            allScores.AddRange(fileScores[name]);
            continue;
        }

        using var retriever = new HybridRetriever(Path.Combine(work, $"cache_{name}"));
        var engine = new DocNestQueryEngine(retriever, llm);

        foreach (var qa in c.Questions)
        {
            var result = await engine.AnswerAsync(document, qa.Q, allowLlm);
            var (score, _) = await judge.ScoreAsync(qa.Q, result.Answer, qa.Truth);
            fileScores.TryAdd(name, new List<int>());
            fileScores[name].Add(score);
            allScores.Add(score);
            totalTokens += result.TokensUsed;

            var q = qa.Q.Length > 58 ? qa.Q[..58] + "…" : qa.Q;
            var answer = result.Answer.ReplaceLineEndings(" ").Trim();
            if (answer.Length > 70) answer = answer[..70] + "…";
            Line($"| {name} | {q} | {result.LayerUsed} | {result.TokensUsed} | {score}/10 | {answer} |");
        }
    }

    Line();
    Line("| File | Avg score | Hit-rate (≥7) |");
    Line("|---|---|---|");
    foreach (var (name, scores) in fileScores)
    {
        Line($"| {name} | {scores.Average():F1}/10 | {scores.Count(s => s >= 7) / (double)scores.Count:P0} |");
    }
    var avg = allScores.Average();
    var hit = allScores.Count(s => s >= 7) / (double)allScores.Count;
    Line($"| **{title.Split('—')[0].Trim()} overall** | **{avg:F1}/10** | **{hit:P0}** |");
    Line($"\n_{allScores.Count} questions, {totalTokens} LLM tokens._\n");
    return (avg, hit);
}

var p1 = await RunPhase("Phase 1 — generated files (xlsx · docx · html · md)", generated);
var p2 = await RunPhase("Phase 2 — real PDFs (IPCC · BIS · GPT-3 · Attention · Llama 2 · Constitutional AI)", pdfs);

var combinedScores = new List<int>();
// Re-derive a single headline from both phases' weighted question counts.
Line("## Overall");
Line();
Line("| Phase | Avg score | Hit-rate (≥7) |");
Line("|---|---|---|");
Line($"| Phase 1 — generated | {p1.Avg:F1}/10 | {p1.Hit:P0} |");
Line($"| Phase 2 — PDFs | {p2.Avg:F1}/10 | {p2.Hit:P0} |");
var allCount = generated.Sum(c => c.Questions.Count) + pdfs.Sum(c => c.Questions.Count);
var weighted = ((p1.Avg * generated.Sum(c => c.Questions.Count)) + (p2.Avg * pdfs.Sum(c => c.Questions.Count))) / allCount;
Line($"| **All ({allCount} Qs)** | **{weighted:F1}/10** | — |");
Line();

// Write the report next to the solution (best-effort).
var root = Directory.GetCurrentDirectory();
while (root is not null && !File.Exists(Path.Combine(root, "DocNest.sln")))
{
    root = Directory.GetParent(root)?.FullName;
}
if (root is not null)
{
    var resultsDir = Path.Combine(root, "eval", "results");
    Directory.CreateDirectory(resultsDir);
    await File.WriteAllTextAsync(Path.Combine(resultsDir, "report.md"), report.ToString());
    Console.WriteLine($"\nReport written to {Path.Combine(resultsDir, "report.md")}");
}

try { Directory.Delete(work, recursive: true); } catch (IOException) { }

return 0;

namespace DocNest.Eval
{
    internal sealed record EvalCase
    {
        [JsonPropertyName("format")] public string Format { get; init; } = "";
        [JsonPropertyName("file")] public string File { get; init; } = "";
        [JsonPropertyName("questions")] public List<Qa> Questions { get; init; } = new();
    }

    internal sealed record Qa
    {
        [JsonPropertyName("q")] public string Q { get; init; } = "";
        [JsonPropertyName("truth")] public string Truth { get; init; } = "";
    }
}
