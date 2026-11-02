using System.Text;

namespace Babel14.Services;

/// <summary>
/// Decides whether a reply is just the original text again, so same-language chat is not
/// echoed back as a "translation". Models routinely strip quotes, padding or punctuation and
/// change case when they pass a message through unchanged, which a raw string compare misses.
/// </summary>
public static class TextMatch
{
    public static bool Same(string left, string right) =>
        Normalise(left) is { Length: > 0 } a && a == Normalise(right);

    /// <summary>Only letters and digits survive, lowercased: punctuation, quotes and emoji are noise here.</summary>
    private static string Normalise(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        var sb = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            if (char.IsLetterOrDigit(c))
                sb.Append(char.ToLowerInvariant(c));
        }

        return sb.ToString();
    }
}
