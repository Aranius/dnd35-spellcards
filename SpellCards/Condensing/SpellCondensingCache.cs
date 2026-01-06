using System.Security.Cryptography;
using System.Text;
using SpellCards.Models;

namespace SpellCards.Condensing;

public sealed class SpellCondensingCache
{
    private readonly string _directory;

    public SpellCondensingCache(string directory)
    {
        _directory = directory;
        Directory.CreateDirectory(_directory);
    }

    public bool TryRead(Spell spell, out string text) => TryRead(spell.Name, spell.Description, out text);

    public bool TryRead(string spellName, string originalDescription, out string text)
    {
        var path = Path.Combine(_directory, ComputeKey(spellName, originalDescription) + ".txt");
        if (!File.Exists(path))
        {
            text = string.Empty;
            return false;
        }

        text = File.ReadAllText(path).Trim();
        return !string.IsNullOrWhiteSpace(text);
    }

    public void Save(Spell spell, string condensedDescription)
    {
        var path = Path.Combine(_directory, ComputeKey(spell.Name, spell.Description) + ".txt");
        File.WriteAllText(path, condensedDescription ?? string.Empty);
    }

    private static string ComputeKey(string spellName, string source)
    {
        using var sha256 = SHA256.Create();
        var buffer = Encoding.UTF8.GetBytes($"{spellName}\n{source}");
        return Convert.ToHexString(sha256.ComputeHash(buffer)).ToLowerInvariant();
    }
}
