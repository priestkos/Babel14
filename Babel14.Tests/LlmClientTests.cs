using System.Net;
using System.Net.Http;
using Babel14.Services;
using Xunit;

namespace Babel14.Tests;

public class LlmClientTests
{
    private sealed class StubHandler(Func<CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => send(ct);
    }

    private static LlmClient Client(string body) =>
        new(new StubHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body),
        })));

    [Fact]
    public async Task OkBodyReturnsTrimmedContent()
    {
        using var llm = Client("""{"choices":[{"message":{"content":"  hola  "}}]}""");
        Assert.Equal("hola", await llm.TranslateAsync("m", "s", "hello"));
    }

    [Fact]
    public async Task ErrorObjectInOkBodySurfacesProviderMessage()
    {
        using var llm = Client("""{"error":{"message":"Rate limit exceeded","code":429}}""");
        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => llm.TranslateAsync("m", "s", "hello"));
        Assert.Contains("Rate limit exceeded", ex.Message);
    }

    [Theory]
    [InlineData("""{"choices":[]}""")]
    [InlineData("""{"id":"x"}""")]
    public async Task MissingOrEmptyChoicesSaysSo(string body)
    {
        using var llm = Client(body);
        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => llm.TranslateAsync("m", "s", "hello"));
        Assert.Contains("no choices", ex.Message);
    }

    [Fact]
    public async Task UnrequestedCancellationIsReportedAsTimeout()
    {
        // What HttpClient.Timeout produces: a cancellation the caller's token did not ask for.
        using var llm = new LlmClient(new StubHandler(_ =>
            Task.FromException<HttpResponseMessage>(new TaskCanceledException("timeout", new TimeoutException()))));
        var ex = await Assert.ThrowsAsync<TimeoutException>(() => llm.TranslateAsync("m", "s", "hello"));
        Assert.Contains("timed out", ex.Message);
    }

    [Fact]
    public async Task CallerCancellationStaysCancellation()
    {
        using var cts = new CancellationTokenSource();
        using var llm = new LlmClient(new StubHandler(async ct =>
        {
            cts.Cancel();
            await Task.Delay(Timeout.Infinite, ct);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => llm.TranslateAsync("m", "s", "hello", cts.Token));
    }
}
