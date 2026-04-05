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
        var payload = JsonSerializer.Serialize(
            new
            {
                model = requestModel.ModelId,
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = requestModel.Prompt
                    }
                }
            },
            JsonOptions);

        using var request = new HttpRequestMessage(HttpMethod.Post, "api/v1/chat/completions")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        AddHeaders(request, requestModel.ApiKey);

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

        return string.Join(
            " ",
            content
                .Split(["\r\n", "\n", "\r"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private static void AddHeaders(HttpRequestMessage request, string apiKey)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Headers.TryAddWithoutValidation("HTTP-Referer", "https://github.com/tyler/ai-pwsh");
        request.Headers.TryAddWithoutValidation("X-Title", "ai");
    }
}
