using System;
using NuclearMeltdown.Core;
using Xunit;

public class MeltdownClockTests
{
    [Fact]
    public void Not_expired_before_years_elapse()
    {
        var start = new DateTime(2000, 1, 1);
        var now = new DateTime(2049, 12, 31);
        Assert.False(MeltdownClock.HasExpired(start.Ticks, now.Ticks, 50));
    }

    [Fact]
    public void Expired_exactly_at_boundary()
    {
        var start = new DateTime(2000, 1, 1);
        var now = new DateTime(2050, 1, 1);
        Assert.True(MeltdownClock.HasExpired(start.Ticks, now.Ticks, 50));
    }

    [Fact]
    public void Expired_after_boundary()
    {
        var start = new DateTime(2000, 6, 15);
        var now = new DateTime(2051, 1, 1);
        Assert.True(MeltdownClock.HasExpired(start.Ticks, now.Ticks, 50));
    }
}
