namespace SpellCards.Sources.AonPf2;

internal static class AonPf2QueryBuilder
{
    public static object BuildSpellSearch(string query)
    {
        return new Dictionary<string, object?>
        {
            ["query"] = new Dictionary<string, object?>
            {
                ["function_score"] = new Dictionary<string, object?>
                {
                    ["query"] = new Dictionary<string, object?>
                    {
                        ["bool"] = new Dictionary<string, object?>
                        {
                            ["must"] = new object[]
                            {
                                new { term = new { type = "Spell" } }
                            },
                            ["should"] = BuildShould(query),
                            ["minimum_should_match"] = 1,
                            ["must_not"] = new object[]
                            {
                                new { term = new { exclude_from_search = true } },
                                new { term = new { category = "item-bonus" } },

                                // Match AoN UI default filtering (legacy mode false => exclude legacy_id entries)
                                new { exists = new { field = "legacy_id" } },

                                new { exists = new { field = "item_child_id" } }
                            }
                        }
                    },
                    ["boost_mode"] = "multiply",
                    ["functions"] = new object[]
                    {
                        new { filter = new { terms = new { type = new[] { "Ancestry", "Class", "Versatile Heritage" } } }, weight = 1.2 },
                        new { filter = new { terms = new { type = new[] { "Trait" } } }, weight = 1.05 }
                    }
                }
            },
            ["size"] = 25,
            ["sort"] = new object[] { "_score", "_doc" },
            ["_source"] = new Dictionary<string, object?>
            {
                ["excludes"] = new[] { "text" }
            }
        };
    }

    private static object[] BuildShould(string query)
    {
        return
        [
            new Dictionary<string, object>
            {
                ["match_phrase_prefix"] = new Dictionary<string, object>
                {
                    ["name.sayt"] = new { query }
                }
            },
            new Dictionary<string, object>
            {
                ["match_phrase_prefix"] = new Dictionary<string, object>
                {
                    ["legacy_name.sayt"] = new { query }
                }
            },
            new Dictionary<string, object>
            {
                ["match_phrase_prefix"] = new Dictionary<string, object>
                {
                    ["remaster_name.sayt"] = new { query }
                }
            },
            new Dictionary<string, object>
            {
                ["match_phrase_prefix"] = new Dictionary<string, object>
                {
                    ["text.sayt"] = new { query, boost = 0.1 }
                }
            },

            new { term = new { name = query } },
            new { term = new { legacy_name = query } },
            new { term = new { remaster_name = query } },

            new
            {
                @bool = new
                {
                    must = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Select(term => new
                        {
                            multi_match = new
                            {
                                query = term,
                                type = "best_fields",
                                fields = new[] { "name", "legacy_name", "remaster_name", "text^0.1", "trait_raw", "type" },
                                fuzziness = "auto"
                            }
                        })
                        .ToArray()
                }
            }
        ];
    }
}
