namespace Ai.Cli.Output;

public interface IClipboardService
{
    Task SetTextAsync(string text, CancellationToken cancellationToken);
}
