using System.Diagnostics;
using System.CommandLine;
using Ai.Cli.Configuration;
using Ai.Cli.Generation;
using Ai.Cli.Output;

namespace Ai.Cli;

public sealed class AiApplication(
    IAiApplicationService applicationService,
    IClipboardService clipboardService,
    TextWriter standardOutput,
    TextWriter standardError,
    Func<string>? versionProvider = null)
{
    private readonly IAiApplicationService _applicationService = applicationService;
    private readonly IClipboardService _clipboardService = clipboardService;
    private readonly TextWriter _standardOutput = standardOutput;
    private readonly TextWriter _standardError = standardError;
    private readonly Func<string> _versionProvider = versionProvider ?? BuildVersion.GetDisplayVersion;

    public Task<int> RunAsync(string[] args, CancellationToken cancellationToken)
    {
        if (args.Length == 1 && (string.Equals(args[0], "--version", StringComparison.Ordinal) || string.Equals(args[0], "-v", StringComparison.Ordinal)))
        {
            return WriteVersionAsync();
        }

        var bashOption = new Option<bool>("--bash")
        {
            Description = "Generate a bash command. Shorthand for --shell bash."
        };
        var shellOption = new Option<string?>("--shell")
        {
            Description = "Target shell for command generation (powershell, bash, zsh). Overrides the configured default."
        };
        var modelsOption = new Option<bool>("--models")
        {
            Description = "List available OpenRouter model IDs."
        };
        var modelOption = new Option<string?>("--model")
        {
            Description = "Override the configured OpenRouter model ID."
        };
        var timingOption = new Option<bool>("--timing")
        {
            Description = "Print timing information for the AI call and overall request to stderr."
        };
        var goalArgument = new Argument<string[]>("goal")
        {
            Arity = ArgumentArity.ZeroOrMore,
            Description = "The natural-language goal to translate into a shell command."
        };

        var rootCommand = new RootCommand("Generate shell commands with OpenRouter.")
        {
            bashOption,
            shellOption,
            modelsOption,
            modelOption,
            timingOption,
            goalArgument
        };

        rootCommand.SetAction(async parseResult =>
        {
            var totalStopwatch = Stopwatch.StartNew();
            long? modelsElapsedMilliseconds = null;
            long? aiElapsedMilliseconds = null;
            long? clipboardElapsedMilliseconds = null;
            var timingEnabled = parseResult.GetValue(timingOption);

            try
            {
                if (parseResult.GetValue(modelsOption))
                {
                    var modelsStopwatch = Stopwatch.StartNew();
                    IReadOnlyList<string> models;
                    try
                    {
                        models = await _applicationService.GetModelsAsync(cancellationToken);
                    }
                    finally
                    {
                        modelsStopwatch.Stop();
                        modelsElapsedMilliseconds = modelsStopwatch.ElapsedMilliseconds;
                    }

                    foreach (var model in models)
                    {
                        await _standardOutput.WriteLineAsync(model);
                    }

                    return 0;
                }

                var goalTokens = parseResult.GetValue(goalArgument) ?? [];
                if (goalTokens.Length == 0)
                {
                    await _standardError.WriteLineAsync("A goal is required unless --models is used.");
                    return 1;
                }

                var useBash = parseResult.GetValue(bashOption);
                var shellValue = parseResult.GetValue(shellOption);
                if (useBash && shellValue is not null)
                {
                    await _standardError.WriteLineAsync("--bash and --shell cannot be used together.");
                    return 1;
                }

                ShellTarget? cliShellTarget = null;
                if (useBash)
                {
                    cliShellTarget = ShellTarget.Bash;
                }
                else if (shellValue is not null)
                {
                    if (!Enum.TryParse<ShellTarget>(shellValue, ignoreCase: true, out var parsed))
                    {
                        await _standardError.WriteLineAsync(
                            $"Unknown shell target '{shellValue}'. Valid options: powershell, bash, zsh.");
                        return 1;
                    }

                    cliShellTarget = parsed;
                }

                var aiStopwatch = Stopwatch.StartNew();
                GeneratedCommand generatedCommand;
                try
                {
                    generatedCommand = await _applicationService.GenerateCommandAsync(
                        new GenerateUserCommandRequest(
                            Goal: string.Join(" ", goalTokens),
                            ShellTarget: cliShellTarget,
                            ModelOverride: parseResult.GetValue(modelOption)),
                        cancellationToken);
                }
                finally
                {
                    aiStopwatch.Stop();
                    aiElapsedMilliseconds = aiStopwatch.ElapsedMilliseconds;
                }

                var finalCommand = ShellCommandFormatter.FormatForOutput(
                    generatedCommand.RawCommand, generatedCommand.ShellTarget);

                await _standardOutput.WriteLineAsync(finalCommand);

                var clipboardStopwatch = Stopwatch.StartNew();
                try
                {
                    await _clipboardService.SetTextAsync(finalCommand, cancellationToken);
                }
                catch (Exception exception)
                {
                    await _standardError.WriteLineAsync($"Warning: failed to copy command to clipboard: {exception.Message}");
                }
                finally
                {
                    clipboardStopwatch.Stop();
                    clipboardElapsedMilliseconds = clipboardStopwatch.ElapsedMilliseconds;
                }

                return 0;
            }
            catch (AiConfigurationException exception)
            {
                await _standardError.WriteLineAsync(exception.Message);
                return 1;
            }
            catch (Exception exception)
            {
                await _standardError.WriteLineAsync(exception.Message);
                return 1;
            }
            finally
            {
                totalStopwatch.Stop();

                if (timingEnabled)
                {
                    if (modelsElapsedMilliseconds is not null)
                    {
                        await _standardError.WriteLineAsync($"timing.models_ms={modelsElapsedMilliseconds.Value}");
                    }

                    if (aiElapsedMilliseconds is not null)
                    {
                        await _standardError.WriteLineAsync($"timing.ai_ms={aiElapsedMilliseconds.Value}");
                    }

                    if (clipboardElapsedMilliseconds is not null)
                    {
                        await _standardError.WriteLineAsync($"timing.clipboard_ms={clipboardElapsedMilliseconds.Value}");
                    }

                    await _standardError.WriteLineAsync($"timing.total_ms={totalStopwatch.ElapsedMilliseconds}");
                }
            }
        });

        return rootCommand.Parse(args).InvokeAsync(cancellationToken: cancellationToken);
    }

    async Task<int> WriteVersionAsync()
    {
        await _standardOutput.WriteLineAsync(_versionProvider());
        return 0;
    }
}
