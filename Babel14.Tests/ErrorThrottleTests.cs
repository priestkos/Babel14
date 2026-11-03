using Babel14.Services;
using Xunit;

namespace Babel14.Tests;

public class ErrorThrottleTests
{
    [Fact]
    public void OneLinePerWindowAndCountsSuppressed()
    {
        var t0 = new DateTime(2026, 1, 1);
        var throttle = new ErrorThrottle(TimeSpan.FromSeconds(30));

        Assert.Equal("e1", throttle.Next("e1", t0));
        Assert.Null(throttle.Next("e2", t0.AddSeconds(1)));
        Assert.Null(throttle.Next("e3", t0.AddSeconds(29)));
        Assert.Equal("e4 (2 similar suppressed)", throttle.Next("e4", t0.AddSeconds(30)));
        Assert.Equal("e5", throttle.Next("e5", t0.AddSeconds(61)));
    }
}
