using System;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Game.Chat;
using Dalamud.Game.Text;
using Dalamud.Plugin.Services;

namespace Babel14.Services;

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
    private readonly OwnMessageTracker ownMessages = new();
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly CancellationTokenSource lifetime = new();
    private readonly ErrorThrottle errorThrottle = new(TimeSpan.FromSeconds(30));
    private int pendingIncoming;
    private const int MaxPendingIncoming = 8;

    public ChatTranslator(Plugin plugin, LlmClient llm, IChatGui chat, IPluginLog log)
    {
        this.plugin = plugin;
        this.llm = llm;
        this.chat = chat;
        this.log = log;

        chat.ChatMessage += OnChatMessage;
    }

    public void MarkOwn(string text) => ownMessages.Mark(text);

    private void OnChatMessage(IHandleableChatMessage msg)
    {
        var config = plugin.Configuration;
        // No key: every request would just 401, so do not send chat out at all.
        if (!config.TranslateIncoming || msg.IsHandled || string.IsNullOrWhiteSpace(config.ApiKey))
            return;

        if (!config.WatchedChannels.Contains((int)(ushort)msg.LogKind))
            return;

        var text = msg.Message.TextValue;
        var senderName = msg.Sender.TextValue;
        if (string.IsNullOrWhiteSpace(text) || ownMessages.Contains(text))
            return;

        // shortcut: an English reader skips every Latin-script line, so French/German/Spanish/Malay go
        // untranslated too (user's choice); upgrade to a language detector if those are ever wanted.
        if (string.Equals(config.NativeLanguage, "English", StringComparison.OrdinalIgnoreCase) && TextMatch.IsLatinOnly(text))
            return;

        if (Volatile.Read(ref pendingIncoming) >= MaxPendingIncoming)
            return;

        // Copy locals so they can cross into the background task; the message object
        // is only valid during this callback.
        var type = msg.LogKind;
        var token = lifetime.Token;
        Interlocked.Increment(ref pendingIncoming);
        _ = Task.Run(() => TranslateIncomingAsync(type, senderName, text, token));
    }

    private async Task TranslateIncomingAsync(
        XivChatType type,
        string senderName,
        string text,
        CancellationToken cancellationToken)
    {
        var acquired = false;
        try
        {
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            acquired = true;

            if (cancellationToken.IsCancellationRequested || ownMessages.Contains(text))
                return;

            var config = plugin.Configuration;
            var translated = await llm.TranslateAsync(
                config.Model,
                LlmClient.SystemPrompt(config.NativeLanguage),
                text,
                cancellationToken).ConfigureAwait(false);

            if (cancellationToken.IsCancellationRequested)
                return;

            if (string.IsNullOrEmpty(translated) || TextMatch.Same(translated, text))
                return;

            // Mark the printed line so its echo through ChatMessage is not translated again.
            ownMessages.Mark(translated);

            var entry = new XivChatEntry
            {
                Type = type,
                Name = string.IsNullOrWhiteSpace(senderName) ? "(Babel)" : $"(Babel) {senderName}",
                Message = translated,
            };
            await Plugin.Framework.RunOnFrameworkThread(() => chat.Print(entry)).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Shutting down.
        }
        catch (ObjectDisposedException)
        {
            // Gate/client disposed during unload.
        }
        catch (Exception ex)
        {
            log.Error(ex, "Babel: incoming translation failed");
            var line = cancellationToken.IsCancellationRequested
                ? null
                : errorThrottle.Next($"[babel] translation failed: {ex.Message}", DateTime.UtcNow);
            if (line != null)
                await Plugin.Framework.RunOnFrameworkThread(() => chat.PrintError(line)).ConfigureAwait(false);
        }
        finally
        {
            if (acquired)
            {
                try { gate.Release(); }
                catch (ObjectDisposedException) { }
            }

            Interlocked.Decrement(ref pendingIncoming);
        }
    }

    /// <summary>Translate text for outgoing chat into the target language.</summary>
    public async Task<string> TranslateOutgoingAsync(
        string text,
        string targetLanguage,
        CancellationToken cancellationToken = default)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token, cancellationToken);
        var token = linked.Token;

        await gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            var config = plugin.Configuration;
            var prompt = LlmClient.SystemPrompt(targetLanguage);

            return await llm.TranslateAsync(config.Model, prompt, text, token).ConfigureAwait(false);
        }
        finally
        {
            try { gate.Release(); }
            catch (ObjectDisposedException) { }
        }
    }

    public void Dispose()
    {
        chat.ChatMessage -= OnChatMessage;
        lifetime.Cancel();
        lifetime.Dispose();
        gate.Dispose();
    }
}
