using System;
using System.Linq;
using System.Threading.Tasks;
using Babel14.Services;
using Babel14.Windows;
using Dalamud.Game.Command;
using Dalamud.Game.Text;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Interface.Windowing;

namespace Babel14;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;

    private const string CommandName = "/babel";

    public Configuration Configuration { get; init; }
    public LlmClient Llm { get; }
    public ChatTranslator Translator { get; }

    public readonly WindowSystem WindowSystem = new("Babel14");
    private readonly BabelWindow window;

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        Llm = new LlmClient();
        Llm.Configure(Configuration.ApiBaseUrl, Configuration.ApiKey);
        Translator = new ChatTranslator(this, Llm, ChatGui, Log);

        window = new BabelWindow(this, Log);
        WindowSystem.AddWindow(window);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Toggle the Babel translation window. /babel send <text> - translate & print to chat (local)."
        });

        PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleMain;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMain;

        Log.Information("Babel14 loaded");
    }

    public void Dispose()
    {
        PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleMain;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMain;

        WindowSystem.RemoveAllWindows();
        window.Dispose();
        Translator.Dispose();
        Llm.Dispose();

        CommandManager.RemoveHandler(CommandName);
    }

    private void ToggleMain() => window.Toggle();

    private void OnCommand(string command, string args)
    {
        if (args.StartsWith("send ", StringComparison.OrdinalIgnoreCase))
        {
            var text = args[5..].Trim();
            if (text.Length > 0)
                _ = SendFromCommandAsync(text);
            return;
        }

        window.Toggle();
    }

    private async Task SendFromCommandAsync(string text)
    {
        try
        {
            var target = Configuration.KnownLanguages.Count > 0
                ? Configuration.KnownLanguages[0]
                : "English";
            // Command sending uses the "other" language: translate outgoing into the first
            // known language that differs from the native one, if any.
            var other = Configuration.KnownLanguages.FirstOrDefault(l =>
                !string.IsNullOrWhiteSpace(l) &&
                !l.Equals(Configuration.NativeLanguage, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(other))
                target = other;

            var translated = await Translator.TranslateOutgoingAsync(text, target).ConfigureAwait(false);
            Translator.MarkOwn(translated);

            ChatGui.Print(new XivChatEntry
            {
                Type = XivChatType.Echo,
                Message = translated,
            });
        }
        catch (OperationCanceledException)
        {
            // Plugin unloading.
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Babel: /babel send failed");
            ChatGui.PrintError($"[babel] {ex.Message}");
        }
    }
}
