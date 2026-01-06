using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace SpellCards.Rendering;

public static class SchoolTheme
{
    public static string Color(string schoolKey)
    {
        if (string.IsNullOrWhiteSpace(schoolKey))
            return Colors.Black;

        return schoolKey.ToLowerInvariant() switch
        {
            "abjuration" => "#3A6EA5",
            "conjuration" => "#3A8F5C",
            "divination" => "#C9A227",
            "enchantment" => "#7A4FA3",
            "evocation" => "#B22222",
            "illusion" => "#4B5FA5",
            "necromancy" => "#444444",
            "transmutation" => "#C6862C",
            _ => Colors.Black
        };
    }

    public static string IconPath(string schoolKey)
    {
        if (string.IsNullOrWhiteSpace(schoolKey))
            return string.Empty;

        return Path.Combine(AppContext.BaseDirectory, $"assets/icons/{schoolKey}.svg");
    }

}
