using Sholto.Data;
using Sholto.Interface.Controller;
using Xunit;

namespace Sholto.App.Tests;

/// <summary>The jog is the hottest path (~100 Hz): a platter turn from the controller side, through
/// recognition, translation, the bus and the platter's seek accumulator, must allocate nothing per
/// event once warm. The event instance is reused: in production the device mapping allocates it, which
/// is outside this path.</summary>
public class PlatterCommandAllocationTests
{
    private readonly EmittingControlSurface _surface = new();
    private readonly PerformanceRig _rig = new();

    public PlatterCommandAllocationTests()
    {
        var bus = new DataBus(new ThrowingFailureSink());
        bus.Register<TurnPlatter>(_rig.Platter);
        _rig.Tick.Start();
        new ControllerInputFactory()
            .Create(_surface, bus, new ImmediateAppThread(), _rig.Clock)
            .Start();
    }

    [Theory]
    [InlineData(JogSource.SideRing)]
    [InlineData(JogSource.TopPlatter)]
    public void A_jog_turn_from_the_controller_allocates_nothing_after_warm_up(JogSource source)
    {
        var turn = new ControllerEvent.JogRotated(0, 1, source);
        for (var i = 0; i < 2_000; i++) _surface.Emit(turn);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 10_000; i++) _surface.Emit(turn);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
    }

    [Fact]
    public void A_jog_turn_reaches_the_seek_accumulator_and_the_frame_flush_clears_it()
    {
        _surface.Emit(new ControllerEvent.JogRotated(0, 5, JogSource.SideRing));

        Assert.NotEqual(0.0, _rig.Seek.PendingSeconds(0));

        _rig.Clock.Tick();

        Assert.Equal(0.0, _rig.Seek.PendingSeconds(0));
    }
}
