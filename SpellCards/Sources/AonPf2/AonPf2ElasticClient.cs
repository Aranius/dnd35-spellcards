using System.Text.Json;
using SpellCards.Infrastructure;

namespace SpellCards.Sources.AonPf2;

internal sealed class AonPf2ElasticClient
{
    private readonly HttpCache _cache;
    private readonly string _elasticUrl;
    private readonly JsonSerializerOptions _serializerOptions;

    public AonPf2ElasticClient(HttpCache cache, string elasticUrl, JsonSerializerOptions serializerOptions)
    {
        _cache = cache;
        _elasticUrl = elasticUrl.TrimEnd('/');
        _serializerOptions = serializerOptions;
    }

    public async Task<AonSearchResponse> SearchAsync(object body, CancellationToken ct)
    {
        var url = _elasticUrl + "/_search";
        var json = JsonSerializer.Serialize(body, _serializerOptions);
        var responseJson = await _cache.PostStringCachedAsync(url, json, ct).ConfigureAwait(false);

        return JsonSerializer.Deserialize<AonSearchResponse>(responseJson, _serializerOptions)
               ?? throw new InvalidOperationException("AoN Elasticsearch returned invalid JSON.");
    }
}
