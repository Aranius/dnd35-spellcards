using SpellCards.Infrastructure;
using SpellCards.Models;
using SpellCards.Parsing;

namespace SpellCards.Sources;

public sealed class D20SrdSpellSource
{
    private const string BaseUrl = "https://www.d20srd.org/";
    private const string SpellIndexUrl = "https://www.d20srd.org/indexes/spells.htm";

    private readonly HttpCache _cache;

    public D20SrdSpellSource(HttpCache cache) => _cache = cache;

    public async Task<IReadOnlyList<Spell>> FetchSpellsAsync(IReadOnlyList<string> spellNames, CancellationToken ct)
    {
        var indexHtml = await _cache.GetStringCachedAsync(SpellIndexUrl, ct);
        var nameToUrl = D20SrdIndexParser.ParseNameToUrl(indexHtml, BaseUrl);

        var result = new List<Spell>();

        foreach (var name in spellNames)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                var resolvedName = SpellNameResolver.Resolve(name, nameToUrl);
                var url = nameToUrl[resolvedName];

                var html = await _cache.GetStringCachedAsync(url, ct);
                var spell = D20SrdSpellParser.ParseSpellPage(html, url);
                result.Add(spell);
            }
            catch (Exception ex)
            {
                result.Add(BuildNotFoundSpell(name, ex.Message));
            }
        }

        return result;
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
