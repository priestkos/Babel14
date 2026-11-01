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

    /// <summary>True while a translation holds the gate (incoming or outgoing).</summary>
    public bool Busy => gate.CurrentCount == 0;

    public void MarkOwn(string text) => ownMessages.Mark(text);

    private void OnChatMessage(IHandleableChatMessage msg)
    {
        var config = plugin.Configuration;
        if (!config.TranslateIncoming || msg.IsHandled)
            return;

        if (!config.WatchedChannels.Contains((int)(ushort)msg.LogKind))
            return;

        var text = msg.Message.TextValue;
        var senderName = msg.Sender.TextValue;
        if (string.IsNullOrWhiteSpace(text) || ownMessages.Contains(text))
            return;

        if (Volatile.Read(ref pendingIncoming) >= MaxPendingIncoming)
            return;

        // Copy locals so they can cross into the background task; the message object
        // is only valid during this callback.
        Interlocked.Increment(ref pendingIncoming);
        _ = Task.Run(() => TranslateIncomingAsync(msg.LogKind, senderName, text, lifetime.Token));
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

            if (string.IsNullOrEmpty(translated) || translated == text)
                return;

            var line = config.ShowOriginal && !string.Equals(translated, text, StringComparison.OrdinalIgnoreCase)
                ? $"{translated}\n[babel {senderName}: {text}]"
                : translated;

            // Mark every form we may see echoed back through ChatMessage.
            ownMessages.MarkAll(translated, line);

            chat.Print(new XivChatEntry
            {
                Type = type,
                Name = senderName,
                Message = line,
            });
        }
        catch (OperationCanceledException)
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
            if (!cancellationToken.IsCancellationRequested)
                chat.PrintError($"[babel] translation failed: {ex.Message}");
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
