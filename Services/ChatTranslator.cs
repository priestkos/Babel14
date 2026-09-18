using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using BabelFFXIV.Services;
using Dalamud.Game.Chat;
using Dalamud.Game.Text;
using Dalamud.Plugin.Services;

namespace BabelFFXIV.Services;

/// <summary>
/// Watches incoming chat and translates foreign messages via the LLM,
/// printing the result as a follow-up line. Outgoing translation is exposed for the window/command.
/// </summary>
public sealed class ChatTranslator : IDisposable
{
    private readonly Plugin plugin;
    private readonly LlmClient llm;
    private readonly IChatGui chat;
    private readonly IPluginLog log;

    /// <summary>Messages already handled by us (prevents re-translating our own output).</summary>
    private readonly ConcurrentDictionary<string, byte> ownMessages = new();

    public ChatTranslator(Plugin plugin, LlmClient llm, IChatGui chat, IPluginLog log)
    {
        this.plugin = plugin;
        this.llm = llm;
        this.chat = chat;
        this.log = log;

        chat.ChatMessage += OnChatMessage;
    }

    public bool Busy { get; private set; }

    private void SetBusy(bool b) => Busy = b;

    public void MarkOwn(string text)
    {
        ownMessages[text] = 0;
        if (ownMessages.Count > 256)
            ownMessages.Clear();
    }

    private bool OwnMessage(string text) => !ownMessages.TryAdd(text, 0);

    private void OnChatMessage(IHandleableChatMessage msg)
    {
        var config = plugin.Configuration;
        if (!config.TranslateIncoming || msg.IsHandled)
            return;

        if (!config.WatchedChannels.Contains((int)(ushort)msg.LogKind))
            return;

        var text = msg.Message.TextValue;
        var senderName = msg.Sender.TextValue;
        if (string.IsNullOrWhiteSpace(text) || OwnMessage(text))
            return;

        // Copy locals so they can cross into the background task; the message object
        // is only valid during this callback.
        _ = Task.Run(() => TranslateIncomingAsync(msg.LogKind, senderName, text));
    }

    private async Task TranslateIncomingAsync(XivChatType type, string senderName, string text)
    {
        if (Busy)
            return; // one at a time; cheap rate limiting
        SetBusy(true);

        try
        {
            var config = plugin.Configuration;
            var translated = await llm.TranslateAsync(
                config.Model,
                LlmClient.SystemPrompt(config.NativeLanguage),
                text).ConfigureAwait(false);

            if (string.IsNullOrEmpty(translated) || translated == text)
                return;

            MarkOwn(translated);

            var line = config.ShowOriginal && !string.Equals(translated, text, StringComparison.OrdinalIgnoreCase)
                ? $"{translated}\n[babel {senderName}: {text}]"
                : translated;

            chat.Print(new XivChatEntry
            {
                Type = type,
                Name = senderName,
                Message = line,
            });
        }
        catch (Exception ex)
        {
            log.Error(ex, "Babel: incoming translation failed");
            chat.PrintError($"[babel] translation failed: {ex.Message}");
        }
        finally
        {
            SetBusy(false);
        }
    }

    /// <summary>Translate text for outgoing chat into the target language.</summary>
    public async Task<string> TranslateOutgoingAsync(string text, string targetLanguage)
    {
        SetBusy(true);
        try
        {
            var config = plugin.Configuration;
            var prompt =
                "You are a real-time translator inside the chat of the online game Final Fantasy XIV. " +
                $"Translate the user's message into {targetLanguage}. " +
                "Preserve tone and register (casual game chat). Keep game terms, item names, emotes inside " +
                "angle brackets like <t>, and names exactly as written. " +
                "Reply with ONLY the translation - no explanations, no quotes, no notes.";

            return await llm.TranslateAsync(config.Model, prompt, text).ConfigureAwait(false);
        }
        finally
        {
            SetBusy(false);
        }
    }

    public void Dispose() => chat.ChatMessage -= OnChatMessage;
}
