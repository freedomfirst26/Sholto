namespace Sholto.Data.Tests;

public sealed class ManualFrameClockTests
{
    private readonly DateTime _start = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Now_WithStart_ReportsStartUntilAdvanced()
    {
        var clock = new ManualFrameClock(_start);

        Assert.Equal(_start, clock.Now);
        Assert.Equal(_start, clock.Now);
    }

    [Fact]
    public void Advance_WithStart_MovesNowByExactlySpan()
    {
        var clock = new ManualFrameClock(_start);
        var span = TimeSpan.FromTicks(160_001);

        clock.Advance(span);
        clock.Advance(span);

        Assert.Equal(_start + span + span, clock.Now);
    }

    [Fact]
    public void Tick_WithStart_DispatchesTheAdvancedNow()
    {
        var clock = new ManualFrameClock(_start);
        DateTime? seen = null;
        clock.Subscribe(new LambdaFrameHandler(now => seen = now), 0);

        clock.Advance(TimeSpan.FromMilliseconds(16));
        clock.Tick();

        Assert.Equal(_start + TimeSpan.FromMilliseconds(16), seen);
    }

    [Fact]
    public void Now_WithoutStart_ReportsWallTime()
    {
        var clock = new ManualFrameClock();

        var before = DateTime.UtcNow;
        var now = clock.Now;
        var after = DateTime.UtcNow;

        Assert.InRange(now, before, after);
    }

    [Fact]
    public void Advance_WithoutStart_Throws()
    {
        var clock = new ManualFrameClock();

        Assert.Throws<InvalidOperationException>(() => clock.Advance(TimeSpan.FromMilliseconds(16)));
    }
}
