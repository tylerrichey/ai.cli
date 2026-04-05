using TextCopy;

namespace Ai.Cli.Output;

public sealed class SystemClipboardService : IClipboardService
{
    public Task SetTextAsync(string text, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ClipboardService.SetText(text);
        return Task.CompletedTask;
    }
}
