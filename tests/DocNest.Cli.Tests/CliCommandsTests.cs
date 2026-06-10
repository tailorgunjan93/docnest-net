using System;
using System.IO;
using System.Threading.Tasks;
using DocNest.Cli;
using DocNest.Query;
using FluentAssertions;
using Xunit;

namespace DocNest.Cli.Tests;

public class CliCommandsTests
{
    [Fact]
    public async Task Convert_then_info_then_query_round_trips()
    {
        var dir = Path.Combine(Path.GetTempPath(), "docnest-cli-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var md = Path.Combine(dir, "sample.md");
            await File.WriteAllTextAsync(md, "# Service Report\n\n## Reliability\n\nUptime: 99.9% over the quarter.\n");
            var udf = Path.Combine(dir, "sample.udf");
            var error = new StringWriter();

            var convertOut = new StringWriter();
            (await CliCommands.ConvertAsync(md, udf, true, "float16", convertOut, error)).Should().Be(0);
            File.Exists(udf).Should().BeTrue();
            convertOut.ToString().Should().Contain("Wrote");

            var infoOut = new StringWriter();
            (await CliCommands.InfoAsync(udf, infoOut, error)).Should().Be(0);
            infoOut.ToString().Should().Contain("Sections:");

            var queryOut = new StringWriter();
            var exit = await CliCommands.QueryAsync(udf, "what is the uptime?", true, null, Path.Combine(dir, "cache"), queryOut, error);
            exit.Should().Be(0);
            queryOut.ToString().Should().Contain("99.9%");
            queryOut.ToString().Should().Contain("Layer 0");
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch (IOException) { }
        }
    }

    [Fact]
    public async Task Convert_missing_source_returns_error_exit_code()
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var exit = await CliCommands.ConvertAsync("does-not-exist.md", null, true, "float16", output, error);
        exit.Should().Be(1);
        error.ToString().Should().Contain("not found");
    }

    [Fact]
    public void LlmProviderFactory_creates_provider_from_flags_or_null()
    {
        LlmProviderFactory.Create("openai", "gpt-4o-mini", "key", "https://x/v1")
            .Should().BeOfType<OpenAiCompatibleLlmProvider>();
        LlmProviderFactory.Create("anthropic", "claude-3-5-haiku-latest", "key", "https://x")
            .Should().BeOfType<AnthropicLlmProvider>();
        LlmProviderFactory.Create(provider: null, model: null, apiKey: null, baseUrl: null)
            .Should().BeNull();
    }
}
