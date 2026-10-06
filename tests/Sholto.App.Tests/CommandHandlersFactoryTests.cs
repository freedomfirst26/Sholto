using Sholto.App;
using Sholto.Data;
using Xunit;

namespace Sholto.App.Tests;

public class CommandHandlersFactoryTests
{
    [Fact]
    public void Every_command_in_the_data_layer_gets_exactly_one_handler()
    {
        var rig = new PerformanceRig();
        var bus = new DataBus(new ThrowingFailureSink());
        var cue = new CueRouting(rig.Core.Decks, new RecordingMasterCueOutput(), bus);
        var registry = new RecordingCommandRegistry();

        new CommandHandlersFactory(new ImmediateAppThread(), bus, new Sholto.App.Lifecycle.NullHintCounter(), Microsoft.Extensions.Options.Options.Create(new GlanceOptions())).Register(
            registry, bus, rig.Core, cue, new InspectMode(bus), rig.Platter, new Sholto.App.Lifecycle.NullAppLifecycle());

        var commands = typeof(ICommand).Assembly.GetTypes()
            .Where(t => t.IsValueType && typeof(ICommand).IsAssignableFrom(t))
            .ToHashSet();
        Assert.Equal(56, commands.Count);
        Assert.Equal(commands, registry.Registered);
    }

    [Fact]
    public void Every_command_carries_an_origin_and_a_deck()
    {
        var commands = typeof(ICommand).Assembly.GetTypes()
            .Where(t => t.IsValueType && typeof(ICommand).IsAssignableFrom(t));
        Assert.All(commands, t => Assert.True(typeof(IHasOrigin).IsAssignableFrom(t), t.Name));
    }
}
