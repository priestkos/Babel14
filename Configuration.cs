using Dalamud.Configuration;
using System;
using System.Collections.Generic;

namespace Babel14;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    // --- LLM backend ---
    /// <summary>Base URL of an OpenAI-compatible chat completions endpoint.</summary>
    public string ApiBaseUrl { get; set; } = "https://api.openai.com/v1";

    /// <summary>Model name, e.g. gpt-4o-mini, deepseek-chat, llama3...</summary>
    public string Model { get; set; } = "gpt-4o-mini";

    /// <summary>API key. Stored locally in plugin config.</summary>
    public string ApiKey { get; set; } = string.Empty;

    // --- Translation behaviour ---
    /// <summary>Your native language (what YOU read). Incoming foreign chat is translated to this.</summary>
    public string NativeLanguage { get; set; } = "English";

    /// <summary>Languages you can speak. Outgoing chat is translated into the chosen target.</summary>
    public List<string> KnownLanguages { get; set; } = new() { "English" };

    /// <summary>Translate incoming chat messages.</summary>
    public bool TranslateIncoming { get; set; } = true;

    /// <summary>Show the original text under the translation.</summary>
    public bool ShowOriginal { get; set; } = true;

    /// <summary>Chat channel IDs (XivChatType raw values) to watch for incoming messages.
    /// Defaults: Say(10), Shout(13), Yell(14), TellIncoming(58), Party(18), Alliance(20).</summary>
    public List<int> WatchedChannels { get; set; } = new() { 10, 13, 14, 58, 18, 20 };

    public void Save() => Plugin.PluginInterface.SavePluginConfig(this);
}
