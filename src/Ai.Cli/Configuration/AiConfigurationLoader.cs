using System.Text.Json;

namespace Ai.Cli.Configuration;

public static class AiConfigurationLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static AiConfiguration Load(string path)
    {
        if (!File.Exists(path))
        {
            return new AiConfiguration(null, null, null);
        }

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<AiConfiguration>(json, JsonOptions)
            ?? new AiConfiguration(null, null, null);
    }
}
