using System;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Babel14.Services;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Dalamud.Game.Text;
using Dalamud.Plugin.Services;

namespace Babel14.Windows;

public class BabelWindow : Window, IDisposable
{
    private readonly Plugin plugin;
    private readonly IPluginLog log;

    // Translate tab state
    private string outgoingText = string.Empty;
    private string targetLanguage = string.Empty;
    private string sendStatus = string.Empty;
    private bool sending;

    // Settings tab state
    private string apiKeyBuffer = string.Empty;
    private string testResult = string.Empty;
    private bool testing;
    private bool customModel;

    public BabelWindow(Plugin plugin, IPluginLog log) : base("Babel##BabelMainWindow")
    {
        this.plugin = plugin;
        this.log = log;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(400, 340),
            MaximumSize = new Vector2(800, 900),
        };
    }

    public void Dispose() { }

    private string FirstKnownLanguage() =>
        plugin.Configuration.KnownLanguages.FirstOrDefault(l => !string.IsNullOrWhiteSpace(l))
        ?? Languages.DefaultKnown;

    public override void Draw()
    {
        if (!ImGui.BeginTabBar("##babelTabs"))
            return;

        if (ImGui.BeginTabItem("Translate"))
        {
            DrawTranslateTab();
            ImGui.EndTabItem();
        }
        if (ImGui.BeginTabItem("Settings"))
        {
            DrawSettingsTab();
            ImGui.EndTabItem();
        }
        ImGui.EndTabBar();
    }

    private void DrawTranslateTab()
    {
        ImGui.TextWrapped("Compose a message in your language; it will be translated, printed to local chat, and copied to your clipboard.");
        ImGui.Spacing();

        var target = targetLanguage;
        if (string.IsNullOrWhiteSpace(target))
            target = FirstKnownLanguage();
        var targetIndex = Languages.IndexOf(target);
        ImGui.SetNextItemWidth(220);
        if (ImGui.Combo("Target language", ref targetIndex, Languages.All))
            targetLanguage = Languages.All[targetIndex];

        ImGui.Spacing();
        var message = outgoingText;
        var flags = ImGuiInputTextFlags.EnterReturnsTrue | ImGuiInputTextFlags.CtrlEnterForNewLine;
        var submitted = ImGui.InputTextMultiline("##outgoing", ref message, 2000, new Vector2(-1, 90), flags);
        outgoingText = message;

        ImGui.BeginDisabled(sending);
        if (ImGui.Button(sending ? "Translating..." : "Translate & Copy to Clipboard", new Vector2(-1, 0)) || (submitted && !sending))
        {
            if (!sending && !string.IsNullOrWhiteSpace(outgoingText))
                _ = SendAsync();
        }
        ImGui.EndDisabled();

        if (!string.IsNullOrEmpty(sendStatus))
        {
            ImGui.Spacing();
            ImGui.TextWrapped(sendStatus);
        }
    }

    private async Task SendAsync()
    {
        var config = plugin.Configuration;
        sending = true;
        sendStatus = "Translating...";
        var sent = outgoingText;
        try
        {
            var target = string.IsNullOrWhiteSpace(targetLanguage)
                ? config.KnownLanguages.FirstOrDefault(l => !string.IsNullOrWhiteSpace(l)) ?? "English"
                : targetLanguage.Trim();

            var translated = await plugin.Translator.TranslateOutgoingAsync(sent.Trim(), target)
                .ConfigureAwait(false);
            plugin.Translator.MarkOwn(translated);

            // Chat and ImGui must be touched on the framework/UI thread.
            await Plugin.Framework.Run(() =>
            {
                Plugin.ChatGui.Print(new XivChatEntry
                {
                    Type = XivChatType.Echo,
                    Message = translated,
                });
                ImGui.SetClipboardText(translated);
                // Keep anything typed while the translation was running.
                if (outgoingText == sent)
                    outgoingText = string.Empty;
            }).ConfigureAwait(false);

            sendStatus = $"Copied to clipboard & printed locally: {translated}";
        }
        catch (OperationCanceledException)
        {
            sendStatus = "Cancelled.";
        }
        catch (Exception ex)
        {
            log.Error(ex, "Babel: outgoing translation failed");
            sendStatus = $"Error: {ex.Message}";
        }
        finally
        {
            sending = false;
        }
    }

    private void DrawSettingsTab()
    {
        var config = plugin.Configuration;
        var dirty = false;

        ImGui.Text("LLM backend (OpenAI-compatible chat completions)");
        ImGui.Spacing();

        var baseUrl = config.ApiBaseUrl;
        ImGui.SetNextItemWidth(320);
        if (ImGui.InputText("Base URL", ref baseUrl, 256))
        {
            config.ApiBaseUrl = baseUrl.TrimEnd('/');
            dirty = true;
        }

        var pick = customModel || Models.IndexOf(config.Model) < 0
            ? Models.CustomIndex
            : Models.IndexOf(config.Model);
        ImGui.SetNextItemWidth(320);
        if (ImGui.Combo("Model", ref pick, Models.Labels))
        {
            customModel = pick == Models.CustomIndex;
            if (!customModel)
            {
                config.Model = Models.All[pick].Id;
                dirty = true;
            }
        }

        if (customModel || Models.IndexOf(config.Model) < 0)
        {
            var slug = config.Model;
            ImGui.SetNextItemWidth(320);
            if (ImGui.InputText("Model slug", ref slug, 128))
            {
                config.Model = slug.Trim();
                dirty = true;
            }

            ImGui.TextDisabled("Any OpenRouter model id, e.g. vendor/model or vendor/model:free.");
        }

        // API key is kept in a UI-only buffer until edited; it is never re-displayed.
        if (apiKeyBuffer.Length == 0 && !string.IsNullOrEmpty(config.ApiKey))
            ImGui.TextDisabled("API key is set. Type a new key to replace it.");
        var key = apiKeyBuffer;
        ImGui.SetNextItemWidth(320);
        if (ImGui.InputText("API Key", ref key, 256, ImGuiInputTextFlags.Password))
        {
            apiKeyBuffer = key;
            if (key.Length > 0)
            {
                config.ApiKey = key;
                dirty = true;
            }
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Text("Languages");
        ImGui.Spacing();

        var nativeIndex = Languages.IndexOf(config.NativeLanguage);
        ImGui.SetNextItemWidth(200);
        if (ImGui.Combo("Translate incoming into", ref nativeIndex, Languages.All))
        {
            config.NativeLanguage = Languages.All[nativeIndex];
            dirty = true;
        }

        for (var i = 0; i < config.KnownLanguages.Count; i++)
        {
            ImGui.PushID(i);
            var langIndex = Languages.IndexOf(config.KnownLanguages[i]);
            ImGui.SetNextItemWidth(200);
            if (ImGui.Combo("##lang", ref langIndex, Languages.All))
            {
                config.KnownLanguages[i] = Languages.All[langIndex];
                dirty = true;
            }
            ImGui.SameLine();
            if (ImGui.Button("Remove"))
            {
                config.KnownLanguages.RemoveAt(i);
                dirty = true;
                ImGui.PopID();
                break;
            }
            ImGui.PopID();
        }
        if (ImGui.Button("+ Add known language"))
        {
            config.KnownLanguages.Add(
                Languages.All.FirstOrDefault(l => !config.KnownLanguages.Contains(l)) ?? Languages.All[0]);
            dirty = true;
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        var translateIncoming = config.TranslateIncoming;
        if (ImGui.Checkbox("Translate incoming chat", ref translateIncoming))
        {
            config.TranslateIncoming = translateIncoming;
            dirty = true;
        }

        ImGui.Spacing();
        ImGui.BeginDisabled(testing);
        if (ImGui.Button("Test connection", new Vector2(140, 0)))
            _ = TestAsync();
        ImGui.EndDisabled();
        if (testing)
            ImGui.TextDisabled("testing...");
        else if (!string.IsNullOrEmpty(testResult))
            ImGui.TextWrapped(testResult);

        if (dirty)
        {
            plugin.Llm.Configure(config.ApiBaseUrl, config.ApiKey);
            config.Save();
        }
    }

    private async Task TestAsync()
    {
        var config = plugin.Configuration;
        testing = true;
        testResult = string.Empty;
        try
        {
            if (string.IsNullOrWhiteSpace(config.ApiKey))
                throw new InvalidOperationException("Set your API key first.");

            plugin.Llm.Configure(config.ApiBaseUrl, config.ApiKey);
            var reply = await plugin.Llm.TestAsync(config.Model).ConfigureAwait(false);
            testResult = $"OK - model replied: {reply}";
        }
        catch (Exception ex)
        {
            log.Error(ex, "Babel: connection test failed");
            testResult = $"Failed: {ex.Message}";
        }
        finally
        {
            testing = false;
        }
    }
}
