using Dalamud.Configuration;
using Babel14.Services;
using Dalamud.Game.Text;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace Babel14;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 2;

    // --- LLM backend ---
    /// <summary>Base URL of an OpenAI-compatible chat completions endpoint.</summary>
    public string ApiBaseUrl { get; set; } = "https://openrouter.ai/api/v1";

    /// <summary>Model name, e.g. openrouter/free, openai/gpt-4o-mini, deepseek-chat, llama3...</summary>
    public string Model { get; set; } = "openrouter/free";

    /// <summary>API key. Stored locally in plugin config.</summary>
    public string ApiKey { get; set; } = string.Empty;

    // --- Translation behaviour ---
    /// <summary>Language incoming chat is translated into (what YOU read).</summary>
    public string NativeLanguage { get; set; } = Languages.DefaultNative;

    /// <summary>Languages you can speak. Outgoing chat is translated into the chosen target.</summary>
    // Json.NET reuses an initialised list and appends the JSON items to it, so every
    // plugin load re-added the defaults (one "English" per session). Replace instead.
    [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
    public List<string> KnownLanguages { get; set; } = new() { Languages.DefaultKnown };

    /// <summary>Translate incoming chat messages.</summary>
    public bool TranslateIncoming { get; set; } = true;

    /// <summary>Chat channel IDs (XivChatType raw values) to watch for incoming messages.</summary>
    [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
    public List<int> WatchedChannels { get; set; } = DefaultChannels();

    // Named, not numbered: the v1 list was hand-typed ids that meant TellIncoming/Party/SystemError/LS3/LS5.
    public static List<int> DefaultChannels() => new()
    {
        (int)XivChatType.Say, (int)XivChatType.Yell, (int)XivChatType.Shout, (int)XivChatType.TellIncoming,
        (int)XivChatType.Party, (int)XivChatType.CrossParty, (int)XivChatType.Alliance, (int)XivChatType.NoviceNetwork,
        (int)XivChatType.Ls1, (int)XivChatType.Ls2, (int)XivChatType.Ls3, (int)XivChatType.Ls4,
        (int)XivChatType.Ls5, (int)XivChatType.Ls6, (int)XivChatType.Ls7, (int)XivChatType.Ls8,
        (int)XivChatType.CrossLinkShell1, (int)XivChatType.CrossLinkShell2, (int)XivChatType.CrossLinkShell3,
        (int)XivChatType.CrossLinkShell4, (int)XivChatType.CrossLinkShell5, (int)XivChatType.CrossLinkShell6,
        (int)XivChatType.CrossLinkShell7, (int)XivChatType.CrossLinkShell8,
    };

    public void Save() => Plugin.PluginInterface.SavePluginConfig(this);
}
