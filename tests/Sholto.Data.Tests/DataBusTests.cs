using Sholto.Data;

namespace Sholto.Data.Tests;

public class DataBusTests
{
    private readonly RecordingFailureSink _sink = new();
    private readonly DataBus _bus;

    public DataBusTests() => _bus = new DataBus(_sink);

    private static Origin Here => new("test", "ctl", "press");

    [Fact]
    public void Send_invokes_handler_with_the_command()
    {
        var h = new PingHandler();
        _bus.Register<Ping>(h);
        _bus.Send(new Ping(7, Here));
        Assert.Equal(1, h.Count);
        Assert.Equal(new Ping(7, Here), h.Last);
    }

    [Fact]
    public void Send_without_handler_reports_and_does_not_throw()
    {
        _bus.Send(new Ping(1, Here));
        var f = Assert.Single(_sink.Failures);
        Assert.Equal(typeof(Ping), f.MessageType);
        Assert.Null(f.Handler);
    }

    [Fact]
    public void Registering_a_command_twice_throws()
    {
        _bus.Register<Ping>(new PingHandler());
        Assert.Throws<InvalidOperationException>(() => _bus.Register<Ping>(new PingHandler()));
    }

    [Fact]
    public void Registering_a_query_twice_throws()
    {
        _bus.Register<Double, int>(new DoubleHandler());
        Assert.Throws<InvalidOperationException>(() => _bus.Register<Double, int>(new DoubleHandler()));
    }

    [Fact]
    public void Command_handler_exception_is_reported_not_rethrown()
    {
        var h = new PingHandler { OnHandle = () => throw new InvalidOperationException("boom") };
        _bus.Register<Ping>(h);
        _bus.Send(new Ping(1, Here));
        var f = Assert.Single(_sink.Failures);
        Assert.Same(h, f.Handler);
        Assert.Equal("boom", f.Exception.Message);
    }

    [Fact]
    public void Ask_returns_the_handlers_result()
    {
        _bus.Register<Double, int>(new DoubleHandler());
        Assert.Equal(42, _bus.Ask<Double, int>(new Double(21)));
    }

    [Fact]
    public void Ask_without_handler_reports_and_returns_default()
    {
        Assert.Equal(0, _bus.Ask<Double, int>(new Double(21)));
        Assert.Single(_sink.Failures);
    }

    [Fact]
    public void Publish_reaches_every_subscriber_in_subscription_order()
    {
        var order = new List<string>();
        _bus.Subscribe(new RecordingHandler<Fact>(_ => order.Add("a")));
        _bus.Subscribe(new RecordingHandler<Fact>(_ => order.Add("b")));
        _bus.Publish(new Fact(1));
        Assert.Equal(["a", "b"], order);
    }

    [Fact]
    public void Unsubscribe_stops_delivery_and_is_idempotent()
    {
        var h = new RecordingHandler<Fact>();
        var sub = _bus.Subscribe(h);
        _bus.Publish(new Fact(1));
        sub.Dispose();
        sub.Dispose();
        _bus.Publish(new Fact(2));
        Assert.Equal([new Fact(1)], h.Received);
    }

    [Fact]
    public void Late_subscriber_gets_last_state_per_slot_immediately()
    {
        _bus.Publish(new DeckState(0, 1));
        _bus.Publish(new DeckState(1, 10));
        _bus.Publish(new DeckState(0, 2));
        var h = new RecordingHandler<DeckState>();
        _bus.Subscribe(h);
        Assert.Equal(2, h.Received.Count);
        Assert.Contains(new DeckState(0, 2), h.Received);
        Assert.Contains(new DeckState(1, 10), h.Received);
    }

    [Fact]
    public void Facts_are_not_replayed()
    {
        _bus.Publish(new Fact(1));
        var h = new RecordingHandler<Fact>();
        _bus.Subscribe(h);
        Assert.Empty(h.Received);
    }

    [Fact]
    public void State_subscriber_also_receives_later_publishes()
    {
        var h = new RecordingHandler<DeckState>();
        _bus.Subscribe(h);
        _bus.Publish(new DeckState(0, 5));
        Assert.Equal([new DeckState(0, 5)], h.Received);
    }

    [Fact]
    public void Event_handler_exception_is_reported_and_others_still_run()
    {
        var bad = new RecordingHandler<Fact>(_ => throw new InvalidOperationException("boom"));
        var good = new RecordingHandler<Fact>();
        _bus.Subscribe(bad);
        _bus.Subscribe(good);
        _bus.Publish(new Fact(3));
        Assert.Single(_sink.Failures);
        Assert.Equal([new Fact(3)], good.Received);
    }

    [Fact]
    public void Reentrant_publish_is_delivered_depth_first()
    {
        var seen = new List<int>();
        var h = new RecordingHandler<Fact>(f =>
        {
            seen.Add(f.Value);
            if (f.Value < 3) _bus.Publish(new Fact(f.Value + 1));
        });
        _bus.Subscribe(h);
        _bus.Publish(new Fact(1));
        Assert.Equal([1, 2, 3], seen);
        Assert.Empty(_sink.Failures);
    }

    [Fact]
    public void Runaway_reentrant_publish_is_cut_off_and_reported()
    {
        var count = 0;
        var h = new RecordingHandler<Fact>(_ => { count++; _bus.Publish(new Fact(0)); });
        _bus.Subscribe(h);
        _bus.Publish(new Fact(0));
        Assert.Equal(DataBus.MaxPublishDepth, count);
        Assert.Single(_sink.Failures);
    }

    [Fact]
    public void Handler_may_unsubscribe_itself_during_publish()
    {
        IDisposable? sub = null;
        var h = new RecordingHandler<Fact>(_ => sub!.Dispose());
        var other = new RecordingHandler<Fact>();
        sub = _bus.Subscribe(h);
        _bus.Subscribe(other);
        _bus.Publish(new Fact(1));
        _bus.Publish(new Fact(2));
        Assert.Single(h.Received);
        Assert.Equal(2, other.Received.Count);
    }

    [Fact]
    public void Send_and_Publish_of_a_struct_allocate_nothing_after_warmup()
    {
        var ping = new PingHandler();
        _bus.Register<Ping>(ping);
        _bus.Subscribe(new NoopHandler<DeckState>());
        _bus.Subscribe(new NoopHandler<Fact>());
        for (var i = 0; i < 1000; i++)
        {
            _bus.Send(new Ping(i, Here));
            _bus.Publish(new Fact(i));
            _bus.Publish(new DeckState(i & 1, i));
        }
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++)
        {
            _bus.Send(new Ping(i, Here));
            _bus.Publish(new Fact(i));
            _bus.Publish(new DeckState(i & 1, i));
        }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Fact]
    public void Manual_frame_clock_runs_handlers_in_order_then_subscription()
    {
        var clock = new ManualFrameClock();
        var seen = new List<string>();
        clock.Subscribe(new LambdaFrameHandler(_ => seen.Add("late")), 100);
        clock.Subscribe(new LambdaFrameHandler(_ => seen.Add("app")), 0);
        clock.Subscribe(new LambdaFrameHandler(_ => seen.Add("app2")), 0);
        clock.Tick();
        Assert.Equal(["app", "app2", "late"], seen);
    }
}
