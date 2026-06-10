using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using DocNest;
using DocNest.Query;
using FluentAssertions;
using Xunit;

namespace DocNest.Query.Tests;

public class LlmProviderTests
{
    [Fact]
    public async Task OpenAi_posts_chat_completions_and_parses_content()
    {
        var stub = new StubHandler(HttpStatusCode.OK,
            """{"choices":[{"message":{"role":"assistant","content":"Hello!"}}]}""");
        var provider = new OpenAiCompatibleLlmProvider("key", "gpt-4o-mini", "https://example.com/v1", new HttpClient(stub));

        var answer = await provider.CompleteAsync("hi");

        answer.Should().Be("Hello!");
        stub.LastRequestUri.Should().EndWith("/chat/completions");
        stub.LastBody.Should().Contain("\"model\":\"gpt-4o-mini\"").And.Contain("\"role\":\"user\"");
    }

    [Fact]
    public async Task OpenAi_non_success_maps_to_intelligence_exception()
    {
        var provider = new OpenAiCompatibleLlmProvider("k", "m", "https://x/v1",
            new HttpClient(new StubHandler(HttpStatusCode.InternalServerError, "boom")));

        var act = async () => await provider.CompleteAsync("hi");

        await act.Should().ThrowAsync<IntelligenceException>();
    }

    [Fact]
    public async Task Anthropic_posts_messages_and_parses_text()
    {
        var stub = new StubHandler(HttpStatusCode.OK,
            """{"content":[{"type":"text","text":"Hi there"}]}""");
        var provider = new AnthropicLlmProvider("key", "claude-3-haiku", "https://example.com", new HttpClient(stub));

        var answer = await provider.CompleteAsync("hi");

        answer.Should().Be("Hi there");
        stub.LastRequestUri.Should().EndWith("/v1/messages");
        stub.LastBody.Should().Contain("\"model\":\"claude-3-haiku\"").And.Contain("\"max_tokens\"");
    }

    [SkippableFact]
    public async Task Real_endpoint_returns_text_when_configured()
    {
        var apiKey = Environment.GetEnvironmentVariable("DOCNEST_LLM_API_KEY");
        Skip.If(string.IsNullOrEmpty(apiKey), "DOCNEST_LLM_API_KEY not set — real-endpoint LLM test skipped.");

        var baseUrl = Environment.GetEnvironmentVariable("DOCNEST_LLM_BASE_URL") ?? "https://api.openai.com/v1";
        var model = Environment.GetEnvironmentVariable("DOCNEST_LLM_MODEL") ?? "gpt-4o-mini";
        var provider = new OpenAiCompatibleLlmProvider(apiKey!, model, baseUrl);

        var answer = await provider.CompleteAsync("Reply with the single word: ok");
        answer.Should().NotBeNullOrWhiteSpace();
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _body;
        public string? LastRequestUri { get; private set; }
        public string? LastBody { get; private set; }

        public StubHandler(HttpStatusCode status, string body)
        {
            _status = status;
            _body = body;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri?.ToString();
            if (request.Content is not null)
            {
                LastBody = await request.Content.ReadAsStringAsync(cancellationToken);
            }
            return new HttpResponseMessage(_status) { Content = new StringContent(_body) };
        }
    }
}
