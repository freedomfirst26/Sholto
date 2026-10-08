using Sholto.App;
using Sholto.Data;
using Xunit;

namespace Sholto.App.Tests;

/// <summary>The Inspect gate on the bus path: while Inspect is on, the controller's and keyboard's commands
/// are echoed as <see cref="CommandReceived"/> instead of executed; every other origin always runs.</summary>
public class InspectGateTests
{
    private static Origin From(string interfaceId, string control = "deck.play", string gesture = "play.press",
        int deck = Origin.NoDeck) =>
        new(interfaceId, control, gesture, deck);

    private readonly DataBus _bus = new(new ThrowingFailureSink());
    private readonly InspectMode _inspect;
    private readonly CountingCommandHandler<TogglePlay> _play = new();
    private readonly CountingCommandHandler<SetCrossfader> _crossfader = new();
    private readonly CountingCommandHandler<ReportControl> _report = new();
    private readonly CountingEventHandler<CommandReceived> _echoed = new();

    public InspectGateTests()
    {
        _inspect = new InspectMode(_bus);
        var registry = new InspectGatedCommandRegistry(_bus, _inspect, _bus,
            [InterfaceIds.Controller, InterfaceIds.Keyboard]);
        registry.Register<TogglePlay>(_play);
        registry.Register<SetCrossfader>(_crossfader);
        registry.Register<ReportControl>(_report);
        registry.Register<SetInspectMode>(_inspect);
        _bus.Subscribe<CommandReceived>(_echoed);
    }

    [Theory]
    [InlineData(InterfaceIds.Controller)]
    [InlineData(InterfaceIds.Keyboard)]
    [InlineData(InterfaceIds.MainUI)]
    public void Outside_inspect_every_origin_executes_and_nothing_is_echoed(string interfaceId)
    {
        _bus.Send(new TogglePlay(1, From(interfaceId)));

        Assert.Equal(1, _play.Count);
        Assert.Equal(0, _echoed.Count);
    }

    [Theory]
    [InlineData(InterfaceIds.Controller)]
    [InlineData(InterfaceIds.Keyboard)]
    public void In_inspect_a_controller_or_keyboard_command_is_echoed_and_not_executed(string interfaceId)
    {
        _bus.Send(new SetInspectMode(true, From(InterfaceIds.Faceplate, "faceplate", "mount")));

        var origin = From(interfaceId, deck: 1);
        _bus.Send(new TogglePlay(1, origin));

        Assert.Equal(0, _play.Count);
        Assert.Equal(1, _echoed.Count);
        Assert.Equal(new CommandReceived(origin, "TogglePlay"), _echoed.Last);
        Assert.Equal(1, _echoed.Last.Origin.Deck);
    }

    [Theory]
    [InlineData(InterfaceIds.MainUI)]
    [InlineData(InterfaceIds.Faceplate)]
    [InlineData(InterfaceIds.Bench)]
    public void In_inspect_the_other_interfaces_still_execute(string interfaceId)
    {
        _bus.Send(new SetInspectMode(true, From(InterfaceIds.Faceplate, "faceplate", "mount")));

        _bus.Send(new TogglePlay(0, From(interfaceId)));

        Assert.Equal(1, _play.Count);
        Assert.Equal(0, _echoed.Count);
    }

    [Fact]
    public void A_global_control_is_echoed_with_no_deck()
    {
        _bus.Send(new SetInspectMode(true, From(InterfaceIds.Faceplate, "faceplate", "mount")));

        _bus.Send(new SetCrossfader(0.5, From(InterfaceIds.Controller, "mixer.crossfader", "crossfader.move")));

        Assert.Equal(0, _crossfader.Count);
        Assert.Equal("SetCrossfader", _echoed.Last.CommandName);
        Assert.Equal(Origin.NoDeck, _echoed.Last.Origin.Deck);
    }

    [Fact]
    public void Leaving_inspect_lets_the_controller_act_again()
    {
        _bus.Send(new SetInspectMode(true, From(InterfaceIds.Faceplate, "faceplate", "mount")));
        _bus.Send(new SetInspectMode(false, From(InterfaceIds.Faceplate, "faceplate", "unmount")));

        _bus.Send(new TogglePlay(0, From(InterfaceIds.Controller)));

        Assert.Equal(1, _play.Count);
        Assert.Equal(0, _echoed.Count);
    }

    [Fact]
    public void A_report_control_does_nothing_outside_inspect_and_is_echoed_in_inspect()
    {
        var shift = From(InterfaceIds.Controller, "deck.shift", "shift.hold", deck: 1);

        _bus.Send(new ReportControl(true, shift));
        Assert.Equal(0, _echoed.Count);
        Assert.Equal(1, _report.Count);

        _bus.Send(new SetInspectMode(true, From(InterfaceIds.Faceplate, "faceplate", "mount")));
        _bus.Send(new ReportControl(true, shift));

        Assert.Equal(1, _report.Count);   // not executed: echoed only
        Assert.Equal(new CommandReceived(shift, "ReportControl"), _echoed.Last);
        Assert.Equal(1, _echoed.Last.Origin.Deck);
    }

    [Fact]
    public void Inspect_mode_is_published_once_per_change_and_replayed_to_a_late_subscriber()
    {
        var changes = new CountingEventHandler<InspectModeChanged>();
        _bus.Subscribe<InspectModeChanged>(changes);

        _bus.Send(new SetInspectMode(true, From(InterfaceIds.Faceplate)));
        _bus.Send(new SetInspectMode(true, From(InterfaceIds.Faceplate)));

        Assert.Equal(1, changes.Count);
        Assert.True(_inspect.IsOn);

        var late = new CountingEventHandler<InspectModeChanged>();
        _bus.Subscribe<InspectModeChanged>(late);
        Assert.True(late.Last.On);
    }

    [Fact]
    public void The_gate_allocates_nothing_in_either_mode_after_warm_up()
    {
        var turn = new TurnPlatter(0, 1, PlatterSurface.Top, false,
            From(InterfaceIds.Controller, "deck.jog", "jog.top.turn"));
        var bus = new DataBus(new ThrowingFailureSink());
        var inspect = new InspectMode(bus);
        var registry = new InspectGatedCommandRegistry(bus, inspect, bus,
            [InterfaceIds.Controller, InterfaceIds.Keyboard]);
        var handler = new CountingCommandHandler<TurnPlatter>();
        registry.Register<TurnPlatter>(handler);
        var crossfaderHandler = new CountingCommandHandler<SetCrossfader>();
        registry.Register<SetCrossfader>(crossfaderHandler);
        registry.Register<SetInspectMode>(inspect);
        var echoes = new CountingEventHandler<CommandReceived>();
        bus.Subscribe<CommandReceived>(echoes);

        var crossfade = new SetCrossfader(0.5, From(InterfaceIds.Controller, "mixer.crossfader", "crossfader.move"));

        long Measure()
        {
            for (var i = 0; i < 2_000; i++) bus.Send(turn);
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 10_000; i++) bus.Send(turn);
            return GC.GetAllocatedBytesForCurrentThread() - before;
        }

        // A global command must not box either: guards against a default interface member on ICommand.
        long MeasureGlobal()
        {
            for (var i = 0; i < 2_000; i++) bus.Send(crossfade);
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 10_000; i++) bus.Send(crossfade);
            return GC.GetAllocatedBytesForCurrentThread() - before;
        }

        Assert.Equal(0, Measure());
        Assert.Equal(12_000, handler.Count);
        Assert.Equal(0, MeasureGlobal());
        Assert.Equal(12_000, crossfaderHandler.Count);

        bus.Send(new SetInspectMode(true, From(InterfaceIds.Faceplate)));
        Assert.Equal(0, Measure());
        Assert.Equal(12_000, handler.Count);
        Assert.Equal(12_000, echoes.Count);
        Assert.Equal(0, MeasureGlobal());
        Assert.Equal(12_000, crossfaderHandler.Count);
        Assert.Equal(24_000, echoes.Count);
    }
}
