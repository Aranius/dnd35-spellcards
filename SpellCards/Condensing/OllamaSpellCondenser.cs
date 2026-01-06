using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using SpellCards.Models;

namespace SpellCards.Condensing;

public sealed class OllamaSpellCondenser : ISpellCondenser, IDisposable
{
    private const int MaxSummaryLength = 900;

    private readonly HttpClient _httpClient;
    private readonly SpellCondensingCache _cache;
    private readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web);
    private readonly string _model;

    public OllamaSpellCondenser(string model, string endpoint, SpellCondensingCache cache)
    {        
        _model = string.IsNullOrWhiteSpace(model) ? "mixtral:8x7b" : model.Trim();
        _cache = cache;

        var baseUri = string.IsNullOrWhiteSpace(endpoint) ? "http://127.0.0.1:11434/" : endpoint.TrimEnd('/') + "/";
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(baseUri),
            Timeout = TimeSpan.FromMinutes(2)
        };
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Dnd35SpellCards", "1.0"));
    }

    public static async Task<bool> IsServiceAvailableAsync(string endpoint, CancellationToken ct)
    {
        var baseUri = string.IsNullOrWhiteSpace(endpoint) ? "http://127.0.0.1:11434/" : endpoint.TrimEnd('/') + "/";

        try
        {
            using var http = new HttpClient
            {
                BaseAddress = new Uri(baseUri),
                Timeout = TimeSpan.FromSeconds(5)
            };

            using var response = await http.GetAsync("api/version", ct).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<string> CondenseAsync(Spell spell, CancellationToken ct)
    {
        if (_cache.TryRead(spell, out var cached))
            return cached;

        var prompt = BuildPrompt(spell);
        var request = new OllamaRequest
        {
            Model = _model,
            Prompt = prompt,
            Stream = false
        };

        var json = JsonSerializer.Serialize(request, _serializerOptions);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var response = await _httpClient.PostAsync("api/generate", content, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            throw new InvalidOperationException($"Ollama returned {(int)response.StatusCode}: {body}");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        var payload = await JsonSerializer.DeserializeAsync<OllamaResponse>(stream, _serializerOptions, ct).ConfigureAwait(false);
        var condensed = Sanitize(payload?.Response);

        if (string.IsNullOrWhiteSpace(condensed))
            return spell.Description;

        _cache.Save(spell, condensed);
        return condensed;
    }

    private static string BuildPrompt(Spell spell)
    {
        var builder = new StringBuilder();
        builder.AppendLine("You are a meticulous Dungeons & Dragons 3.5e rules editor.");
        builder.AppendLine("Rewrite the spell below using only rules text.");
        builder.AppendLine("Keep bonuses, dice, distances, durations, saves, SR, and activation conditions.");
        builder.AppendLine("Remove lore, examples, and repeated explanations.");
        builder.AppendLine("Do not restate metadata (Casting Time, Range, Duration, Save, SR, Components) unless it changes during the effect; focus only on the spell's mechanical description.");
        builder.AppendLine("State any exceptions (good summoned creatures immune, spell resistance can bypass, controller regains command once protection ends).");
        builder.AppendLine("Call out how the barrier behaves (1-ft radius, moves with target, ends if the warded creature attacks through it).");
        builder.AppendLine("Output exactly 6 bullets plus an optional Material Component bullet; every line must start with '- '.");
        builder.AppendLine("Each primary bullet should fall between 35 and 50 words, may include up to three sentences, and must stay rules-focused.");
        builder.AppendLine("Mention spell resistance, save type, and other defensive interactions only once in the bullet where they first matter; never repeat them in later bullets.");
        builder.AppendLine("Add '- Material Component: ...' only if the spell actually lists one, and keep that bullet under 12 words.");
        builder.AppendLine("Ensure the five primary bullets total between 190 and 230 words, and do not add intros, conclusions, or free-form paragraphs.");
        builder.AppendLine();
        builder.AppendLine($"Spell: {spell.Name}");
        builder.AppendLine($"Level: {spell.ClassLevel}");
        builder.AppendLine($"School: {spell.SchoolText}");
        builder.AppendLine($"Casting Time: {spell.Cast}");
        builder.AppendLine($"Range: {spell.Range}");
        if (!string.IsNullOrWhiteSpace(spell.TargetOrArea))
            builder.AppendLine(spell.TargetOrArea);
        builder.AppendLine($"Duration: {spell.Duration}");
        builder.AppendLine($"Saving Throw: {spell.Save}");
        builder.AppendLine($"Spell Resistance: {spell.Sr}");
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

    public void Dispose()
    {
        _httpClient.Dispose();
    }

    private sealed record OllamaRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; init; } = string.Empty;

        [JsonPropertyName("prompt")]
        public string Prompt { get; init; } = string.Empty;

        [JsonPropertyName("stream")]
        public bool Stream { get; init; } = false;

        [JsonPropertyName("options")]
        public OllamaRequestOptions Options { get; init; } = new();
    }

    private sealed record OllamaRequestOptions
    {
        [JsonPropertyName("temperature")]
        public double Temperature { get; init; } = 0.2;

        [JsonPropertyName("top_p")]
        public double TopP { get; init; } = 0.9;

        [JsonPropertyName("repeat_penalty")]
        public double RepeatPenalty { get; init; } = 1.08;

        [JsonPropertyName("num_ctx")]
        public int ContextSize { get; init; } = 2048;
    }

    private sealed record OllamaResponse
    {
        [JsonPropertyName("response")]
        public string? Response { get; init; }
    }
}
