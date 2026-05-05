using Ai.Cli;
using Ai.Cli.Configuration;
using Ai.Cli.History;
using Ai.Cli.OpenRouter;
using Ai.Cli.Output;

IMarkdownFormatter markdownFormatter = Console.IsOutputRedirected
    ? new PlainMarkdownFormatter()
    : new AnsiMarkdownFormatter();

var operatingSystem = OperatingSystem.IsWindows() ? OperatingSystemKind.Windows
    : OperatingSystem.IsMacOS() ? OperatingSystemKind.MacOS
    : OperatingSystemKind.Linux;

var userProfile = Environment.GetEnvironmentVariable("USERPROFILE");
var xdgConfigHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
var homeDirectory = Environment.GetEnvironmentVariable("HOME");

var configPath = ConfigFileLocator.GetConfigPath(
    operatingSystem,
    userProfile,
    xdgConfigHome,
    homeDirectory);

var configuration = AiConfigurationLoader.Load(configPath);
var baseUrl = ConfigurationResolver.ResolveBaseUrl(
    configuration,
    Environment.GetEnvironmentVariable("OPENROUTER_BASE_URL"));

var httpClient = new HttpClient
{
    BaseAddress = baseUrl
};

var historyPath = ConfigFileLocator.GetHistoryPath(
    operatingSystem,
    userProfile,
    xdgConfigHome,
    homeDirectory);

var application = new AiApplication(
    new DefaultAiApplicationService(new OpenRouterClient(httpClient)),
    new SystemClipboardService(),
    Console.Out,
    Console.Error,
    new ProcessCommandExecutor(),
    markdownFormatter: markdownFormatter,
    historyService: new JsonlHistoryService(historyPath),
    configurationProvider: () => configuration);

return await application.RunAsync(args, CancellationToken.None);
