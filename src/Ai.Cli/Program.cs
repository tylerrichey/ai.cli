using Ai.Cli;
using Ai.Cli.OpenRouter;
using Ai.Cli.Output;

var httpClient = new HttpClient
{
    BaseAddress = new Uri("https://openrouter.ai/")
};

var application = new AiApplication(
    new DefaultAiApplicationService(new OpenRouterClient(httpClient)),
    new SystemClipboardService(),
    Console.Out,
    Console.Error);

return await application.RunAsync(args, CancellationToken.None);
