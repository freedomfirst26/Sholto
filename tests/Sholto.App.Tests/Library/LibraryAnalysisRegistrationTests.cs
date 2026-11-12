using Microsoft.Extensions.Options;
using Sholto.App.Lifecycle;
using Sholto.App.Library;
using Sholto.Data;
using Xunit;

namespace Sholto.App.Tests.Library;

/// <summary>The core builds the library-analysis handler and the command registry routes both of its
/// commands to it.</summary>
public class LibraryAnalysisRegistrationTests
{
    [Fact]
    public void The_core_builds_the_handler_and_both_commands_are_registered()
    {
        var rig = new PerformanceRig();
        var bus = new DataBus(new ThrowingFailureSink());
        var registry = new RecordingCommandRegistry();

        new CommandHandlersFactory(new ImmediateAppThread(), bus, new NullHintCounter(), Options.Create(new GlanceOptions())).Register(
            registry, bus, rig.Core, new CueRouting(rig.Core.Decks, new RecordingMasterCueOutput(), bus), new InspectMode(bus),
            rig.Platter, new NullAppLifecycle());

        Assert.IsType<LibraryAnalysisHandler>(rig.Core.LibraryAnalysis);
        Assert.Contains(typeof(AnalyzeLibrary), registry.Registered);
        Assert.Contains(typeof(CancelLibraryAnalysis), registry.Registered);
    }
}
