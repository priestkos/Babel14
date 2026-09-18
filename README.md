# Babel FFXIV

Bidirectional in-game chat translation for FFXIV, powered by any OpenAI-compatible LLM API.

## Features
- **Incoming**: foreign chat in watched channels is translated to your language; the original is printed underneath (optional).
- **Outgoing**: compose in your language in the plugin window (or `/babel send <text>`), it is translated and sent to Say.
- Works with any OpenAI-compatible endpoint: OpenAI, DeepSeek, OpenRouter, or a local llama.cpp / LM Studio / KoboldCpp server.

## Usage
1. `/babel` — open the window.
2. Settings tab: set Base URL, Model, API key, your language, and any other languages you understand. Use **Test connection** to verify.
3. Translate tab: type a message, hit Enter / Translate & Send.

## Building
Requires .NET 10 SDK and the Dalamud dev distrib at `%APPDATA%\XIVLauncher\addon\Hooks\dev`.

```
dotnet build -c Release
```

Output: `bin/Release/BabelFFXIV/` (zip-ready `latest.zip`).

## Install (dev)
`/xlsettings` → Experimental → Dev Plugin Locations → add `bin/Release/BabelFFXIV`, then enable under `/xlplugins` → Dev Tools.
