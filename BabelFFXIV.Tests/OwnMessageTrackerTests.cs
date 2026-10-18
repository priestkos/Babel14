using BabelFFXIV.Services;
using Xunit;

namespace BabelFFXIV.Tests;

public class OwnMessageTrackerTests
{
    [Fact]
    public void Contains_does_not_insert()
    {
        var tracker = new OwnMessageTracker();
        Assert.False(tracker.Contains("hello"));
        Assert.False(tracker.Contains("hello"));
        Assert.Equal(0, tracker.Count);
    }

    [Fact]
    public void Mark_then_Contains()
    {
        var tracker = new OwnMessageTracker();
        tracker.Mark("hello");
        Assert.True(tracker.Contains("hello"));
        Assert.False(tracker.Contains("HELLO"));
    }

    [Fact]
    public void MarkAll_covers_show_original_line_and_translation()
    {
        var tracker = new OwnMessageTracker();
        const string translated = "Hello";
        const string original = "こんにちは";
        var line = $"{translated}\n[babel Bob: {original}]";

        tracker.MarkAll(translated, line);

        Assert.True(tracker.Contains(translated));
        Assert.True(tracker.Contains(line));
        Assert.False(tracker.Contains(original));
    }

    [Fact]
    public void Duplicate_foreign_lines_are_not_auto_owned()
    {
        var tracker = new OwnMessageTracker();
        Assert.False(tracker.Contains("gg"));
        Assert.False(tracker.Contains("gg"));
        tracker.Mark("gg");
        Assert.True(tracker.Contains("gg"));
    }

    [Fact]
    public void Clear_on_overflow_keeps_latest_mark()
    {
        var tracker = new OwnMessageTracker(maxEntries: 2);
        tracker.Mark("a");
        tracker.Mark("b");
        tracker.Mark("c"); // overflow → clear → re-mark c

        Assert.True(tracker.Contains("c"));
        Assert.False(tracker.Contains("a"));
        Assert.False(tracker.Contains("b"));
        Assert.Equal(1, tracker.Count);
    }

    [Fact]
    public void Mark_ignores_null_and_empty()
    {
        var tracker = new OwnMessageTracker();
        tracker.Mark(null);
        tracker.Mark("");
        tracker.MarkAll(null, "", "ok");
        Assert.Equal(1, tracker.Count);
        Assert.True(tracker.Contains("ok"));
    }
}
