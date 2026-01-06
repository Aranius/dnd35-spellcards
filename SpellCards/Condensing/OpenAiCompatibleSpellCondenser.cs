using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using SpellCards.Models;

namespace SpellCards.Condensing;

public sealed class OpenAiCompatibleSpellCondenser : ISpellCondenser, IDisposable
{
    private const int MaxSummaryLength = 900;

    private readonly HttpClient _httpClient;
    private readonly SpellCondensingCache _cache;
    private readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web);

    private readonly string _model;

    public OpenAiCompatibleSpellCondenser(string model, string endpoint, string apiKey, SpellCondensingCache cache)
    {
        _model = string.IsNullOrWhiteSpace(model) ? "gpt-4o-mini" : model.Trim();
        _cache = cache;

        if (string.IsNullOrWhiteSpace(endpoint))
            throw new ArgumentException("LLM endpoint is required.", nameof(endpoint));

        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException("LLM API key is required.", nameof(apiKey));

        var baseUri = endpoint.TrimEnd('/') + "/";
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(baseUri),
            Timeout = TimeSpan.FromMinutes(2)
        };

        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Dnd35SpellCards", "1.0"));
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());
    }

    public async Task<string> CondenseAsync(Spell spell, CancellationToken ct)
    {
        if (_cache.TryRead(spell, out var cached))
            return cached;

        var prompt = BuildPrompt(spell);

        var request = new ChatCompletionsRequest
        {
            Model = _model,
            Temperature = 0.2,
            TopP = 0.9,
            Messages =
            [
                new ChatMessage("system", "You are a meticulous Dungeons & Dragons rules editor."),
                new ChatMessage("user", prompt)
            ]
        };

        var json = JsonSerializer.Serialize(request, _serializerOptions);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var response = await _httpClient.PostAsync("v1/chat/completions", content, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            throw new InvalidOperationException($"LLM returned {(int)response.StatusCode}: {body}");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        var payload = await JsonSerializer.DeserializeAsync<ChatCompletionsResponse>(stream, _serializerOptions, ct).ConfigureAwait(false);

        var condensed = Sanitize(payload?.Choices?.FirstOrDefault()?.Message?.Content);

        if (string.IsNullOrWhiteSpace(condensed))
            return spell.Description;

        _cache.Save(spell, condensed);
        return condensed;
    }

    private static string BuildPrompt(Spell spell)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Rewrite the spell below using only rules text.");
        builder.AppendLine("Keep bonuses, dice, distances, durations, saves, spell resistance, and activation conditions.");
        builder.AppendLine("Remove lore, examples, and repeated explanations.");
        builder.AppendLine("Do not restate metadata (Casting Time, Range, Duration, Save, SR, Components) unless it changes during the effect.");
        builder.AppendLine("Be concise; preserve mechanics.");
        builder.AppendLine();
        builder.AppendLine($"Spell: {spell.Name}");
        builder.AppendLine($"Level: {spell.ClassLevel}");
        if (!string.IsNullOrWhiteSpace(spell.SchoolText))
            builder.AppendLine($"School: {spell.SchoolText}");
        if (!string.IsNullOrWhiteSpace(spell.Cast))
            builder.AppendLine($"Casting Time: {spell.Cast}");
        if (!string.IsNullOrWhiteSpace(spell.Range))
            builder.AppendLine($"Range: {spell.Range}");
        if (!string.IsNullOrWhiteSpace(spell.TargetOrArea))
            builder.AppendLine(spell.TargetOrArea);
        if (!string.IsNullOrWhiteSpace(spell.Duration))
            builder.AppendLine($"Duration: {spell.Duration}");
        if (!string.IsNullOrWhiteSpace(spell.Save))
            builder.AppendLine($"Saving Throw: {spell.Save}");
        if (!string.IsNullOrWhiteSpace(spell.Sr))
            builder.AppendLine($"Spell Resistance: {spell.Sr}");
        if (!string.IsNullOrWhiteSpace(spell.Components))
            builder.AppendLine($"Components: {spell.Components}");
        builder.AppendLine("Original description:");
        builder.AppendLine(spell.Description.Trim());
        builder.AppendLine();
        builder.AppendLine("Condensed rules summary:");

        return builder.ToString();
    }

    private static string Sanitize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var cleaned = text.Replace("Condensed rules summary:", string.Empty, StringComparison.OrdinalIgnoreCase)
                          .Replace("Original description:", string.Empty, StringComparison.OrdinalIgnoreCase)
                          .Trim();

        if (cleaned.Length > MaxSummaryLength)
            cleaned = cleaned[..MaxSummaryLength].TrimEnd();

        return cleaned;
    }

    public void Dispose() => _httpClient.Dispose();

    private sealed record ChatCompletionsRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; init; } = string.Empty;

        [JsonPropertyName("messages")]
        public List<ChatMessage> Messages { get; init; } = new();

        [JsonPropertyName("temperature")]
        public double Temperature { get; init; } = 0.2;

        [JsonPropertyName("top_p")]
        public double TopP { get; init; } = 0.9;
    }

    private sealed record ChatMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);

    private sealed record ChatCompletionsResponse
    {
        [JsonPropertyName("choices")]
        public List<Choice>? Choices { get; init; }

        public sealed record Choice
        {
            [JsonPropertyName("message")]
            public ChoiceMessage? Message { get; init; }
        }

        public sealed record ChoiceMessage
        {
            [JsonPropertyName("content")]
            public string? Content { get; init; }
        }
    }
}
