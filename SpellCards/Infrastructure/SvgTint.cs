using System.Text.RegularExpressions;

namespace SpellCards.Infrastructure;

public static class SvgTint
{
    public static string TintSvg(string svg, string hexColor)
    {
        // Normalize hex like "#B22222"
        if (!hexColor.StartsWith("#"))
            hexColor = "#" + hexColor;

        // Ensure SVG has explicit stroke/fill, overriding any existing ones.
        // Strategy:
        // - Replace stroke="..." and fill="..." everywhere (safe for single-color icons)
        // - If missing, add stroke and fill="none" on the root <svg ...> tag
        svg = Regex.Replace(svg, @"stroke=""[^""]*""", $@"stroke=""{hexColor}""", RegexOptions.IgnoreCase);
        svg = Regex.Replace(svg, @"fill=""[^""]*""", @"fill=""none""", RegexOptions.IgnoreCase);

        // Add stroke if it didn’t exist anywhere (common for outline icons)
        if (!Regex.IsMatch(svg, @"\bstroke=""", RegexOptions.IgnoreCase))
        {
            svg = Regex.Replace(svg, @"<svg\b", $@"<svg stroke=""{hexColor}"" fill=""none""", RegexOptions.IgnoreCase);
        }

        return svg;
    }
}
