# Babel FFXIV

Bidirectional in-game chat translation for FFXIV, powered by any OpenAI-compatible LLM API.

## Features
- **Incoming**: foreign chat in watched channels is translated to your language; the original is printed underneath (optional).
- **Outgoing**: compose in your language in the plugin window (or `/babel send <text>`); the result is translated, printed to **local** chat, and (from the window) copied to the clipboard so you can paste into the game chat box.
- Works with any OpenAI-compatible endpoint: OpenAI, DeepSeek, OpenRouter, or a local llama.cpp / LM Studio / KoboldCpp server.

## Usage
1. `/babel` — open the window.
2. Settings tab: set Base URL, Model, API key, your language, and any other languages you understand. Use **Test connection** to verify.
3. Translate tab: type a message, hit Enter / **Translate & Copy to Clipboard**, then paste into the game chat if you want others to see it.
4. `/babel send <text>` — translate and print locally (does not send on the network).

## Privacy & security notes
- Your **API key is stored in plaintext** in the Dalamud plugin config on disk (normal for Dalamud plugins). Treat the config directory like a secrets store.
- **Base URL is fully user-controlled** so you can point at local or third-party OpenAI-compatible servers. Prefer HTTPS for remote endpoints; a mis-set URL can send chat text (and your Bearer token) to whatever host you configure, including LAN services on the game PC.
- Chat text is sent to the configured LLM provider for translation.

## Building
Requires .NET 10 SDK and the Dalamud dev distrib at `%APPDATA%\XIVLauncher\addon\Hooks\dev`.

```
dotnet build -c Release
dotnet test BabelFFXIV.Tests/BabelFFXIV.Tests.csproj
```

Output: `bin/Release/BabelFFXIV/` (zip-ready `latest.zip`).

## Install (dev)
`/xlsettings` → Experimental → Dev Plugin Locations → add `bin/Release/BabelFFXIV`, then enable under `/xlplugins` → Dev Tools.
