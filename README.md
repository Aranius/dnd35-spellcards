# D&D 3.5 Spell Cards (MTG size) PDF Generator

Create printable, Magic: The Gathering–sized cards for any list of SRD 3.5 spells. The tool pulls rules text from d20srd.org, optionally condenses it with a local LLM, and renders a high-resolution PDF via QuestPDF.

## Highlights
- **One-click PDF** – feed it a spell list, get `out/spellcards.pdf` laid out front-to-back.
- **Smart fetching** – caches SRD requests locally so repeated runs are instant.
- **Optional AI summaries** – trims spell text using an Ollama-hosted model (defaults to `mixtral:8x7b`).
- **Cross-platform** – .NET 9 console app runs on Windows, macOS, and Linux.

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
dotnet run --project Dnd35.SpellCards
```

### Publishing your own zips
Produce trimmed, platform-specific bundles ready for upload:
```bash
# Windows x64
dotnet publish Dnd35.SpellCards -c Release -r win-x64 --self-contained false -p:PublishTrimmed=true

# Linux x64
dotnet publish Dnd35.SpellCards -c Release -r linux-x64 --self-contained false -p:PublishTrimmed=true

# macOS (Apple Silicon)
dotnet publish Dnd35.SpellCards -c Release -r osx-arm64 --self-contained false -p:PublishTrimmed=true
```
Each command drops binaries under `Dnd35.SpellCards/bin/Release/net9.0/<rid>/publish/`. Zip that folder (it contains `requests.txt`, `settings.json`, fonts, etc.) and attach it to a GitHub release.

## Configuration Files
- **`requests.txt`** – spell names to render. Duplicate names are ignored.
- **`settings.json`** – runtime defaults copied beside the executable at publish time:
  ```json
  {
    "ollama": {
      "model": "mixtral:8x7b",
      "endpoint": "http://127.0.0.1:11434"
    }
  }
  ```
- **`cache/`** – HTTP downloads and condensed spell text. Delete `cache/condensed` when switching LLM models so summaries regenerate.

## Controlling the LLM Summaries
The generator falls back to raw SRD text if condensing fails or is disabled.

1. Install [Ollama](https://ollama.com/) and pull the recommended model:
   ```bash
   ollama pull mixtral:8x7b
   ollama serve
   ```
2. Run the app. By default it uses the values found in `settings.json`.
3. Override per run with command-line switches:
   - `--model=phi3:mini`
   - `--endpoint=http://192.168.1.42:11434`
   - `--no-condense` (skip the AI phase entirely)
4. Environment variables still work and sit below CLI/settings in precedence:
   - `SPELLCARDS_OLLAMA_MODEL`
   - `SPELLCARDS_OLLAMA_ENDPOINT`

> Tip: Mixtral’s outputs best fit the cards when you keep the default prompt. If you experiment with other models, clear `cache/condensed` between runs.

## Command Reference
```
Usage: SpellCards [options]
  --model=<name>       Override Ollama model for this run.
  --endpoint=<url>     Override Ollama endpoint (default http://127.0.0.1:11434).
  --no-condense        Skip LLM summaries; use raw SRD descriptions.
```

## Rendering Pipeline
1. **Fetch** – `D20SrdSpellSource` pulls SRD spells, caching HTML and parsed JSON.
2. **Condense (optional)** – `OllamaSpellCondenser` builds a prompt, calls your local Ollama server, and caches responses per spell and model.
3. **Split** – `SpellSplitter` divides long descriptions across multiple cards when needed.
4. **Render** – `SpellCardDocument` builds the PDF using QuestPDF and embedded fonts (`Cinzel`, `Source Serif`).

## Troubleshooting
- **`requests.txt not found`** – make sure it sits next to the executable (publish copies it automatically).
- **Ollama 404 / unreachable** – confirm `ollama serve` is running and the model has been pulled. Use `--no-condense` to bypass temporarily.
- **Fonts missing** – keep the `assets/` directory beside the binary; `dotnet publish` already places it correctly.

Icons provided by [game-icons.net](https://game-icons.net) (CC BY 3.0). Spell content © Wizards of the Coast (SRD). Use responsibly.