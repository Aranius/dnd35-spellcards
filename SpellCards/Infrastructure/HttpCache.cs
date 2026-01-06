using System.Security.Cryptography;
using System.Text;

namespace SpellCards.Infrastructure;

public sealed class HttpCache
{
    private readonly HttpClient _http;
    private readonly string _cacheDir;

    public HttpCache(HttpClient http, string cacheDir)
    {
        _http = http;
        _cacheDir = cacheDir;
        Directory.CreateDirectory(_cacheDir);
    }

    public async Task<string> GetStringCachedAsync(string url, CancellationToken ct)
    {
        var file = Path.Combine(_cacheDir, Sha1(url) + ".html");
        if (File.Exists(file))
            return await File.ReadAllTextAsync(file, ct);

        var html = await _http.GetStringAsync(url, ct);
        await File.WriteAllTextAsync(file, html, ct);
        return html;
    }

    public async Task<string> PostStringCachedAsync(string url, string jsonBody, CancellationToken ct)
    {
        var key = Sha1(url + "\n" + jsonBody);
        var file = Path.Combine(_cacheDir, "post_" + key + ".json");

        if (File.Exists(file))
            return await File.ReadAllTextAsync(file, ct);

        using var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
        using var resp = await _http.PostAsync(url, content, ct).ConfigureAwait(false);

        var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"HTTP {(int)resp.StatusCode} for POST {url}: {body}");

        await File.WriteAllTextAsync(file, body, ct).ConfigureAwait(false);
        return body;
    }

    private static string Sha1(string s)
    {
        using var sha1 = SHA1.Create();
        var bytes = sha1.ComputeHash(Encoding.UTF8.GetBytes(s));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
