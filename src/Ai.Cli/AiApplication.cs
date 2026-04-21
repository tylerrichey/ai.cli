using System.Diagnostics;
using System.CommandLine;
using Ai.Cli.Configuration;
using Ai.Cli.Generation;
using Ai.Cli.History;
using Ai.Cli.Output;

namespace Ai.Cli;

public sealed class AiApplication(
    IAiApplicationService applicationService,
    IClipboardService clipboardService,
    TextWriter standardOutput,
    TextWriter standardError,
    ICommandExecutor? commandExecutor = null,
    Func<string>? versionProvider = null,
    IMarkdownFormatter? markdownFormatter = null,
    IHistoryService? historyService = null)
{
    private readonly IAiApplicationService _applicationService = applicationService;
    private readonly IClipboardService _clipboardService = clipboardService;
    private readonly TextWriter _standardOutput = standardOutput;
    private readonly TextWriter _standardError = standardError;
    private readonly ICommandExecutor? _commandExecutor = commandExecutor;
    private readonly Func<string> _versionProvider = versionProvider ?? BuildVersion.GetDisplayVersion;
    private readonly IMarkdownFormatter _markdownFormatter = markdownFormatter ?? new PlainMarkdownFormatter();
    private readonly IHistoryService? _historyService = historyService;

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
        var executeOption = new Option<bool>("--execute", ["-x"])
        {
            Description = "Execute the generated command after displaying it and prompting for confirmation."
        };
        var questionOption = new Option<bool>("--question", ["-q"])
        {
            Description = "Ask a question and print the answer instead of generating a command."
        };
        var fileOption = new Option<string[]>("--file", ["-f"])
        {
            Description = "Include up to 3 files in the AI request context."
        };
        var rawOption = new Option<bool>("--raw")
        {
            Description = "Disable markdown formatting on question output."
        };
        var timingOption = new Option<bool>("--timing")
        {
            Description = "Print timing information for the AI call and overall request to stderr."
        };
        var noHistoryOption = new Option<bool>("--no-history", ["-nh"])
        {
            Description = "Do not record this invocation in history."
        };
        var historyOption = new Option<bool>("--history", ["-hs"])
        {
            Description = "Search and display history. Remaining arguments are used as a search term."
        };
        var resumeOption = new Option<bool>("--resume", ["-r"])
        {
            Description = "Continue from the last history entry, sending it as conversation context."
        };
        var goalArgument = new Argument<string[]>("goal")
        {
            Arity = ArgumentArity.ZeroOrMore,
            Description = "The natural-language goal or question to send to the AI."
        };

        var rootCommand = new RootCommand("Generate shell commands or answers with OpenRouter.")
        {
            bashOption,
            shellOption,
            modelsOption,
            modelOption,
            executeOption,
            questionOption,
            fileOption,
            rawOption,
            timingOption,
            noHistoryOption,
            historyOption,
            resumeOption,
            goalArgument
        };

        rootCommand.SetAction(async parseResult =>
        {
            var totalStopwatch = Stopwatch.StartNew();
            long? modelsElapsedMilliseconds = null;
            long? aiElapsedMilliseconds = null;
            long? clipboardElapsedMilliseconds = null;
            long? executeElapsedMilliseconds = null;
            var timingEnabled = parseResult.GetValue(timingOption);

            try
            {
                // History search: --history / -hs [search term from goal tokens]
                if (parseResult.GetValue(historyOption))
                {
                    var searchTokens = parseResult.GetValue(goalArgument) ?? [];
                    var searchTerm = searchTokens.Length > 0 ? string.Join(" ", searchTokens) : null;
                    return await ShowHistoryAsync(searchTerm, cancellationToken);
                }

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
                    await _standardError.WriteLineAsync("A goal or question is required unless --models is used.");
                    return 1;
                }

                var useBash = parseResult.GetValue(bashOption);
                var shellValue = parseResult.GetValue(shellOption);
                var useQuestionMode = parseResult.GetValue(questionOption);
                var useResumeMode = parseResult.GetValue(resumeOption);
                var includedFiles = parseResult.GetValue(fileOption) ?? [];
                var noHistory = parseResult.GetValue(noHistoryOption);

                if (useBash && shellValue is not null)
                {
                    await _standardError.WriteLineAsync("--bash and --shell cannot be used together.");
                    return 1;
                }

                if (useQuestionMode && parseResult.GetValue(executeOption))
                {
                    await _standardError.WriteLineAsync("-q and -x cannot be used together.");
                    return 1;
                }

                if (useQuestionMode && useBash)
                {
                    await _standardError.WriteLineAsync("-q and --bash cannot be used together.");
                    return 1;
                }

                if (useQuestionMode && shellValue is not null)
                {
                    await _standardError.WriteLineAsync("-q and --shell cannot be used together.");
                    return 1;
                }

                if (useResumeMode && useQuestionMode)
                {
                    await _standardError.WriteLineAsync("-r and -q cannot be used together.");
                    return 1;
                }

                if (useResumeMode && parseResult.GetValue(executeOption))
                {
                    await _standardError.WriteLineAsync("-r and -x cannot be used together.");
                    return 1;
                }

                if (useResumeMode && useBash)
                {
                    await _standardError.WriteLineAsync("-r and --bash cannot be used together.");
                    return 1;
                }

                if (useResumeMode && shellValue is not null)
                {
                    await _standardError.WriteLineAsync("-r and --shell cannot be used together.");
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
                try
                {
                    if (useResumeMode)
                    {
                        if (_historyService is null)
                        {
                            await _standardError.WriteLineAsync("History service is not available.");
                            return 1;
                        }

                        var allEntries = await _historyService.SearchAsync(null, cancellationToken);
                        if (allEntries.Count == 0)
                        {
                            await _standardError.WriteLineAsync("No history entries found to resume from.");
                            return 1;
                        }

                        var byId = allEntries.ToDictionary(e => e.Id);
                        var lastEntry = allEntries[0];

                        // Walk the chain oldest-to-newest
                        var chain = new List<HistoryEntry>();
                        var current = lastEntry;
                        while (true)
                        {
                            chain.Insert(0, current);
                            if (!current.ResumedFromId.HasValue || !byId.TryGetValue(current.ResumedFromId.Value, out var prev))
                            {
                                break;
                            }
                            current = prev;
                        }

                        var effectiveKind = DetermineEffectiveKind(chain);

                        var priorMessages = new List<ConversationMessage>();
                        foreach (var entry in chain)
                        {
                            priorMessages.Add(new ConversationMessage("user", entry.Input));
                            priorMessages.Add(new ConversationMessage("assistant", entry.Response));
                        }

                        if (effectiveKind == HistoryEntryKind.Command)
                        {
                            var resumeGeneratedCommand = await _applicationService.GenerateCommandAsync(
                                new GenerateUserCommandRequest(
                                    Goal: string.Join(" ", goalTokens),
                                    ShellTarget: cliShellTarget,
                                    ModelOverride: parseResult.GetValue(modelOption),
                                    IncludedFiles: includedFiles,
                                    PriorMessages: priorMessages),
                                cancellationToken);

                            var resumeFinalCommand = ShellCommandFormatter.FormatForOutput(
                                resumeGeneratedCommand.RawCommand, resumeGeneratedCommand.ShellTarget);

                            await _standardOutput.WriteLineAsync(resumeFinalCommand);

                            var resumeClipboardStopwatch = Stopwatch.StartNew();
                            try
                            {
                                await _clipboardService.SetTextAsync(resumeFinalCommand, cancellationToken);
                            }
                            catch (Exception exception)
                            {
                                await _standardError.WriteLineAsync($"Warning: failed to copy command to clipboard: {exception.Message}");
                            }
                            finally
                            {
                                resumeClipboardStopwatch.Stop();
                                clipboardElapsedMilliseconds = resumeClipboardStopwatch.ElapsedMilliseconds;
                            }

                            if (!noHistory)
                            {
                                await TryRecordHistoryAsync(new HistoryEntry(
                                    Id: Guid.NewGuid(),
                                    Timestamp: DateTimeOffset.UtcNow,
                                    Kind: HistoryEntryKind.Resume,
                                    Input: string.Join(" ", goalTokens),
                                    Response: resumeGeneratedCommand.RawCommand,
                                    ShellTarget: resumeGeneratedCommand.ShellTarget.ToString().ToLowerInvariant(),
                                    ModelId: resumeGeneratedCommand.ModelId,
                                    WorkingDirectory: Directory.GetCurrentDirectory(),
                                    IncludedFiles: includedFiles,
                                    WasExecuted: false,
                                    ResumedFromId: lastEntry.Id,
                                    EffectiveKind: HistoryEntryKind.Command), cancellationToken);
                            }

                            return 0;
                        }

                        var resumeAnswer = await _applicationService.AskQuestionAsync(
                            new AskQuestionRequest(
                                Question: string.Join(" ", goalTokens),
                                ModelOverride: parseResult.GetValue(modelOption),
                                IncludedFiles: includedFiles,
                                PriorMessages: priorMessages),
                            cancellationToken);

                        var useRaw = parseResult.GetValue(rawOption);
                        var resumeFormatted = useRaw ? resumeAnswer.Answer : _markdownFormatter.Format(resumeAnswer.Answer);
                        await _standardOutput.WriteLineAsync(resumeFormatted);

                        if (!noHistory)
                        {
                            await TryRecordHistoryAsync(new HistoryEntry(
                                Id: Guid.NewGuid(),
                                Timestamp: DateTimeOffset.UtcNow,
                                Kind: HistoryEntryKind.Resume,
                                Input: string.Join(" ", goalTokens),
                                Response: resumeAnswer.Answer,
                                ShellTarget: null,
                                ModelId: resumeAnswer.ModelId,
                                WorkingDirectory: Directory.GetCurrentDirectory(),
                                IncludedFiles: includedFiles,
                                WasExecuted: false,
                                ResumedFromId: lastEntry.Id,
                                EffectiveKind: HistoryEntryKind.Question), cancellationToken);
                        }

                        return 0;
                    }

                    if (useQuestionMode)
                    {
                        var answer = await _applicationService.AskQuestionAsync(
                            new AskQuestionRequest(
                                Question: string.Join(" ", goalTokens),
                                ModelOverride: parseResult.GetValue(modelOption),
                                IncludedFiles: includedFiles),
                            cancellationToken);

                        var useRaw = parseResult.GetValue(rawOption);
                        var formatted = useRaw ? answer.Answer : _markdownFormatter.Format(answer.Answer);
                        await _standardOutput.WriteLineAsync(formatted);

                        if (!noHistory)
                        {
                            await TryRecordHistoryAsync(new HistoryEntry(
                                Id: Guid.NewGuid(),
                                Timestamp: DateTimeOffset.UtcNow,
                                Kind: HistoryEntryKind.Question,
                                Input: string.Join(" ", goalTokens),
                                Response: answer.Answer,
                                ShellTarget: null,
                                ModelId: answer.ModelId,
                                WorkingDirectory: Directory.GetCurrentDirectory(),
                                IncludedFiles: includedFiles,
                                WasExecuted: false), cancellationToken);
                        }

                        return 0;
                    }

                    var generatedCommand = await _applicationService.GenerateCommandAsync(
                        new GenerateUserCommandRequest(
                            Goal: string.Join(" ", goalTokens),
                            ShellTarget: cliShellTarget,
                            ModelOverride: parseResult.GetValue(modelOption),
                            IncludedFiles: includedFiles),
                        cancellationToken);

                    if (parseResult.GetValue(executeOption))
                    {
                        await _standardError.WriteLineAsync(generatedCommand.RawCommand);

                        var executor = _commandExecutor ?? new ProcessCommandExecutor();

                        if (!executor.IsInteractive)
                        {
                            await _standardError.WriteLineAsync("Cannot prompt for confirmation: input is not interactive.");
                            return 1;
                        }

                        await _standardError.WriteAsync("Press Enter to execute, any other key to cancel: ");
                        var keyInfo = executor.ReadKey();
                        await _standardError.WriteLineAsync();

                        if (keyInfo.Key != ConsoleKey.Enter)
                        {
                            await _standardError.WriteLineAsync("Cancelled.");

                            if (!noHistory)
                            {
                                await TryRecordHistoryAsync(new HistoryEntry(
                                    Id: Guid.NewGuid(),
                                    Timestamp: DateTimeOffset.UtcNow,
                                    Kind: HistoryEntryKind.Command,
                                    Input: string.Join(" ", goalTokens),
                                    Response: generatedCommand.RawCommand,
                                    ShellTarget: generatedCommand.ShellTarget.ToString().ToLowerInvariant(),
                                    ModelId: generatedCommand.ModelId,
                                    WorkingDirectory: Directory.GetCurrentDirectory(),
                                    IncludedFiles: includedFiles,
                                    WasExecuted: false), cancellationToken);
                            }

                            return 0;
                        }

                        if (!noHistory)
                        {
                            await TryRecordHistoryAsync(new HistoryEntry(
                                Id: Guid.NewGuid(),
                                Timestamp: DateTimeOffset.UtcNow,
                                Kind: HistoryEntryKind.Command,
                                Input: string.Join(" ", goalTokens),
                                Response: generatedCommand.RawCommand,
                                ShellTarget: generatedCommand.ShellTarget.ToString().ToLowerInvariant(),
                                ModelId: generatedCommand.ModelId,
                                WorkingDirectory: Directory.GetCurrentDirectory(),
                                IncludedFiles: includedFiles,
                                WasExecuted: true), cancellationToken);
                        }

                        var (fileName, arguments) = ShellCommandFormatter.GetExecutionCommand(
                            generatedCommand.RawCommand, generatedCommand.ShellTarget);

                        var executeStopwatch = Stopwatch.StartNew();
                        try
                        {
                            return await executor.ExecuteAsync(fileName, arguments, cancellationToken);
                        }
                        finally
                        {
                            executeStopwatch.Stop();
                            executeElapsedMilliseconds = executeStopwatch.ElapsedMilliseconds;
                        }
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

                    if (!noHistory)
                    {
                        await TryRecordHistoryAsync(new HistoryEntry(
                            Id: Guid.NewGuid(),
                            Timestamp: DateTimeOffset.UtcNow,
                            Kind: HistoryEntryKind.Command,
                            Input: string.Join(" ", goalTokens),
                            Response: generatedCommand.RawCommand,
                            ShellTarget: generatedCommand.ShellTarget.ToString().ToLowerInvariant(),
                            ModelId: generatedCommand.ModelId,
                            WorkingDirectory: Directory.GetCurrentDirectory(),
                            IncludedFiles: includedFiles,
                            WasExecuted: false), cancellationToken);
                    }

                    return 0;
                }
                finally
                {
                    aiStopwatch.Stop();
                    aiElapsedMilliseconds = aiStopwatch.ElapsedMilliseconds;
                }
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

                    if (executeElapsedMilliseconds is not null)
                    {
                        await _standardError.WriteLineAsync($"timing.execute_ms={executeElapsedMilliseconds.Value}");
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

    async Task<int> ShowHistoryAsync(string? searchTerm, CancellationToken cancellationToken)
    {
        if (_historyService is null)
        {
            await _standardError.WriteLineAsync("History service is not available.");
            return 1;
        }

        var entries = await _historyService.SearchAsync(
            string.IsNullOrWhiteSpace(searchTerm) ? null : searchTerm,
            cancellationToken);

        if (entries.Count == 0)
        {
            await _standardError.WriteLineAsync("No history entries found.");
            return 0;
        }

        const int maxDisplay = 50;
        const int maxResponseLength = 200;

        var displayed = entries.Take(maxDisplay);
        foreach (var entry in displayed)
        {
            var localTime = entry.Timestamp.ToLocalTime();
            var kindLabel = entry.Kind switch
            {
                HistoryEntryKind.Command => $"command ({entry.ShellTarget})",
                HistoryEntryKind.Resume => "resume",
                _ => "question"
            };

            await _standardOutput.WriteLineAsync(
                $"[{localTime:yyyy-MM-dd HH:mm:ss}] {kindLabel}  model: {entry.ModelId}");
            await _standardOutput.WriteLineAsync($"  {entry.Input}");

            var response = entry.Response.Length > maxResponseLength
                ? string.Concat(entry.Response.AsSpan(0, maxResponseLength), "...")
                : entry.Response;
            var responseLine = response.ReplaceLineEndings(" ");
            await _standardOutput.WriteLineAsync($"  -> {responseLine}");
            await _standardOutput.WriteLineAsync();
        }

        return 0;
    }

    private static HistoryEntryKind DetermineEffectiveKind(List<HistoryEntry> chain)
    {
        for (var i = chain.Count - 1; i >= 0; i--)
        {
            var entry = chain[i];
            if (entry.Kind == HistoryEntryKind.Command)
                return HistoryEntryKind.Command;
            if (entry.Kind == HistoryEntryKind.Question)
                return HistoryEntryKind.Question;
            if (entry.Kind == HistoryEntryKind.Resume && entry.EffectiveKind.HasValue)
                return entry.EffectiveKind.Value;
        }

        return HistoryEntryKind.Question;
    }

    async Task TryRecordHistoryAsync(HistoryEntry entry, CancellationToken cancellationToken)
    {
        if (_historyService is null)
        {
            return;
        }

        try
        {
            await _historyService.RecordAsync(entry, cancellationToken);
        }
        catch (Exception exception)
        {
            await _standardError.WriteLineAsync($"Warning: failed to record history: {exception.Message}");
        }
    }
}
