using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Ai.Cli.Generation;
using Ai.Cli.OpenRouter;

namespace Ai.Cli.Tests;

public sealed class OpenRouterClientTests
{
    [Fact]
    public async Task GetModelIdsAsync_SortsIdsAlphabeticallyIgnoringCase()
    {
        using var handler = new RecordingHandler(
            _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                    {"data":[{"id":"zeta/model"},{"id":"Alpha/model"},{"id":"beta/model"}]}
                    """, Encoding.UTF8, "application/json")
            }));
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://openrouter.ai/") };
        var client = new OpenRouterClient(httpClient);

        var modelIds = await client.GetModelIdsAsync("test-key", CancellationToken.None);

        Assert.Equal(["Alpha/model", "beta/model", "zeta/model"], modelIds);
        Assert.Equal(HttpMethod.Get, handler.LastRequest!.Method);
        Assert.Equal("/api/v1/models", handler.LastRequest.RequestUri!.AbsolutePath);
        Assert.Equal("Bearer", handler.LastRequest.Headers.Authorization?.Scheme);
        Assert.Equal("test-key", handler.LastRequest.Headers.Authorization?.Parameter);
    }

    [Fact]
    public async Task GenerateCommandAsync_PostsPromptAndReturnsTrimmedCommand()
    {
        string? handlerLastBody = null;
        using var handler = new RecordingHandler(async request =>
        {
            handlerLastBody = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                    {"choices":[{"message":{"content":"Get-ChildItem\r\n"}}]}
                    """, Encoding.UTF8, "application/json")
            };
        });
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://openrouter.ai/") };
        var client = new OpenRouterClient(httpClient);

        var command = await client.GenerateCommandAsync(
            new GenerateCommandRequest(
                ApiKey: "test-key",
                ModelId: "openai/test-model",
                Prompt: "Goal: list files"),
            CancellationToken.None);

        Assert.Equal("Get-ChildItem", command);
        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Equal("/api/v1/chat/completions", handler.LastRequest.RequestUri!.AbsolutePath);
        Assert.Equal("Bearer", handler.LastRequest.Headers.Authorization?.Scheme);
        Assert.Equal("test-key", handler.LastRequest.Headers.Authorization?.Parameter);
        Assert.Contains("\"model\":\"openai/test-model\"", handlerLastBody, StringComparison.Ordinal);
        Assert.Contains("Goal: list files", handlerLastBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerateCommandAsync_NormalizesMultiLineContentToASingleLine()
    {
        using var handler = new RecordingHandler(
            _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                    {"choices":[{"message":{"content":"Get-ChildItem\r\n| Sort-Object Name\r\n"}}]}
                    """, Encoding.UTF8, "application/json")
            }));
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://openrouter.ai/") };
        var client = new OpenRouterClient(httpClient);

        var command = await client.GenerateCommandAsync(
            new GenerateCommandRequest(
                ApiKey: "test-key",
                ModelId: "openai/test-model",
                Prompt: "Goal: list files"),
            CancellationToken.None);

        Assert.Equal("Get-ChildItem | Sort-Object Name", command);
    }

    [Fact]
    public async Task GenerateTextAsync_PreservesMultilineContent()
    {
        using var handler = new RecordingHandler(
            _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                    {"choices":[{"message":{"content":"line one\r\nline two"}}]}
                    """, Encoding.UTF8, "application/json")
            }));
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://openrouter.ai/") };
        var client = new OpenRouterClient(httpClient);

        var answer = await client.GenerateTextAsync(
            new GenerateCommandRequest(
                ApiKey: "test-key",
                ModelId: "openai/test-model",
                Prompt: "Question: explain"),
            CancellationToken.None);

        Assert.Equal("line one" + Environment.NewLine + "line two", answer);
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> responder) : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _responder = responder;

        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return _responder(request);
        }
    }
}
