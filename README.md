# NpcPrototype

A C# console prototype for AI-driven NPCs powered by the Anthropic Claude API. Each NPC has a distinct personality, short-term conversation memory, and long-term fact memory extracted from past chats.

This is the warm-up project before porting the system into [s&box](https://sbox.facepunch.com) (Facepunch's Source 2-based C# game platform).

## What works today

- **Three NPCs with distinct personalities**
  - **Ayşe** — cheerful baker, slightly gossipy
  - **Kemal** — gruff blacksmith, terse but secretly wise
  - **Elif** — 12-year-old kid, asks "why?" about everything, occasionally gossips about the other villagers
- **Two-tier memory per NPC**
  - **Short-term** (`Memory`): last 10 conversation entries, replayed as `messages[]` on every API call
  - **Long-term** (`LongTermMemory`): up to 20 distilled facts, injected into the system prompt
- **Automatic fact extraction** — every 5 messages, a separate Claude call distills the recent conversation into JSON facts (name, preferences, important events)
- **Interactive switching** — choose who to talk to via `ayse` / `kemal` / `elif`, type `degistir` mid-conversation to switch, `exit` to quit
- Built on `Anthropic` SDK v12.13.0, `claude-haiku-4-5`, .NET 10

## Architecture

```
TalkAsync(input)
  ├─ BuildMessagesFromMemory()  → user/assistant roles from short-term memory
  ├─ BuildSystemPrompt()        → personality + long-term facts
  └─ Anthropic API call         → reply + append to short-term memory

ExtractFactsAsync()             [every 5 messages]
  ├─ Last 5 short-term entries  → separate API call with JSON-only system prompt
  └─ Parse JSON array           → dedupe → append to long-term memory
```

Why a separate call for fact extraction? Personas and analytics conflict in one prompt — splitting them keeps Ayşe in character while a clean analyst pass handles fact distillation. Trade-off: one extra API call per 5 turns.

## Running it

Requires .NET 10 SDK and an Anthropic API key.

```powershell
# PowerShell
$env:ANTHROPIC_API_KEY = "sk-ant-..."
cd NpcPrototype
dotnet run
```

```bash
# bash
export ANTHROPIC_API_KEY="sk-ant-..."
cd NpcPrototype
dotnet run
```

The API key is **only** read from the `ANTHROPIC_API_KEY` environment variable — nothing is hardcoded or stored on disk.

## Roadmap

- **April 2026** — refine memory system, add NPC-to-NPC fact sharing (real village gossip instead of hallucinated gossip)
- **May 2026** — port to s&box: replace console loop with in-game proximity dialogue, hook memory into save/load, async-safe on the main thread
- **Later** — mood system that drifts based on player interactions; vector-DB-backed long-term memory; structured outputs for fact extraction

## License

Personal learning project — no license yet.
