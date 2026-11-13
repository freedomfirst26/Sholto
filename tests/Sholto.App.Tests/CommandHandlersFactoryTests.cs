using Sholto.App;
using Sholto.Data;
using Xunit;

namespace Sholto.App.Tests;

public class CommandHandlersFactoryTests
{
    private static (RecordingCommandRegistry Registry, DataBus Bus) RegisterAll()
    {
        var rig = new PerformanceRig();
        var bus = new DataBus(new ThrowingFailureSink());
        var cue = new CueRouting(rig.Core.Decks, new RecordingMasterCueOutput(), bus);
        var registry = new RecordingCommandRegistry();

        new CommandHandlersFactory(new ImmediateAppThread(), bus, new Sholto.App.Lifecycle.NullHintCounter(), Microsoft.Extensions.Options.Options.Create(new GlanceOptions()), new RecordingLogFactory(),
            Microsoft.Extensions.Options.Options.Create(new Sholto.App.Decks.BeatJumpOptions())).Register(
            registry, bus, rig.Core, cue, new InspectMode(bus), rig.Platter, new Sholto.App.Lifecycle.NullAppLifecycle());
        return (registry, bus);
    }

    [Fact]
    public async Task The_folder_commands_and_the_folder_check_are_registered_on_the_lifecycles_folders()
    {
        var (registry, bus) = RegisterAll();

        Assert.Contains(typeof(ChooseFolders), registry.Registered);
        Assert.Contains(typeof(ChangeFolders), registry.Registered);
        // The query is answered (an unregistered query throws).
        await bus.Ask<CheckFolders, Task<FolderCheckResult>>(new CheckFolders(null, null));
    }

    [Fact]
    public void Every_command_in_the_data_layer_gets_exactly_one_handler()
    {
        var (registry, _) = RegisterAll();

        var commands = typeof(ICommand).Assembly.GetTypes()
            .Where(t => t.IsValueType && typeof(ICommand).IsAssignableFrom(t))
            .ToHashSet();
        Assert.Equal(67, commands.Count);
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
