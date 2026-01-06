using System.Text.Json;

namespace SpellCards.Infrastructure;

internal sealed record SpellCardsSettings
{
    public OllamaSettings Ollama { get; init; } = new();
    public LlmSettings Llm { get; init; } = new();

    public static SpellCardsSettings? Load(string path)
    {
        if (!File.Exists(path))
            return null;

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<SpellCardsSettings>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        }
        catch
        {
            return null;
        }
    }
}

internal sealed record OllamaSettings
{
    public string? Model { get; init; }
    public string? Endpoint { get; init; }
}

internal sealed record LlmSettings
{
    public string? Endpoint { get; init; }
    public string? ApiKey { get; init; }
    public string? Model { get; init; }
}
