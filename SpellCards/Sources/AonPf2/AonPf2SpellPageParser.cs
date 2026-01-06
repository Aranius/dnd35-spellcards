using System.Text;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace SpellCards.Sources.AonPf2;

internal static class AonPf2SpellPageParser
{
    public static string ExtractFullDescription(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return string.Empty;

        var doc = new HtmlParser().ParseDocument(html);

        var root = doc.QuerySelector("#main") ?? doc.Body;
        if (root is null)
            return string.Empty;

        foreach (var el in root.QuerySelectorAll("script,style,nethys-search"))
            el.Remove();

        var spellSpan = root.QuerySelector("span h1.title")?.Closest("span")
                      ?? root.QuerySelector("h1.title")?.Closest("span")
                      ?? root.QuerySelector("span")
                      ?? root;

        if (spellSpan is null)
            return string.Empty;

        var hr = spellSpan.QuerySelector("hr");
        if (hr is null)
            return NormalizeWhitespace(GetMetaDescription(doc));

        var sb = new StringBuilder();

        for (var node = hr.NextSibling; node is not null; node = node.NextSibling)
        {
            if (node is IElement el)
            {
                var tag = el.TagName;
                if (tag.Equals("SCRIPT", StringComparison.OrdinalIgnoreCase) ||
                    tag.Equals("STYLE", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (tag.Equals("UL", StringComparison.OrdinalIgnoreCase) && !el.Children.Any())
                    continue;

                var text = el.TextContent?.Trim() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(text))
                    sb.AppendLine(text);

                continue;
            }

            if (node is IText textNode)
            {
                var text = textNode.Text?.Trim() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(text))
                    sb.AppendLine(text);
            }
        }

        var result = NormalizeWhitespace(sb.ToString());
        return string.IsNullOrWhiteSpace(result) ? NormalizeWhitespace(GetMetaDescription(doc)) : result;
    }

    private static string GetMetaDescription(AngleSharp.Dom.IDocument doc)
        => doc.QuerySelector("meta[name='description']")?.GetAttribute("content")?.Trim() ?? string.Empty;

    private static string NormalizeWhitespace(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        text = text.Replace("\r\n", "\n").Replace("\r", "\n");

        while (text.Contains("\n\n\n", StringComparison.Ordinal))
            text = text.Replace("\n\n\n", "\n\n", StringComparison.Ordinal);

        return text.Trim();
    }
}
