using DocNest.Parsers;
using DocNest.Pipeline;
using DocNest.Query;
using DocNest.Retrieval;
using DocNest.Udf;

namespace DocNest.Cli;

/// <summary>
/// The CLI command logic, free of any argument-parsing framework — takes plain parameters + output
/// writers and returns an exit code, so each command is directly unit-testable.
/// </summary>
public static class CliCommands
{
    /// <summary>Parse a document → pipeline → <c>.udf</c>.</summary>
    public static async Task<int> ConvertAsync(
        string source, string? output, bool fast, string quantization,
        TextWriter outWriter, TextWriter errorWriter, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(source))
        {
            await errorWriter.WriteLineAsync($"Error: source not found: {source}").ConfigureAwait(false);
            return 1;
        }

        try
        {
            var factory = new ParserFactory();
            if (!factory.Supports(source))
            {
                await errorWriter.WriteLineAsync($"Error: unsupported format: {Path.GetExtension(source)}").ConfigureAwait(false);
                return 1;
            }

            var raw = await factory.Get(source).ParseAsync(source, cancellationToken).ConfigureAwait(false);
            var document = new DocNestPipeline().Process(raw);
            var outputPath = output ?? Path.ChangeExtension(source, ".udf");
            await new UdfWriter().WriteAsync(document, outputPath, cancellationToken: cancellationToken).ConfigureAwait(false);

            await outWriter.WriteLineAsync($"Wrote {outputPath} ({document.Sections.Count} sections)").ConfigureAwait(false);
            return 0;
        }
        catch (DocNestException ex)
        {
            await errorWriter.WriteLineAsync($"Error: {ex.Message}").ConfigureAwait(false);
            return 1;
        }
    }

    /// <summary>Answer a question over a <c>.udf</c> (or a raw file) via the 5-layer engine.</summary>
    public static async Task<int> QueryAsync(
        string path, string question, bool allowLlm, ILlmProvider? llm, string cacheDir,
        TextWriter outWriter, TextWriter errorWriter, CancellationToken cancellationToken = default)
    {
        try
        {
            Document document;
            if (path.EndsWith(".udf", StringComparison.OrdinalIgnoreCase))
            {
                document = (await UdfReader.LoadAsync(path, cancellationToken: cancellationToken).ConfigureAwait(false)).ToDocument();
            }
            else
            {
                if (!File.Exists(path))
                {
                    await errorWriter.WriteLineAsync($"Error: file not found: {path}").ConfigureAwait(false);
                    return 1;
                }
                var raw = await new ParserFactory().Get(path).ParseAsync(path, cancellationToken).ConfigureAwait(false);
                document = new DocNestPipeline().Process(raw);
            }

            using var retriever = new HybridRetriever(cacheDir);
            var engine = new DocNestQueryEngine(retriever, llm);
            var result = await engine.AnswerAsync(document, question, allowLlm, cancellationToken).ConfigureAwait(false);

            if (result.LayerUsed < 0 || string.IsNullOrEmpty(result.Answer))
            {
                await outWriter.WriteLineAsync("No answer found (try --allow-llm with an LLM provider configured).").ConfigureAwait(false);
            }
            else
            {
                await outWriter.WriteLineAsync(result.Answer).ConfigureAwait(false);
                var citations = result.Citations.Count > 0 ? $", citations: {string.Join(", ", result.Citations)}" : string.Empty;
                await outWriter.WriteLineAsync($"[Layer {result.LayerUsed}, {result.TokensUsed} tokens{citations}]").ConfigureAwait(false);
            }
            return 0;
        }
        catch (DocNestException ex)
        {
            await errorWriter.WriteLineAsync($"Error: {ex.Message}").ConfigureAwait(false);
            return 1;
        }
    }

    /// <summary>Print the catalogue summary for a <c>.udf</c>.</summary>
    public static async Task<int> InfoAsync(
        string udf, TextWriter outWriter, TextWriter errorWriter, CancellationToken cancellationToken = default)
    {
        try
        {
            var package = await UdfReader.LoadAsync(udf, cancellationToken: cancellationToken).ConfigureAwait(false);
            await outWriter.WriteLineAsync($"Title:       {package.Manifest.Title}").ConfigureAwait(false);
            await outWriter.WriteLineAsync($"Format:      {package.Manifest.SourceFormat}").ConfigureAwait(false);
            await outWriter.WriteLineAsync($"UDF version: {package.Manifest.UdfVersion}").ConfigureAwait(false);
            await outWriter.WriteLineAsync($"Sections:    {package.Catalogue.SectionIndex.Count}").ConfigureAwait(false);
            await outWriter.WriteLineAsync($"Key numbers: {package.Catalogue.KeyNumbers.Count}").ConfigureAwait(false);
            if (!string.IsNullOrEmpty(package.Catalogue.Summary))
            {
                await outWriter.WriteLineAsync($"Summary:     {package.Catalogue.Summary}").ConfigureAwait(false);
            }
            return 0;
        }
        catch (DocNestException ex)
        {
            await errorWriter.WriteLineAsync($"Error: {ex.Message}").ConfigureAwait(false);
            return 1;
        }
    }
}
