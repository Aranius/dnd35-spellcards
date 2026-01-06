using System.Text.Json;
using SpellCards.Infrastructure;
using SpellCards.Models;
using SpellCards.Sources.AonPf2;

namespace SpellCards.Sources;

public sealed class AonPf2SpellSource
{
    private const string DefaultElasticUrl = "https://elasticsearch.aonprd.com/aon-test";

    private readonly HttpCache _cache;
    private readonly AonPf2ElasticClient _elastic;

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public AonPf2SpellSource(HttpCache cache, string? elasticUrl = null)
    {
        _cache = cache;
        var url = string.IsNullOrWhiteSpace(elasticUrl) ? DefaultElasticUrl : elasticUrl.Trim();
        _elastic = new AonPf2ElasticClient(cache, url, SerializerOptions);
    }

    public async Task<IReadOnlyList<Spell>> FetchSpellsAsync(IReadOnlyList<string> spellNames, CancellationToken ct)
    {
        var requested = spellNames
            .Select(s => (s ?? string.Empty).Trim().TrimStart('\uFEFF'))
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();

        if (requested.Count == 0)
            return Array.Empty<Spell>();

        var results = new List<Spell>(requested.Count);

        foreach (var name in requested)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                var hits = await SearchSpellsAsync(name, ct).ConfigureAwait(false);

                var nameToUrl = hits
                    .Where(h => !string.IsNullOrWhiteSpace(h.Name) && !string.IsNullOrWhiteSpace(h.Url))
                    .GroupBy(h => h.Name!, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First().Url!, StringComparer.OrdinalIgnoreCase);

                if (nameToUrl.Count == 0)
                    throw new InvalidOperationException($"No PF2 spell results returned for '{name}'.");

                var resolvedName = SpellNameResolver.Resolve(name, nameToUrl);
                var chosen = hits.FirstOrDefault(h => string.Equals(h.Name, resolvedName, StringComparison.OrdinalIgnoreCase))
                             ?? throw new InvalidOperationException($"Resolved '{name}' to '{resolvedName}', but could not locate that hit.");

                var spell = AonPf2SpellMapper.Map(chosen);

                if (!string.IsNullOrWhiteSpace(spell.SourceUrl))
                {
                    var html = await _cache.GetStringCachedAsync(spell.SourceUrl, ct).ConfigureAwait(false);
                    var fullText = AonPf2SpellPageParser.ExtractFullDescription(html);

                    if (!string.IsNullOrWhiteSpace(fullText) && fullText.Length > spell.Description.Length)
                        spell = spell with { Description = fullText };
                }

                results.Add(spell);
            }
            catch (Exception ex)
            {
                results.Add(BuildNotFoundSpell(name, ex.Message));
            }
        }

        return results;
    }

    private async Task<List<AonSpellHit>> SearchSpellsAsync(string query, CancellationToken ct)
    {
        var body = AonPf2QueryBuilder.BuildSpellSearch(query);
        var response = await _elastic.SearchAsync(body, ct).ConfigureAwait(false);

        return response.Hits?.Hits?
                   .Select(h => h.Source)
                   .Where(s => s is not null)
                   .Cast<AonSpellHit>()
                   .ToList()
               ?? new List<AonSpellHit>();
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
}