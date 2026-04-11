using Ai.Cli;
using Ai.Cli.Configuration;
using Ai.Cli.History;
using Ai.Cli.OpenRouter;
using Ai.Cli.Output;

var httpClient = new HttpClient
{
    BaseAddress = new Uri("https://openrouter.ai/")
};

IMarkdownFormatter markdownFormatter = Console.IsOutputRedirected
    ? new PlainMarkdownFormatter()
    : new AnsiMarkdownFormatter();

var operatingSystem = OperatingSystem.IsWindows() ? OperatingSystemKind.Windows
    : OperatingSystem.IsMacOS() ? OperatingSystemKind.MacOS
    : OperatingSystemKind.Linux;

var historyPath = ConfigFileLocator.GetHistoryPath(
    operatingSystem,
    userProfile: Environment.GetEnvironmentVariable("USERPROFILE"),
    xdgConfigHome: Environment.GetEnvironmentVariable("XDG_CONFIG_HOME"),
    homeDirectory: Environment.GetEnvironmentVariable("HOME"));

var application = new AiApplication(
    new DefaultAiApplicationService(new OpenRouterClient(httpClient)),
    new SystemClipboardService(),
    Console.Out,
    Console.Error,
    new ProcessCommandExecutor(),
    markdownFormatter: markdownFormatter,
    historyService: new JsonlHistoryService(historyPath));

return await application.RunAsync(args, CancellationToken.None);
