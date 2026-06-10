using System.CommandLine;
using System.CommandLine.Invocation;
using DocNest.Cli;

// ── convert ────────────────────────────────────────────────────────────────
var sourceArg = new Argument<string>("source", "File to convert (.md/.html/.csv/.docx/.xlsx/.pdf)");
var outputOpt = new Option<string?>(new[] { "--output", "-o" }, "Output .udf path (default: alongside source)");
var fastOpt = new Option<bool>(new[] { "--fast", "-f" }, "Skip heavy enrichment (default behaviour)");
var quantOpt = new Option<string>(new[] { "--quantization", "-q" }, () => "float16", "Embedding compression: float32|float16|int8|binary");

var convert = new Command("convert", "Convert a document to a .udf knowledge base.")
{
    sourceArg, outputOpt, fastOpt, quantOpt,
};
convert.SetHandler(async ctx =>
{
    ctx.ExitCode = await CliCommands.ConvertAsync(
        ctx.ParseResult.GetValueForArgument(sourceArg),
        ctx.ParseResult.GetValueForOption(outputOpt),
        ctx.ParseResult.GetValueForOption(fastOpt),
        ctx.ParseResult.GetValueForOption(quantOpt) ?? "float16",
        Console.Out, Console.Error, ctx.GetCancellationToken());
});

// ── query ──────────────────────────────────────────────────────────────────
var pathArg = new Argument<string>("path", "A .udf file or a source document");
var questionArg = new Argument<string>("question", "The question to answer");
var allowLlmOpt = new Option<bool>("--allow-llm", () => true, "Allow LLM layers (2-4) when deterministic layers can't answer");
var providerOpt = new Option<string?>("--provider", "LLM provider: openai | anthropic");
var modelOpt = new Option<string?>("--model", "LLM model name");
var baseUrlOpt = new Option<string?>("--base-url", "LLM base URL (OpenAI-compatible endpoints)");
var apiKeyOpt = new Option<string?>("--api-key", "LLM API key (or env DOCNEST_LLM_API_KEY)");
var cacheOpt = new Option<string>("--cache-dir", () => ".docnest_cache", "Retrieval cache directory");

var query = new Command("query", "Answer a question over a .udf or document (5-layer RAG).")
{
    pathArg, questionArg, allowLlmOpt, providerOpt, modelOpt, baseUrlOpt, apiKeyOpt, cacheOpt,
};
query.SetHandler(async ctx =>
{
    var llm = LlmProviderFactory.Create(
        ctx.ParseResult.GetValueForOption(providerOpt),
        ctx.ParseResult.GetValueForOption(modelOpt),
        ctx.ParseResult.GetValueForOption(apiKeyOpt),
        ctx.ParseResult.GetValueForOption(baseUrlOpt));
    ctx.ExitCode = await CliCommands.QueryAsync(
        ctx.ParseResult.GetValueForArgument(pathArg),
        ctx.ParseResult.GetValueForArgument(questionArg),
        ctx.ParseResult.GetValueForOption(allowLlmOpt),
        llm,
        ctx.ParseResult.GetValueForOption(cacheOpt) ?? ".docnest_cache",
        Console.Out, Console.Error, ctx.GetCancellationToken());
});

// ── info ───────────────────────────────────────────────────────────────────
var infoPathArg = new Argument<string>("udf", "A .udf file");
var info = new Command("info", "Show a .udf catalogue summary.") { infoPathArg };
info.SetHandler(async ctx =>
{
    ctx.ExitCode = await CliCommands.InfoAsync(
        ctx.ParseResult.GetValueForArgument(infoPathArg), Console.Out, Console.Error, ctx.GetCancellationToken());
});

var root = new RootCommand("DocNest — the document normalisation engine RAG has always needed.")
{
    convert, query, info,
};
return await root.InvokeAsync(args);
