using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using SpellCards.Models;

namespace SpellCards.Parsing;

public static class D20SrdSpellParser
{
    public static Spell ParseSpellPage(string html, string url)
    {
        var doc = new HtmlParser().ParseDocument(html);

        // 1️⃣ Name
        var name = doc.QuerySelector("h1")?.TextContent.Trim()
            ?? throw new InvalidOperationException($"Spell name not found: {url}");

        // 2️⃣ School (from h4 > a)
        var schoolText = doc.QuerySelector("h4 a")?.TextContent.Trim();
        if (string.IsNullOrWhiteSpace(schoolText))
            throw new InvalidOperationException($"School not found for '{name}'");

        var schoolKey = ToSchoolKey(schoolText);

        // 3️⃣ Stat table
        var table = doc.QuerySelector("table.statBlock")
            ?? throw new InvalidOperationException($"Stat block not found for '{name}'");

        var stats = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in table.QuerySelectorAll("tr"))
        {
            var key = row.QuerySelector("th")?.TextContent.Trim().TrimEnd(':');
            var value = row.QuerySelector("td")?.TextContent.Trim();

            if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(value))
                stats[key] = value;
        }

        string Get(string k) => stats.TryGetValue(k, out var v) ? v : "";

        // 4️⃣ Target / Area / Effect
        var targetOrArea =
            !string.IsNullOrWhiteSpace(Get("Area")) ? $"Area: {Get("Area")}" :
            !string.IsNullOrWhiteSpace(Get("Target")) ? $"Target: {Get("Target")}" :
            !string.IsNullOrWhiteSpace(Get("Effect")) ? $"Effect: {Get("Effect")}" :
            "";

        // 5️⃣ Description (everything after the table)
        var description = ExtractDescriptionAfter(table);

        return new Spell
        {
            Name = name,
            ClassLevel = Get("Level"),
            SchoolText = schoolText,
            SchoolKey = schoolKey,

            Cast = Get("Casting Time"),
            Range = Get("Range"),
            TargetOrArea = targetOrArea,
            Duration = Get("Duration"),
            Save = Get("Saving Throw"),
            Sr = Get("Spell Resistance"),
            Components = Get("Components"),

            Tags = "",
            Description = description,
            SourceUrl = url
        };
    }

    private static string ExtractDescriptionAfter(IElement table)
    {
        var sb = new System.Text.StringBuilder();
        var node = table.NextElementSibling;

        while (node != null)
        {
            if (node.TagName.Equals("H6", StringComparison.OrdinalIgnoreCase) ||
                node.TagName.Equals("P", StringComparison.OrdinalIgnoreCase))
            {
                sb.Append(' ').Append(node.TextContent.Trim());
            }

            node = node.NextElementSibling;
        }

        return sb.ToString().Trim();
    }

    private static string ToSchoolKey(string school)
    {
        var s = school.ToLowerInvariant();
        if (s.Contains("abjuration")) return "abjuration";
        if (s.Contains("conjuration")) return "conjuration";
        if (s.Contains("divination")) return "divination";
        if (s.Contains("enchantment")) return "enchantment";
        if (s.Contains("evocation")) return "evocation";
        if (s.Contains("illusion")) return "illusion";
        if (s.Contains("necromancy")) return "necromancy";
        if (s.Contains("transmutation")) return "transmutation";
        throw new InvalidOperationException($"Unknown school '{school}'");
    }
}
