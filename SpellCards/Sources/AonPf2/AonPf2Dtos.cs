using System.Text.Json.Serialization;

namespace SpellCards.Sources.AonPf2;

internal sealed class AonSearchResponse
{
    [JsonPropertyName("hits")]
    public AonHitsContainer? Hits { get; init; }
}

internal sealed class AonHitsContainer
{
    [JsonPropertyName("hits")]
    public List<AonHitWrapper>? Hits { get; init; }
}

internal sealed class AonHitWrapper
{
    [JsonPropertyName("_source")]
    public AonSpellHit? Source { get; init; }
}

internal sealed class AonSpellHit
{
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("url")]
    public string? Url { get; init; }

    [JsonPropertyName("summary")]
    public string? Summary { get; init; }

    [JsonPropertyName("level")]
    public int? Level { get; init; }

    [JsonPropertyName("tradition")]
    public List<string>? Tradition { get; init; }

    [JsonPropertyName("trait")]
    public List<string>? Trait { get; init; }

    [JsonPropertyName("actions")]
    public string? Actions { get; init; }

    [JsonPropertyName("range_raw")]
    [JsonConverter(typeof(AonStringOrStringArrayJsonConverter))]
    public string? RangeRaw { get; init; }

    [JsonPropertyName("duration_raw")]
    [JsonConverter(typeof(AonStringOrStringArrayJsonConverter))]
    public string? DurationRaw { get; init; }

    [JsonPropertyName("saving_throw")]
    [JsonConverter(typeof(AonStringOrStringArrayJsonConverter))]
    public string? SavingThrow { get; init; }

    [JsonPropertyName("defense")]
    [JsonConverter(typeof(AonStringOrStringArrayJsonConverter))]
    public string? Defense { get; init; }

    [JsonPropertyName("target")]
    [JsonConverter(typeof(AonStringOrStringArrayJsonConverter))]
    public string? Target { get; init; }

    [JsonPropertyName("area")]
    [JsonConverter(typeof(AonStringOrStringArrayJsonConverter))]
    public string? Area { get; init; }

    [JsonPropertyName("effect")]
    [JsonConverter(typeof(AonStringOrStringArrayJsonConverter))]
    public string? Effect { get; init; }
}
