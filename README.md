# D&D Spell Cards (MTG size) PDF Generator

Create printable, Magic: The Gathering-sized cards for a list of spells. The tool pulls rules text (3.5 from d20srd.org; 5e from Open5e; PF1 from PathfinderSpellDb; PF2 from Archives of Nethys), optionally condenses it with an LLM (hosted OpenAI-compatible endpoint or local Ollama), and renders a high-resolution PDF via QuestPDF.

## Highlights
- **One-click PDF** - feed it a spell list, get `out/spellcards.pdf` laid out front-to-back.
- **Smart fetching** - caches HTTP requests locally so repeated runs are instant.
- **Multiple rulesets** - supports **D&D 3.5**, **D&D 5e**, **Pathfinder 1e**, and **Pathfinder 2e**.
- **Optional AI summaries** - trims spell text using either:
  - an **OpenAI-compatible** `/v1/chat/completions` endpoint (recommended for hosted LLMs), or
  - a local **Ollama** server.
- **Cross-platform** - .NET 9 console app runs on Windows, macOS, and Linux.

## Download & Run
### Prebuilt packages
1. Grab the latest release zip for your platform (Windows, Linux, macOS) from the GitHub Releases page.
2. Extract the archive somewhere writable (it creates `cache/` and `out/`).
3. Edit `requests.txt` (one spell per line; `#` comments allowed).
4. Launch the binary:
   - **Windows**: `SpellCards.exe`
   - **Linux/macOS**: `./SpellCards`
5. The generated PDF lives under `out/spellcards.pdf`.

### Build from source
```bash
dotnet restore
dotnet run --project SpellCards
```

### Publishing your own zips
Produce trimmed, platform-specific bundles ready for upload:
```bash
# Windows x64
dotnet publish SpellCards -c Release -r win-x64 --self-contained false -p:PublishTrimmed=true

# Linux x64
dotnet publish SpellCards -c Release -r linux-x64 --self-contained false -p:PublishTrimmed=true

# macOS (Apple Silicon)
dotnet publish SpellCards -c Release -r osx-arm64 --self-contained false -p:PublishTrimmed=true
```
Each command drops binaries under `SpellCards/bin/Release/net9.0/<rid>/publish/`. Zip that folder (it contains `requests.txt`, `settings.json`, fonts, etc.) and attach it to a GitHub release.

## Configuration Files
- **`requests.txt`** - spell names to render. Duplicate names are ignored.
  - Optional first non-empty line directive to select the ruleset:
    - `ruleset: 3.5` (default)
    - `ruleset: 5e`
    - `ruleset: pf1`
    - `ruleset: pf2`
  - Comment-prefixed forms also work, e.g. `# ruleset: pf2`
- **`settings.json`** - runtime defaults copied beside the executable at publish time:
  ```json
  {
    "ollama": {
      "model": "mixtral:8x7b",
      "endpoint": "http://127.0.0.1:11434"
    },
    "llm": {
      "endpoint": "",
      "apiKey": "",
      "model": ""
    }
  }
  ```
  - `llm.endpoint` is a base URL for an **OpenAI-compatible** API (the app calls `v1/chat/completions`).
  - `llm.apiKey` can be a literal key or `env:NAME` to read from an environment variable.
- **`cache/`** - HTTP downloads and condensed spell text. Delete `cache/condensed` when switching LLM providers/models so summaries regenerate.

## Controlling the LLM Summaries
Condensing is optional. If enabled, the selection order is:
1. **OpenAI-compatible LLM** (when `llm.endpoint` + `llm.apiKey` are provided)
2. **Ollama** (when reachable)
3. **Raw text** (no condensation; long spells will still split across multiple cards)

You can always force raw text with `--no-condense`.

### OpenAI-compatible endpoint
Provide `llm.endpoint` and `llm.apiKey` via config, env vars, or CLI:
- CLI:
  - `--llm-endpoint=https://api.openai.com/`
  - `--llm-key=...`
  - `--llm-model=gpt-4o-mini`
- Environment variables:
  - `SPELLCARDS_LLM_ENDPOINT`
  - `SPELLCARDS_LLM_APIKEY`
  - `SPELLCARDS_LLM_MODEL`

### Ollama (local)
1. Install [Ollama](https://ollama.com/) and pull the recommended model:
   ```bash
   ollama pull mixtral:8x7b
   ollama serve
   ```
2. Run the app. By default it uses the values found in `settings.json`.
3. Override per run with command-line switches:
   - `--model=phi3:mini`
   - `--endpoint=http://192.168.1.42:11434`
4. Environment variables still work and sit below CLI/settings in precedence:
   - `SPELLCARDS_OLLAMA_MODEL`
   - `SPELLCARDS_OLLAMA_ENDPOINT`

## Command Reference
```
Usage: SpellCards [options]
  --llm-endpoint=<url>   Base URL for an OpenAI-compatible API.
  --llm-key=<key>        API key (or use settings.json / env var).
  --llm-model=<name>     Model name for the OpenAI-compatible endpoint.

  --model=<name>         Override Ollama model for this run.
  --endpoint=<url>       Override Ollama endpoint (default http://127.0.0.1:11434).

  --no-condense          Skip LLM summaries; use raw descriptions.
```

## Rendering Pipeline
1. **Fetch** - a spell source pulls spell data, caching responses.
2. **Condense (optional)** - call an OpenAI-compatible endpoint or Ollama and cache condensed text.
3. **Split** - `SpellSplitter` divides long descriptions across multiple cards when needed.
4. **Render** - `SpellCardDocument` builds the PDF using QuestPDF and embedded fonts (`Cinzel`, `Source Serif`).

## Troubleshooting
- **`requests.txt not found`** - make sure it sits next to the executable (publish copies it automatically).
- **Invalid ruleset** - ensure the first line is `ruleset: 3.5`, `ruleset: 5e`, `ruleset: pf1`, or `ruleset: pf2`.
- **Condensing fails** - the app falls back to raw text automatically. Use `--no-condense` to bypass.
- **Fonts missing** - keep the `assets/` directory beside the binary; `dotnet publish` already places it correctly.

Icons provided by [game-icons.net](https://game-icons.net) (CC BY 3.0). Spell content (SRD/PRD). Use responsibly.
