using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Ai.Cli.Generation;

namespace Ai.Cli.OpenRouter;

public sealed class OpenRouterClient(HttpClient httpClient) : IOpenRouterClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient = httpClient;

    public async Task<IReadOnlyList<string>> GetModelIdsAsync(string apiKey, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "api/v1/models");
        AddHeaders(request, apiKey);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        var ids = document.RootElement
            .GetProperty("data")
            .EnumerateArray()
            .Select(model => model.GetProperty("id").GetString())
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Cast<string>()
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return ids;
    }

    public async Task<string> GenerateCommandAsync(GenerateCommandRequest requestModel, CancellationToken cancellationToken)
    {
        var content = await GenerateContentAsync(
            requestModel.ApiKey,
            requestModel.ModelId,
            [new ConversationMessage("user", requestModel.Prompt)],
            cancellationToken);
        return string.Join(
            " ",
            content
                .Split(["\r\n", "\n", "\r"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    public async Task<string> GenerateTextAsync(GenerateCommandRequest requestModel, CancellationToken cancellationToken)
    {
        var content = await GenerateContentAsync(
            requestModel.ApiKey,
            requestModel.ModelId,
            [new ConversationMessage("user", requestModel.Prompt)],
            cancellationToken);
        return NormalizeTextContent(content);
    }

    public async Task<string> GenerateTextWithMessagesAsync(string apiKey, string modelId, IReadOnlyList<ConversationMessage> messages, CancellationToken cancellationToken)
    {
        var content = await GenerateContentAsync(apiKey, modelId, messages, cancellationToken);
        return NormalizeTextContent(content);
    }

    private static string NormalizeTextContent(string content) =>
        content
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal)
            .Replace("\n", Environment.NewLine, StringComparison.Ordinal)
            .TrimEnd();

    private async Task<string> GenerateContentAsync(string apiKey, string modelId, IReadOnlyList<ConversationMessage> messages, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(
            new
            {
                model = modelId,
                messages = messages.Select(m => new { role = m.Role, content = m.Content }).ToArray()
            },
            JsonOptions);

        using var request = new HttpRequestMessage(HttpMethod.Post, "api/v1/chat/completions")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        AddHeaders(request, apiKey);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var content = document.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException("OpenRouter returned an empty command response.");
        }

        return content;
    }

    private static void AddHeaders(HttpRequestMessage request, string apiKey)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Headers.TryAddWithoutValidation("HTTP-Referer", "https://github.com/tyler/ai.cli");
        request.Headers.TryAddWithoutValidation("X-Title", "ai");
    }
}
