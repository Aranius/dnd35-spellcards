using System.Text.RegularExpressions;

namespace SpellCards.Processing;

public static class SpellDescriptionFormatter
{
    public static string FormatForCard(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return string.Empty;

        var s = Normalize(description);

        s = ReplaceInlineHeading(s, "Critical Success", "CS:");
        s = ReplaceInlineHeading(s, "Critical Failure", "CF:");
        s = ReplaceInlineHeading(s, "Success", "S:");
        s = ReplaceInlineHeading(s, "Failure", "F:");

        s = BreakBeforeToken(s, "CS:");
        s = BreakBeforeToken(s, "S:");
        s = BreakBeforeToken(s, "F:");
        s = BreakBeforeToken(s, "CF:");

        s = BreakBeforeWord(s, "Heightened", paragraphBreak: true);

        s = Regex.Replace(s, @"\n{3,}", "\n\n");
        return s.Trim();
    }

    private static string Normalize(string text)
    {
        var s = text.Replace("\r", "\n");
        s = Regex.Replace(s, @"[\t ]+", " ");
        s = Regex.Replace(s, @"\n +", "\n");
        s = Regex.Replace(s, @" +\n", "\n");
        return s.Trim();
    }

    private static string ReplaceInlineHeading(string input, string heading, string replacement)
    {
        var pattern = $@"(?i)\b{Regex.Escape(heading)}\b\s*:??\s+";
        return Regex.Replace(input, pattern, replacement + " ", RegexOptions.CultureInvariant);
    }

    private static string BreakBeforeToken(string input, string token)
    {
        var pattern = $@"(?m)(?<=[^\n])\s+{Regex.Escape(token)}\s";
        return Regex.Replace(input, pattern, "\n" + token + " ", RegexOptions.CultureInvariant);
    }

    private static string BreakBeforeWord(string input, string word, bool paragraphBreak)
    {
        var breakStr = paragraphBreak ? "\n\n" : "\n";
        var pattern = $@"(?i)(?<=[^\n])\s+{Regex.Escape(word)}\b";
        return Regex.Replace(input, pattern, breakStr + word, RegexOptions.CultureInvariant);
    }
}
