using AngleSharp.Html.Parser;

namespace SpellCards.Parsing;

public static class D20SrdIndexParser
{
    public static IReadOnlyDictionary<string, string> ParseNameToUrl(string indexHtml, string baseUrl)
    {
        var doc = new HtmlParser().ParseDocument(indexHtml);

        var links = doc.QuerySelectorAll("a[href]")
            .Select(a => new
            {
                Text = a.TextContent.Trim(),
                Href = a.GetAttribute("href")?.Trim()
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.Text) && !string.IsNullOrWhiteSpace(x.Href))
            .Where(x => x.Href!.Contains("/srd/spells/", StringComparison.OrdinalIgnoreCase) ||
                        x.Href!.Contains("srd/spells/", StringComparison.OrdinalIgnoreCase))
            .ToList();

        // d20srd index page uses relative links; normalize to absolute
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var l in links)
        {
            var abs = l.Href!.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? l.Href!
                : new Uri(new Uri(baseUrl), l.Href!).ToString();

            if (!map.ContainsKey(l.Text))
                map[l.Text] = abs;
        }

        return map;
    }
}
