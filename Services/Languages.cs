using System;

namespace Babel14.Services;

/// <summary>
/// Languages offered by the pickers. Japanese and English lead because that is the pair the
/// plugin is built around; the rest are the extras asked for so far, not the full list.
/// </summary>
public static class Languages
{
    public const string DefaultNative = "Japanese";
    public const string DefaultKnown = "English";

    public static readonly string[] All =
    {
        DefaultNative, DefaultKnown, "French", "German", "Spanish", "Malay", "Indonesian", "Korean",
    };

    /// <summary>Index for a combo box; anything not offered falls back to the first entry.</summary>
    public static int IndexOf(string? language)
    {
        var index = Array.IndexOf(All, language);
        return index < 0 ? 0 : index;
    }
}
