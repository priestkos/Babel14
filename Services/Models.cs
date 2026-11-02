using System;

namespace Babel14.Services;

/// <summary>
/// Model presets offered in Settings. The prices are a snapshot of openrouter.ai/api/v1/models
/// taken 2026-11-02, in USD per 1M tokens (prompt / completion). OpenRouter changes them without
/// notice: to refresh, re-read that endpoint and edit the labels here.
/// </summary>
public static class Models
{
    public readonly record struct Choice(string Id, string Label);

    public static readonly Choice[] All =
    {
        new("openrouter/free", "openrouter/free - free (rotates between free models)"),
        new("google/gemma-4-31b-it:free", "google/gemma-4-31b-it:free - free"),
        new("nvidia/nemotron-3.5-lightning:free", "nvidia/nemotron-3.5-lightning:free - free"),
        new("inclusionai/ling-3.0-flash", "inclusionai/ling-3.0-flash - $0.02 / $0.06 per 1M"),
        new("z-ai/glm-5.3-flash", "z-ai/glm-5.3-flash - $0.03 / $0.09 per 1M"),
        new("qwen/qwen3.7-flash", "qwen/qwen3.7-flash - $0.03 / $0.13 per 1M - recommended"),
        new("mistralai/mistral-nemo", "mistralai/mistral-nemo - $0.03 / $0.03 per 1M"),
        new("mistralai/mistral-small-24b-instruct-2501", "mistral-small-24b-instruct-2501 - $0.05 / $0.08 per 1M"),
        new("openai/gpt-5-nano", "openai/gpt-5-nano - $0.05 / $0.40 per 1M"),
        new("google/gemini-2.5-flash-lite", "google/gemini-2.5-flash-lite - $0.10 / $0.40 per 1M"),
        new("anthropic/claude-haiku-5.5", "anthropic/claude-haiku-5.5 - $0.10 / $0.50 per 1M"),
    };

    /// <summary>Combo labels: one per preset, then the free-text option last.</summary>
    public static readonly string[] Labels = BuildLabels();

    /// <summary>Index of the "enter any slug" entry, which sits after every preset.</summary>
    public static int CustomIndex => All.Length;

    /// <summary>Preset index for a configured model id, or -1 when it is not one of the presets.</summary>
    public static int IndexOf(string? id)
    {
        for (var i = 0; i < All.Length; i++)
        {
            if (string.Equals(All[i].Id, id, StringComparison.Ordinal))
                return i;
        }

        return -1;
    }

    private static string[] BuildLabels()
    {
        var labels = new string[All.Length + 1];
        for (var i = 0; i < All.Length; i++)
            labels[i] = All[i].Label;
        labels[All.Length] = "Custom - type any model slug";
        return labels;
    }
}
