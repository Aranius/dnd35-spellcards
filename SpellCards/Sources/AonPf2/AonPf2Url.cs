namespace SpellCards.Sources.AonPf2;

internal static class AonPf2Url
{
    private const string BaseUrl = "https://2e.aonprd.com/";

    public static string Normalize(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return string.Empty;

        if (Uri.TryCreate(url, UriKind.Absolute, out var abs))
            return abs.ToString();

        return new Uri(new Uri(BaseUrl), url.TrimStart('/')).ToString();
    }
}
