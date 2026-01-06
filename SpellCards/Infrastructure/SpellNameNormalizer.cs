using System.Text;
using System.Text.RegularExpressions;

namespace SpellCards.Infrastructure;

public static class SpellNameNormalizer
{
    public static string Normalize(string name)
    {
        name = name.ToLowerInvariant();

        // Normalize apostrophes and punctuation
        name = name.Replace("’", "'");

        // Remove non-letter characters except spaces
        name = Regex.Replace(name, @"[^a-z\s]", "");

        // Collapse whitespace
        name = Regex.Replace(name, @"\s+", " ").Trim();

        return name;
    }
}
