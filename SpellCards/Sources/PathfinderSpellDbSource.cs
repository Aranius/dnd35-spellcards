using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using SpellCards.Infrastructure;
using SpellCards.Models;

namespace SpellCards.Sources;

public sealed class PathfinderSpellDbSource
{
    private const string ApiBase = "https://api.pathfinderspelldb.com";
    private const string SpellsEndpoint = ApiBase + "/spells";

    private readonly HttpCache _cache;

    public PathfinderSpellDbSource(HttpCache cache) => _cache = cache;

    public async Task<IReadOnlyList<Spell>> FetchSpellsAsync(IReadOnlyList<string> spellNames, CancellationToken ct)
    {
        var requested = spellNames
            .Select(s => (s ?? string.Empty).Trim().TrimStart('\uFEFF'))
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();

        if (requested.Count == 0)
            return Array.Empty<Spell>();

        // 1) Download full index (cached)
        var indexJson = await _cache.GetStringCachedAsync(SpellsEndpoint, ct).ConfigureAwait(false);
        var index = JsonSerializer.Deserialize<List<SpellIndexDto>>(indexJson, SerializerOptions)
                    ?? throw new InvalidOperationException("PathfinderSpellDb returned invalid JSON for /spells.");

        if (index.Count == 0)
            throw new InvalidOperationException("PathfinderSpellDb returned no spells from /spells.");

        // Build name -> id map (use first occurrence if duplicates)
        var nameToId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in index)
        {
            if (string.IsNullOrWhiteSpace(s.Name))
                continue;

            if (!nameToId.ContainsKey(s.Name))
                nameToId[s.Name] = s.Id;
        }

        var results = new List<Spell>(requested.Count);

        // 2) Resolve each name, then fetch full detail by id
        foreach (var name in requested)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                var resolvedName = ResolveNameOrThrow(name, nameToId);
                var id = nameToId[resolvedName];

                var detailsUrl = $"{SpellsEndpoint}/{id}";
                var detailsJson = await _cache.GetStringCachedAsync(detailsUrl, ct).ConfigureAwait(false);

                var dto = JsonSerializer.Deserialize<SpellDetailDto>(detailsJson, SerializerOptions)
                          ?? throw new InvalidOperationException($"PathfinderSpellDb returned invalid JSON for '{resolvedName}' ({detailsUrl}).");

                results.Add(MapToSpell(dto, detailsUrl));
            }
            catch (Exception ex)
            {
                results.Add(BuildNotFoundSpell(name, ex.Message));
            }
        }

        return results;
    }

    private static string ResolveNameOrThrow(string requestedName, IReadOnlyDictionary<string, int> nameToId)
    {
        // Exact match
        if (nameToId.ContainsKey(requestedName))
            return requestedName;

        // Normalized / fuzzy match using existing infrastructure
        var nameToFakeUrl = nameToId.Keys.ToDictionary(k => k, k => "id:" + nameToId[k], StringComparer.OrdinalIgnoreCase);
        return SpellNameResolver.Resolve(requestedName, nameToFakeUrl);
    }

    private static Spell MapToSpell(SpellDetailDto s, string sourceUrl)
    {
        var classLevel = BuildClassLevel(s);
        var schoolText = BuildSchoolText(s);
        var schoolKey = BuildSchoolKey(s);

        return new Spell
        {
            Name = s.Name ?? $"Spell #{s.Id}",
            Part = string.Empty,

            ClassLevel = classLevel,
            SchoolText = schoolText,
            SchoolKey = schoolKey,

            Cast = s.CastingTime?.Trim() ?? string.Empty,
            Range = s.Range?.Trim() ?? string.Empty,
            TargetOrArea = string.IsNullOrWhiteSpace(s.Area) ? string.Empty : $"Area: {s.Area.Trim()}",
            Duration = s.Duration?.Trim() ?? string.Empty,
            Save = s.SavingThrows?.Trim() ?? string.Empty,
            Sr = (s.SpellResistance ?? false) ? "Yes" : "No",
            Components = NormalizeComponents(s.Components),

            Tags = string.Empty,
            Description = HtmlToPlainText((s.Description ?? string.Empty).Trim()),
            Notes = string.Empty,
            SourceUrl = sourceUrl
        };
    }

    private static string BuildSchoolText(SpellDetailDto s)
    {
        var school = s.School?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(s.Subschool))
            return school;

        return string.IsNullOrWhiteSpace(school)
            ? $"[{s.Subschool!.Trim()}]"
            : $"{school} [{s.Subschool!.Trim()}]";
    }

    private static string BuildSchoolKey(SpellDetailDto s)
    {
        var school = s.School?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(school))
            return string.Empty;

        // Keep consistent with existing rendering: lower + remove spaces
        return school.ToLowerInvariant().Replace(" ", string.Empty);
    }

    private static string BuildClassLevel(SpellDetailDto s)
    {
        // Prefer a single-line "Class 1, Wizard 2" etc.
        // Use the first class in the list as the primary class.
        var first = s.ClassSpellLevels?.FirstOrDefault();
        if (first is null)
            return "";

        var name = first.ClassName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
            return first.Level is null ? "" : $"Level {first.Level}";

        if (first.Level is null)
            return name;

        return $"{name} {first.Level}";
    }

    private static string NormalizeComponents(IReadOnlyList<ComponentDto>? raw)
    {
        if (raw is null || raw.Count == 0)
            return string.Empty;

        // API provides components as list of { name, abbr }
        // Output: "V S M" etc.
        var parts = raw
            .Select(c => (c.Abbr ?? c.Name ?? string.Empty).Trim())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();

        return string.Join(" ", parts);
    }

    private static string HtmlToPlainText(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return string.Empty;

        // The API returns HTML snippets. QuestPDF expects plain text.
        // Keep it minimal: decode entities and strip tags.
        var decoded = WebUtility.HtmlDecode(html);

        // Replace common block tags with newlines before stripping remaining tags.
        decoded = decoded
            .Replace("<br>", "\n", StringComparison.OrdinalIgnoreCase)
            .Replace("<br/>", "\n", StringComparison.OrdinalIgnoreCase)
            .Replace("<br />", "\n", StringComparison.OrdinalIgnoreCase)
            .Replace("</p>", "\n\n", StringComparison.OrdinalIgnoreCase)
            .Replace("</div>", "\n", StringComparison.OrdinalIgnoreCase)
            .Replace("</li>", "\n", StringComparison.OrdinalIgnoreCase);

        // Strip tags
        var sb = new System.Text.StringBuilder(decoded.Length);
        var insideTag = false;
        foreach (var ch in decoded)
        {
            if (ch == '<')
            {
                insideTag = true;
                continue;
            }

            if (ch == '>')
            {
                insideTag = false;
                continue;
            }

            if (!insideTag)
                sb.Append(ch);
        }

        // Normalize whitespace
        var text = sb.ToString();
        text = text.Replace("\r\n", "\n").Replace("\r", "\n");

        // Collapse excessive blank lines
        while (text.Contains("\n\n\n", StringComparison.Ordinal))
            text = text.Replace("\n\n\n", "\n\n", StringComparison.Ordinal);

        return text.Trim();
    }

    private static Spell BuildNotFoundSpell(string requestedName, string reason)
    {
        var msg = string.IsNullOrWhiteSpace(reason)
            ? "Spell was not found. Please check spelling / spell name."
            : $"Spell was not found. Please check spelling / spell name.\n\nDetails: {reason}";

        return new Spell
        {
            Name = string.IsNullOrWhiteSpace(requestedName) ? "(unknown spell)" : requestedName,
            Part = string.Empty,
            ClassLevel = "",
            SchoolText = "",
            SchoolKey = "",
            Cast = "",
            Range = "",
            TargetOrArea = "",
            Duration = "",
            Save = "",
            Sr = "",
            Components = "",
            Tags = "Not Found",
            Description = msg,
            Notes = "",
            SourceUrl = ""
        };
    }

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private sealed class SpellIndexDto
    {
        [JsonPropertyName("id")]
        public int Id { get; init; }

        [JsonPropertyName("name")]
        public string? Name { get; init; }
    }

    private sealed class SpellDetailDto
    {
        [JsonPropertyName("id")]
        public int Id { get; init; }

        [JsonPropertyName("name")]
        public string? Name { get; init; }

        [JsonPropertyName("school")]
        public string? School { get; init; }

        [JsonPropertyName("subschool")]
        public string? Subschool { get; init; }

        [JsonPropertyName("castingTime")]
        public string? CastingTime { get; init; }

        [JsonPropertyName("components")]
        public List<ComponentDto>? Components { get; init; }

        [JsonPropertyName("range")]
        public string? Range { get; init; }

        [JsonPropertyName("area")]
        public string? Area { get; init; }

        [JsonPropertyName("duration")]
        public string? Duration { get; init; }

        [JsonPropertyName("savingThrows")]
        public string? SavingThrows { get; init; }

        [JsonPropertyName("spellResistance")]
        public bool? SpellResistance { get; init; }

        [JsonPropertyName("description")]
        public string? Description { get; init; }

        [JsonPropertyName("classSpellLevels")]
        public List<ClassSpellLevelDto>? ClassSpellLevels { get; init; }
    }

    private sealed class ComponentDto
    {
        [JsonPropertyName("name")]
        public string? Name { get; init; }

        [JsonPropertyName("abbr")]
        public string? Abbr { get; init; }
    }

    private sealed class ClassSpellLevelDto
    {
        [JsonPropertyName("className")]
        public string? ClassName { get; init; }

        [JsonPropertyName("level")]
        public int? Level { get; init; }
    }
}
