namespace SpellCards.Infrastructure;

public static class SpellNameResolver
{
    public static string Resolve(
        string requestedName,
        IReadOnlyDictionary<string, string> srdIndex)
    {
        // 1. Exact match
        if (srdIndex.ContainsKey(requestedName))
            return requestedName;

        var normalizedRequest = SpellNameNormalizer.Normalize(requestedName);

        // 2. Normalized exact match
        foreach (var key in srdIndex.Keys)
        {
            if (SpellNameNormalizer.Normalize(key) == normalizedRequest)
                return key;
        }

        // 3. Fuzzy match
        var candidates = srdIndex.Keys
            .Select(k => new
            {
                Name = k,
                Distance = Levenshtein.Distance(
                    SpellNameNormalizer.Normalize(k),
                    normalizedRequest)
            })
            .OrderBy(x => x.Distance)
            .Take(3)
            .ToList();

        if (candidates.Count == 0 || candidates[0].Distance > 3)
        {
            throw new InvalidOperationException(
                $"Spell '{requestedName}' not found in SRD.\n" +
                $"Closest matches:\n" +
                string.Join("\n", candidates.Select(c => $"  - {c.Name}"))
            );
        }

        // Accept best match if clearly better than others
        if (candidates.Count == 1 || candidates[0].Distance < candidates[1].Distance)
            return candidates[0].Name;

        throw new InvalidOperationException(
            $"Spell '{requestedName}' is ambiguous.\n" +
            $"Did you mean:\n" +
            string.Join("\n", candidates.Select(c => $"  - {c.Name}"))
        );
    }
}
