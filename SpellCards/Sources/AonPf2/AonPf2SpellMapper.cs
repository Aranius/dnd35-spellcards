using SpellCards.Models;

namespace SpellCards.Sources.AonPf2;

internal static class AonPf2SpellMapper
{
    public static Spell Map(AonSpellHit hit)
    {
        var traditions = hit.Tradition?.Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t.Trim()).ToList()
                         ?? new List<string>();

        var traditionsText = traditions.Count == 0 ? string.Empty : string.Join(", ", traditions);
        var classLevel = hit.Level is null
            ? "Spell"
            : $"Rank {hit.Level}{(string.IsNullOrWhiteSpace(traditionsText) ? "" : $" ({traditionsText})")}";

        var cast = hit.Actions?.Trim() ?? string.Empty;
        var range = hit.RangeRaw?.Trim() ?? string.Empty;

        var targetOrArea =
            !string.IsNullOrWhiteSpace(hit.Target) ? $"Target: {hit.Target.Trim()}" :
            !string.IsNullOrWhiteSpace(hit.Area) ? $"Area: {hit.Area.Trim()}" :
            !string.IsNullOrWhiteSpace(hit.Effect) ? $"Effect: {hit.Effect.Trim()}" :
            string.Empty;

        var duration = hit.DurationRaw?.Trim() ?? string.Empty;
        var save = hit.SavingThrow?.Trim() ?? (hit.Defense?.Trim() ?? string.Empty);

        var traits = hit.Trait?.Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t.Trim()).ToList()
                     ?? new List<string>();

        var tags = traits.Count == 0 ? string.Empty : string.Join(" \u00B7 ", traits);

        var url = AonPf2Url.Normalize(hit.Url);

        var (schoolKey, schoolText) = MapPf2CategoryToSchool(traditions, traits, url);

        return new Spell
        {
            Name = hit.Name?.Trim() ?? "(unknown spell)",
            Part = string.Empty,

            ClassLevel = classLevel,
            SchoolText = schoolText,
            SchoolKey = schoolKey,

            Cast = cast,
            Range = range,
            TargetOrArea = targetOrArea,
            Duration = duration,
            Save = save,
            Sr = string.Empty,
            Components = string.Empty,

            Tags = tags,
            Description = (hit.Summary ?? string.Empty).Trim(),
            Notes = string.Empty,
            SourceUrl = url
        };
    }

    private static (string SchoolKey, string SchoolText) MapPf2CategoryToSchool(
        IReadOnlyList<string> traditions,
        IReadOnlyList<string> traits,
        string? sourceUrl)
    {
        var isRitual = traits.Any(t => t.Equals("Ritual", StringComparison.OrdinalIgnoreCase))
                       || (!string.IsNullOrWhiteSpace(sourceUrl) && sourceUrl.Contains("Rituals.aspx", StringComparison.OrdinalIgnoreCase));
        if (isRitual)
            return ("conjuration", "Ritual");

        var isFocus = traits.Any(t => t.Equals("Focus", StringComparison.OrdinalIgnoreCase))
                      || traits.Any(t => t.Equals("Focus Spell", StringComparison.OrdinalIgnoreCase))
                      || (!string.IsNullOrWhiteSpace(sourceUrl) && sourceUrl.Contains("Focus=", StringComparison.OrdinalIgnoreCase));
        if (isFocus)
            return ("divination", "Focus");

        var primary = traditions.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(primary))
            return (string.Empty, string.Empty);

        return primary.Trim().ToLowerInvariant() switch
        {
            "arcane" => ("evocation", "Arcane"),
            "divine" => ("abjuration", "Divine"),
            "elemental" => ("conjuration", "Elemental"),
            "occult" => ("enchantment", "Occult"),
            "primal" => ("transmutation", "Primal"),
            _ => (string.Empty, string.Empty)
        };
    }
}
