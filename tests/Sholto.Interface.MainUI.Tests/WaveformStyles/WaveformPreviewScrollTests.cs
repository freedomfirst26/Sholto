using Sholto.Data;
using Sholto.Interface.MainUI.ViewModels;

namespace Sholto.Interface.MainUI.Tests.WaveformStyles;

public class WaveformPreviewScrollTests
{
    private sealed class RecordingClock : IFrameClock
    {
        public DateTime Now => DateTime.UtcNow;
        public readonly List<(IFrameTickHandler Handler, int Order)> Subscribed = [];
        public void Subscribe(IFrameTickHandler handler, int order) => Subscribed.Add((handler, order));
    }

    private static readonly DateTime T0 = new(2026, 10, 4, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Subscribes_to_the_frame_clock_after_the_performance_tick()
    {
        var clock = new RecordingClock();
        var scroll = new WaveformPreviewScroll(clock);
        var (handler, order) = Assert.Single(clock.Subscribed);
        Assert.Same(scroll, handler);
        Assert.True(order >= 100);
    }

    [Fact]
    public void Advances_at_the_given_rate_and_wraps_at_the_end()
    {
        var scroll = new WaveformPreviewScroll(new RecordingClock());
        scroll.Start(1000, 50, 0);
        scroll.OnFrame(T0);
        scroll.OnFrame(T0.AddSeconds(0.1));
        Assert.Equal(5, scroll.Position, 6);

        // 1 s at 50 columns/s from 990 wraps past 1000.
        scroll.Start(1000, 50, 990);
        scroll.OnFrame(T0);
        for (int i = 1; i <= 10; i++) scroll.OnFrame(T0.AddSeconds(0.1 * i));
        Assert.Equal(40, scroll.Position, 6);
    }

    [Fact]
    public void Does_nothing_until_started_and_after_stopped()
    {
        var scroll = new WaveformPreviewScroll(new RecordingClock());
        int moved = 0;
        scroll.Moved += (_, _) => moved++;
        scroll.OnFrame(T0);
        scroll.OnFrame(T0.AddSeconds(1));
        Assert.Equal(0, moved);

        scroll.Start(1000, 50, 0);
        scroll.OnFrame(T0);
        scroll.OnFrame(T0.AddSeconds(0.05));
        Assert.Equal(1, moved);

        scroll.Stop();
        scroll.OnFrame(T0.AddSeconds(0.2));
        Assert.Equal(1, moved);
    }

    [Fact]
    public void A_stalled_frame_does_not_make_the_preview_jump()
    {
        var scroll = new WaveformPreviewScroll(new RecordingClock());
        scroll.Start(1000, 50, 0);
        scroll.OnFrame(T0);
        scroll.OnFrame(T0.AddSeconds(30));
        Assert.Equal(5, scroll.Position, 6);
    }

    [Fact]
    public void A_frame_allocates_nothing()
    {
        var scroll = new WaveformPreviewScroll(new RecordingClock());
        scroll.Moved += (_, _) => { };
        scroll.Start(1000, 50, 0);
        var now = T0;
        for (int i = 0; i < 100; i++) scroll.OnFrame(now = now.AddMilliseconds(16));

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) scroll.OnFrame(now = now.AddMilliseconds(16));
        long after = GC.GetAllocatedBytesForCurrentThread();

        Assert.Equal(0, after - before);
    }
}
