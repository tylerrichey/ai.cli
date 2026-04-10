using Ai.Cli;
using Ai.Cli.OpenRouter;
using Ai.Cli.Output;

var httpClient = new HttpClient
{
    BaseAddress = new Uri("https://openrouter.ai/")
};

IMarkdownFormatter markdownFormatter = Console.IsOutputRedirected
    ? new PlainMarkdownFormatter()
    : new AnsiMarkdownFormatter();

var application = new AiApplication(
    new DefaultAiApplicationService(new OpenRouterClient(httpClient)),
    new SystemClipboardService(),
    Console.Out,
    Console.Error,
    new ProcessCommandExecutor(),
    markdownFormatter: markdownFormatter);

return await application.RunAsync(args, CancellationToken.None);
