using System.Collections.Concurrent;

namespace Babel14.Services;

/// <summary>
/// Tracks texts we emitted (or otherwise own) so ChatMessage handlers can ignore them.
/// Lookup never inserts; only <see cref="Mark"/> does.
/// </summary>
public sealed class OwnMessageTracker
{
    private readonly ConcurrentDictionary<string, byte> owned = new();
    private readonly int maxEntries;

    public OwnMessageTracker(int maxEntries = 256)
    {
        this.maxEntries = maxEntries > 0 ? maxEntries : 256;
    }

    public int Count => owned.Count;

    public bool Contains(string? text) =>
        !string.IsNullOrEmpty(text) && owned.ContainsKey(text);

    public void Mark(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return;

        owned[text] = 0;
        if (owned.Count <= maxEntries)
            return;

        owned.Clear();
        owned[text] = 0;
    }

    /// <summary>Mark every distinct non-empty line that will appear in chat.</summary>
    public void MarkAll(params string?[] texts)
    {
        foreach (var text in texts)
            Mark(text);
    }
}
