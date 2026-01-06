using System.Text.Json;
using System.Text.Json.Serialization;
using Dnd35.SpellCards.Infrastructure;
using Dnd35.SpellCards.Models;

namespace Dnd35.SpellCards.Sources;

public sealed class Open5eSpellSource
{
    private const string ApiBase = "https://api.open5e.com";

    // NOTE: Open5e is paginated. We follow "next" links when needed.
    private const string SearchEndpoint = ApiBase + "/spells/?search=";

    private readonly HttpCache _cache;

    public Open5eSpellSource(HttpCache cache) => _cache = cache;

    public async Task<IReadOnlyList<Spell>> FetchSpellsAsync(IReadOnlyList<string> spellNames, CancellationToken ct)
    {
        var result = new List<Spell>(spellNames.Count);

        foreach (var rawRequested in spellNames)
        {
            var requested = (rawRequested ?? string.Empty).Trim().TrimStart('\uFEFF');
            if (string.IsNullOrWhiteSpace(requested))
                continue;

            var (chosen, firstUrlUsed) = await FetchSingleAsync(requested, ct).ConfigureAwait(false);
            result.Add(MapToSpell(chosen, requested, firstUrlUsed));
        }

        return result;
    }

    private async Task<(Open5eSpellDto Chosen, string FirstUrlUsed)> FetchSingleAsync(string requested, CancellationToken ct)
    {
        // Try to reduce paging issues by requesting a larger page size (server may ignore).
        // We still follow "next" if present.
        var firstUrl = SearchEndpoint + Uri.EscapeDataString(requested) + "&page_size=200";

        var allCandidates = new List<Open5eSpellDto>();
        string? url = firstUrl;
        var pages = 0;

        while (!string.IsNullOrWhiteSpace(url))
        {
            ct.ThrowIfCancellationRequested();

            pages++;
            if (pages > 10)
                break;

            var json = await _cache.GetStringCachedAsync(url, ct).ConfigureAwait(false);
            var response = JsonSerializer.Deserialize<Open5eSearchResponse>(json)
                           ?? throw new InvalidOperationException($"Open5e returned invalid JSON for '{requested}'.");

            var results = response.Results ?? new List<Open5eSpellDto>();
            if (results.Count > 0)
                allCandidates.AddRange(results);

            // Fast-path: exact match found on this page.
            var exactOnPage = results
                .Where(r => !string.IsNullOrWhiteSpace(r.Name) && string.Equals(r.Name, requested, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (exactOnPage.Count == 1)
                return (exactOnPage[0], firstUrl);

            if (exactOnPage.Count > 1)
            {
                var names = string.Join(", ", exactOnPage.Select(x => x.Name).Distinct(StringComparer.OrdinalIgnoreCase).Take(10));
                throw new InvalidOperationException($"Spell '{requested}' is ambiguous in Open5e. Matches: {names}");
            }

            url = response.Next;
        }

        if (allCandidates.Count == 0)
            throw new InvalidOperationException($"Spell '{requested}' not found in Open5e.");

        if (allCandidates.Count == 1)
            return (allCandidates[0], firstUrl);

        var names2 = string.Join(", ", allCandidates.Select(x => x.Name).Where(n => !string.IsNullOrWhiteSpace(n))
            .Distinct(StringComparer.OrdinalIgnoreCase).Take(12));
        throw new InvalidOperationException($"Spell '{requested}' not found exactly in Open5e. Candidates: {names2}");
    }

    private static Spell MapToSpell(Open5eSpellDto s, string requestedName, string searchUrl)
    {
        var tags = new List<string>();
        var isRitual = string.Equals(s.Ritual, "yes", StringComparison.OrdinalIgnoreCase);
        var isConcentration = string.Equals(s.Concentration, "yes", StringComparison.OrdinalIgnoreCase);
        if (isRitual) tags.Add("Ritual");
        if (isConcentration) tags.Add("Concentration");

        var duration = s.Duration?.Trim() ?? string.Empty;
        if (isConcentration && !duration.Contains("concentration", StringComparison.OrdinalIgnoreCase))
            duration = string.IsNullOrWhiteSpace(duration) ? "Concentration" : "Concentration, " + duration;

        var components = NormalizeComponents(s.Components ?? string.Empty);

        var description = (s.Desc ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(s.HigherLevel))
            description += (description.Length == 0 ? string.Empty : "\n\n") + "At Higher Levels. " + s.HigherLevel.Trim();

        var notes = string.IsNullOrWhiteSpace(s.Material) ? string.Empty : $"M: {s.Material.Trim()}";

        var classLevel = BuildClassLevel(s);

        var schoolText = string.IsNullOrWhiteSpace(s.School) ? string.Empty : s.School.Trim();
        var schoolKey = string.IsNullOrWhiteSpace(s.School)
            ? ""
            : s.School.Trim().ToLowerInvariant().Replace(" ", string.Empty);

        var targetOrArea = BuildTargetOrArea(s);

        return new Spell
        {
            Name = s.Name ?? requestedName,
            Part = string.Empty,
            ClassLevel = classLevel,
            SchoolText = schoolText,
            SchoolKey = schoolKey,
            Cast = s.CastingTime?.Trim() ?? string.Empty,
            Range = s.Range?.Trim() ?? string.Empty,
            TargetOrArea = targetOrArea,
            Duration = duration,
            Save = string.Empty,
            Sr = string.Empty,
            Components = components,
            Tags = string.Join(" \u00B7 ", tags),
            Description = description,
            Notes = notes,
            SourceUrl = s.DocumentUrl ?? searchUrl
        };
    }

    private static string NormalizeComponents(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        // Open5e commonly uses "V,S,M". Normalize to "V S M".
        var cleaned = raw.Replace(",", " ");
        return string.Join(" ", cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private static string BuildClassLevel(Open5eSpellDto s)
    {
        var classes = (s.DndClass ?? string.Empty).Trim();
        var firstClass = classes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();

        if (s.LevelInt == 0)
        {
            if (!string.IsNullOrWhiteSpace(firstClass))
                return $"{firstClass} Cantrip";
            return "Cantrip";
        }

        if (!string.IsNullOrWhiteSpace(firstClass))
            return $"{firstClass} {s.LevelInt}";

        return $"Level {s.LevelInt}";
    }

    private static string BuildTargetOrArea(Open5eSpellDto s)
    {
        if (s.AreaOfEffect is null)
            return string.Empty;

        var size = s.AreaOfEffect.Size;
        var type = s.AreaOfEffect.Type;
        if (size is null || string.IsNullOrWhiteSpace(type))
            return string.Empty;

        return $"Area: {size}-foot {type.Trim()}";
    }

    private sealed class Open5eSearchResponse
    {
        [JsonPropertyName("count")]
        public int Count { get; init; }

        [JsonPropertyName("next")]
        public string? Next { get; init; }

        [JsonPropertyName("results")]
        public List<Open5eSpellDto> Results { get; init; } = new();
    }

    private sealed class Open5eSpellDto
    {
        [JsonPropertyName("name")]
        public string? Name { get; init; }

        [JsonPropertyName("desc")]
        public string? Desc { get; init; }

        [JsonPropertyName("higher_level")]
        public string? HigherLevel { get; init; }

        [JsonPropertyName("range")]
        public string? Range { get; init; }

        [JsonPropertyName("components")]
        public string? Components { get; init; }

        [JsonPropertyName("material")]
        public string? Material { get; init; }

        [JsonPropertyName("ritual")]
        public string? Ritual { get; init; }

        [JsonPropertyName("concentration")]
        public string? Concentration { get; init; }

        [JsonPropertyName("casting_time")]
        public string? CastingTime { get; init; }

        [JsonPropertyName("duration")]
        public string? Duration { get; init; }

        [JsonPropertyName("school")]
        public string? School { get; init; }

        [JsonPropertyName("level_int")]
        public int LevelInt { get; init; }

        [JsonPropertyName("dnd_class")]
        public string? DndClass { get; init; }

        [JsonPropertyName("document__url")]
        public string? DocumentUrl { get; init; }

        [JsonPropertyName("area_of_effect")]
        public Open5eAreaOfEffectDto? AreaOfEffect { get; init; }
    }

    private sealed class Open5eAreaOfEffectDto
    {
        [JsonPropertyName("size")]
        public int? Size { get; init; }

        [JsonPropertyName("type")]
        public string? Type { get; init; }
    }
}



