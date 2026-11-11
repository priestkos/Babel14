using System.Net;
using System.Net.Http;
using Babel14.Services;
using Xunit;

namespace Babel14.Tests;

/// <summary>What actually goes over the wire: URL, auth header and body.</summary>
public class LlmRequestTests
{
    private sealed class Capture : HttpMessageHandler
    {
        public HttpRequestMessage? Request;
        public string Body = "";
        public HttpStatusCode Status = HttpStatusCode.OK;
        public string Reply = """{"choices":[{"message":{"content":"ok"}}]}""";

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Request = request;
            Body = await request.Content!.ReadAsStringAsync(ct);
            return new HttpResponseMessage(Status) { Content = new StringContent(Reply) };
        }
    }

    [Theory]
    [InlineData("https://openrouter.ai/api/v1", "https://openrouter.ai/api/v1/chat/completions")]
    [InlineData("https://openrouter.ai/api/v1///", "https://openrouter.ai/api/v1/chat/completions")]
    [InlineData("http://localhost:11434/v1", "http://localhost:11434/v1/chat/completions")]
    [InlineData("", "https://openrouter.ai/api/v1/chat/completions")]
    [InlineData("   ", "https://openrouter.ai/api/v1/chat/completions")]
    public async Task PostsToChatCompletionsUnderTheBaseUrl(string baseUrl, string expected)
    {
        var h = new Capture();
        using var llm = new LlmClient(h);
        llm.Configure(baseUrl, "k");
        await llm.TranslateAsync("m", "s", "hello");
        Assert.Equal(HttpMethod.Post, h.Request!.Method);
        Assert.Equal(expected, h.Request.RequestUri!.ToString());
    }

    [Fact]
    public async Task KeyGoesOnlyInTheBearerHeader()
    {
        var h = new Capture();
        using var llm = new LlmClient(h);
        llm.Configure("https://example.invalid/v1", "sk-secret");
        await llm.TranslateAsync("model-x", "SYS", "hello there");

        Assert.Equal("Bearer", h.Request!.Headers.Authorization!.Scheme);
        Assert.Equal("sk-secret", h.Request.Headers.Authorization.Parameter);
        Assert.DoesNotContain("sk-secret", h.Body);
        Assert.Contains("\"model\":\"model-x\"", h.Body);
        Assert.Contains("\"role\":\"system\",\"content\":\"SYS\"", h.Body);
        Assert.Contains("\"role\":\"user\",\"content\":\"hello there\"", h.Body);
    }

    [Fact]
    public async Task NoKeyMeansNoAuthHeader()
    {
        var h = new Capture();
        using var llm = new LlmClient(h);
        llm.Configure("https://example.invalid/v1", "");
        await llm.TranslateAsync("m", "s", "hello");
        Assert.Null(h.Request!.Headers.Authorization);
    }

    [Fact]
    public async Task NonSuccessStatusThrowsWithCodeButNotTheKey()
    {
        var h = new Capture { Status = HttpStatusCode.Unauthorized, Reply = new string('x', 1000) };
        using var llm = new LlmClient(h);
        llm.Configure("https://example.invalid/v1", "sk-secret");
        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => llm.TranslateAsync("m", "s", "hello"));
        Assert.StartsWith("LLM API 401: ", ex.Message);
        Assert.DoesNotContain("sk-secret", ex.Message);
        Assert.True(ex.Message.Length < 400, "provider body must be truncated");
    }

    [Fact]
    public async Task TestWithoutAKeySendsNothing()
    {
        var h = new Capture();
        using var llm = new LlmClient(h);
        await Assert.ThrowsAsync<InvalidOperationException>(() => llm.TestAsync("m"));
        Assert.Null(h.Request);
    }
}
