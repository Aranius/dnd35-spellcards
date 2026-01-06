using SpellCards.Condensing;
using SpellCards.Infrastructure;
using SpellCards.Models;
using SpellCards.Rendering;
using SpellCards.Sources;
using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

try
{
    var commandLine = CommandLineOptions.Parse(args);
    var condenseDescriptions = !commandLine.NoCondense;

    FontManager.RegisterFont(
        File.OpenRead(Path.Combine(AppContext.BaseDirectory, "assets/fonts/Cinzel-SemiBold.ttf")));

    FontManager.RegisterFont(
        File.OpenRead(Path.Combine(AppContext.BaseDirectory, "assets/fonts/SourceSerif4-Regular.ttf")));

    FontManager.RegisterFont(
        File.OpenRead(Path.Combine(AppContext.BaseDirectory, "assets/fonts/SourceSerif4-Semibold.ttf")));

    QuestPDF.Settings.License = LicenseType.Community;

    var ct = CancellationToken.None;

    var baseDir = AppContext.BaseDirectory;
    var reqPath = Path.Combine(baseDir, "requests.txt");

    if (!File.Exists(reqPath))
        throw new FileNotFoundException("requests.txt not found. Ensure it is copied next to the executable.", reqPath);

    var settingsPath = Path.Combine(baseDir, "settings.json");
    var settings = SpellCardsSettings.Load(settingsPath);

    var cacheDir = Path.Combine(baseDir, "cache");
    Directory.CreateDirectory(cacheDir);
    Directory.CreateDirectory(Path.Combine(baseDir, "out"));

    var (ruleSet, spellNames) = LoadSpellRequests(reqPath);

    using var http = new HttpClient();
    http.DefaultRequestHeaders.UserAgent.ParseAdd("Dnd35SpellCards/1.0 (personal use)");

    var cache = new HttpCache(http, cacheDir);

    var spells = ruleSet switch
    {
        RuleSet.Dnd5e => await new Open5eSpellSource(cache).FetchSpellsAsync(spellNames, ct),
        RuleSet.Pf1 => await new PathfinderSpellDbSource(cache).FetchSpellsAsync(spellNames, ct),
        RuleSet.Pf2 => await new AonPf2SpellSource(cache).FetchSpellsAsync(spellNames, ct),
        _ => await new D20SrdSpellSource(cache).FetchSpellsAsync(spellNames, ct)
    };

    ISpellCondenser condenser = new NoOpSpellCondenser();
    OllamaSpellCondenser? ollama = null;
    OpenAiCompatibleSpellCondenser? llm = null;

    if (condenseDescriptions)
    {
        var llmModel = commandLine.LlmModel
                       ?? settings?.Llm?.Model
                       ?? Environment.GetEnvironmentVariable("SPELLCARDS_LLM_MODEL");

        var llmEndpoint = commandLine.LlmEndpoint
                          ?? settings?.Llm?.Endpoint
                          ?? Environment.GetEnvironmentVariable("SPELLCARDS_LLM_ENDPOINT");

        var llmKey = ResolveApiKey(commandLine.LlmApiKey
                                  ?? settings?.Llm?.ApiKey
                                  ?? Environment.GetEnvironmentVariable("SPELLCARDS_LLM_APIKEY"));

        if (!string.IsNullOrWhiteSpace(llmEndpoint) && !string.IsNullOrWhiteSpace(llmKey))
        {
            try
            {
                var condenseCache = new SpellCondensingCache(Path.Combine(cacheDir, "condensed"));
                llm = new OpenAiCompatibleSpellCondenser(llmModel ?? "gpt-4o-mini", llmEndpoint, llmKey, condenseCache);
                condenser = llm;
                Console.WriteLine($"[condense] Using OpenAI-compatible endpoint '{llmEndpoint}' with model '{(llmModel ?? "gpt-4o-mini")}'. Pass --no-condense to disable.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[condense] LLM disabled (falling back): {ex.Message}");
            }
        }

        if (condenser is NoOpSpellCondenser)
        {
            var model = commandLine.Model
                        ?? settings?.Ollama?.Model
                        ?? Environment.GetEnvironmentVariable("SPELLCARDS_OLLAMA_MODEL")
                        ?? "mixtral:8x7b";
            var endpoint = commandLine.Endpoint
                           ?? settings?.Ollama?.Endpoint
                           ?? Environment.GetEnvironmentVariable("SPELLCARDS_OLLAMA_ENDPOINT")
                           ?? "http://127.0.0.1:11434";

            if (!await OllamaSpellCondenser.IsServiceAvailableAsync(endpoint, ct))
            {
                Console.WriteLine($"[condense] Ollama endpoint '{endpoint}' is unreachable. Run ollama serve or provide --no-condense.");
            }
            else
            {
                try
                {
                    var condenseCache = new SpellCondensingCache(Path.Combine(cacheDir, "condensed"));
                    ollama = new OllamaSpellCondenser(model, endpoint, condenseCache);
                    condenser = ollama;
                    Console.WriteLine($"[condense] Using Ollama model '{model}' at {endpoint}. Pass --no-condense to disable.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[condense] Disabled (falling back to raw text): {ex.Message}");
                }
            }
        }
    }

    var finalSpells = await PrepareSpellCardsAsync(spells, condenser, ct);

    llm?.Dispose();
    ollama?.Dispose();

    var output = Path.Combine(baseDir, "out", "spellcards.pdf");
    new SpellCardDocument(finalSpells).GeneratePdf(output);
    
    Console.WriteLine($"Generated: {output}");
}
catch (Exception ex)
{
    Console.Error.WriteLine("Error: " + ex.Message);
    Console.Error.WriteLine();
    Console.Error.WriteLine("Troubleshooting:");
    Console.Error.WriteLine("- Ensure `requests.txt` is next to the executable.");
    Console.Error.WriteLine("- Supported rulesets: 3.5, 5e, pf1, pf2 (first line: `ruleset: <value>`).");
    Console.Error.WriteLine("- Use --no-condense to disable LLM summarization.");
    Environment.ExitCode = 1;
}

static async Task<IReadOnlyList<Spell>> PrepareSpellCardsAsync(IReadOnlyList<Spell> spells, ISpellCondenser condenser, CancellationToken ct)
{
    var result = new List<Spell>();
    var canCondense = condenser is not NoOpSpellCondenser;

    foreach (var spell in spells)
    {
        var baselineParts = SpellSplitter.SplitIfNeeded(spell);
        if (!canCondense || baselineParts.Count == 1)
        {
            result.AddRange(baselineParts);
            continue;
        }

        try
        {
            var condensedText = await condenser.CondenseAsync(spell, ct).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(condensedText))
            {
                result.AddRange(baselineParts);
                continue;
            }

            var condensedParts = SpellSplitter.SplitIfNeeded(spell with { Description = condensedText });
            if (condensedParts.Count < baselineParts.Count)
            {
                Console.WriteLine($"[condense] {spell.Name}: {baselineParts.Count} -> {condensedParts.Count} cards");
                result.AddRange(condensedParts);
            }
            else
            {
                result.AddRange(baselineParts);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[condense] Failed for {spell.Name}: {ex.Message}. Using original text.");
            result.AddRange(baselineParts);
        }
    }

    return result;
}

static (RuleSet RuleSet, IReadOnlyList<string> SpellNames) LoadSpellRequests(string path)
{
    var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var list = new List<string>();

    var ruleSet = RuleSet.Dnd35;
    var skipLineIndex = -1;

    var allLines = File.ReadAllLines(path);
    for (var i = 0; i < allLines.Length; i++)
    {
        var trimmed = allLines[i].Trim().TrimStart('\uFEFF');
        if (string.IsNullOrWhiteSpace(trimmed))
            continue;

        if (TryParseRuleSetDirective(trimmed, out var parsed))
        {
            ruleSet = parsed;
            skipLineIndex = i;
        }

        break;
    }

    for (var i = 0; i < allLines.Length; i++)
    {
        if (i == skipLineIndex)
            continue;

        var rawLine = allLines[i].TrimStart('\uFEFF');
        var line = rawLine.Split('#', 2)[0].Trim();
        if (string.IsNullOrEmpty(line))
            continue;

        if (unique.Add(line))
            list.Add(line);
    }

    if (list.Count == 0)
        throw new InvalidOperationException($"No spell names found in '{path}'. Add at least one name (one per line).");

    return (ruleSet, list);
}

static bool TryParseRuleSetDirective(string rawLine, out RuleSet ruleSet)
{
    ruleSet = RuleSet.Dnd35;

    if (string.IsNullOrWhiteSpace(rawLine))
        return false;

    var s = rawLine.Trim().TrimStart('\uFEFF');

    // Allow comment-prefixed directive on the first non-empty line.
    if (s.StartsWith("#", StringComparison.Ordinal))
        s = s[1..].TrimStart();
    if (s.StartsWith("//", StringComparison.Ordinal))
        s = s[2..].TrimStart();

    if (!s.StartsWith("ruleset", StringComparison.OrdinalIgnoreCase))
        return false;

    s = s["ruleset".Length..].TrimStart();
    if (s.StartsWith(":", StringComparison.Ordinal) || s.StartsWith("=", StringComparison.Ordinal))
        s = s[1..].TrimStart();

    if (string.IsNullOrWhiteSpace(s))
        throw new InvalidOperationException("Invalid ruleset directive. Example: 'ruleset: 5e' or '# ruleset: 3.5'.");

    var value = s.Trim().ToLowerInvariant();
    ruleSet = value switch
    {
        "3.5" or "3.5e" or "35" or "dnd35" or "dnd3.5" => RuleSet.Dnd35,
        "5e" or "5.0" or "5" or "dnd5" or "dnd5e" or "dnd5.0" => RuleSet.Dnd5e,
        "pf" or "pf1" or "pf1e" or "pathfinder" or "pathfinder1" or "pathfinder1e" => RuleSet.Pf1,
        "pf2" or "pf2e" or "pathfinder2" or "pathfinder2e" => RuleSet.Pf2,
        _ => throw new InvalidOperationException($"Unknown ruleset '{s}'. Supported: 3.5, 5e, pf1, pf2.")
    };

    return true;
}

static string? ResolveApiKey(string? raw)
{
    if (string.IsNullOrWhiteSpace(raw))
        return null;

    var trimmed = raw.Trim();
    if (trimmed.StartsWith("env:", StringComparison.OrdinalIgnoreCase))
    {
        var name = trimmed["env:".Length..].Trim();
        if (string.IsNullOrWhiteSpace(name))
            return null;

        return Environment.GetEnvironmentVariable(name);
    }

    return trimmed;
}

internal enum RuleSet
{
    Dnd35,
    Dnd5e,
    Pf1,
    Pf2
}

internal sealed record CommandLineOptions(
    bool NoCondense,
    string? Model,
    string? Endpoint,
    string? LlmModel,
    string? LlmEndpoint,
    string? LlmApiKey)
{
    public static CommandLineOptions Parse(string[] args)
    {
        var noCondense = false;
        string? model = null;
        string? endpoint = null;

        string? llmModel = null;
        string? llmEndpoint = null;
        string? llmApiKey = null;

        foreach (var arg in args)
        {
            if (string.Equals(arg, "--no-condense", StringComparison.OrdinalIgnoreCase))
            {
                noCondense = true;
                continue;
            }

            if (arg.StartsWith("--model=", StringComparison.OrdinalIgnoreCase))
            {
                model = arg[("--model=").Length..].Trim();
                continue;
            }

            if (arg.StartsWith("--endpoint=", StringComparison.OrdinalIgnoreCase))
            {
                endpoint = arg[("--endpoint=").Length..].Trim();
                continue;
            }

            if (arg.StartsWith("--llm-model=", StringComparison.OrdinalIgnoreCase))
            {
                llmModel = arg[("--llm-model=").Length..].Trim();
                continue;
            }

            if (arg.StartsWith("--llm-endpoint=", StringComparison.OrdinalIgnoreCase))
            {
                llmEndpoint = arg[("--llm-endpoint=").Length..].Trim();
                continue;
            }

            if (arg.StartsWith("--llm-key=", StringComparison.OrdinalIgnoreCase))
            {
                llmApiKey = arg[("--llm-key=").Length..].Trim();
            }
        }

        return new CommandLineOptions(
            noCondense,
            string.IsNullOrWhiteSpace(model) ? null : model,
            string.IsNullOrWhiteSpace(endpoint) ? null : endpoint,
            string.IsNullOrWhiteSpace(llmModel) ? null : llmModel,
            string.IsNullOrWhiteSpace(llmEndpoint) ? null : llmEndpoint,
            string.IsNullOrWhiteSpace(llmApiKey) ? null : llmApiKey);
    }
}

