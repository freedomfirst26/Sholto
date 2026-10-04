using Microsoft.Extensions.Options;
using Sholto.App;
using Sholto.App.Lifecycle;
using Sholto.App.Performance;
using Sholto.Data;
using Sholto.Interface.Bench.Controller;
using Sholto.Interface.Controller;

namespace Sholto.Interface.Bench.Headless;

/// <summary>Constructs the performance buckets and registers every command handler over the headless
/// core, installs the Inspect gate in front of every handler, wires the controller input and its LED
/// adapter to a scripted surface. The same steps as the app's <c>InputStackFactory</c> in MainUI, minus
/// the keyboard: Bench references no UI toolkit, so it cannot use that one. Bench has no database, audio
/// device or pickers, so the lifecycle is the do-nothing one and master cue goes nowhere.</summary>
public sealed class HeadlessInputStackFactory(
    IEventPublisher publisher,
    IEventSubscriber subscriber,
    ICommandRegistry registry,
    IQueryRegistry queryRegistry,
    ICommandSender sender,
    ICommandHandlersFactory handlersFactory,
    IPerformanceFactory performanceFactory,
    IControllerInputFactory controllerInputFactory,
    IOptions<ScratchOptions> scratch,
    IOptions<MagnetismOptions> magnetism,
    IMasterCueOutput masterCueOutput,
    IAppLifecycle lifecycle) : IHeadlessInputStackFactory
{
    private readonly IEventPublisher _publisher = publisher;
    private readonly IEventSubscriber _subscriber = subscriber;
    private readonly ICommandRegistry _registry = registry;
    private readonly IQueryRegistry _queryRegistry = queryRegistry;
    private readonly ICommandSender _sender = sender;
    private readonly ICommandHandlersFactory _handlersFactory = handlersFactory;
    private readonly IPerformanceFactory _performanceFactory = performanceFactory;
    private readonly IControllerInputFactory _controllerInputFactory = controllerInputFactory;
    private readonly IOptions<ScratchOptions> _scratch = scratch;
    private readonly IOptions<MagnetismOptions> _magnetism = magnetism;
    private readonly IMasterCueOutput _masterCueOutput = masterCueOutput;
    private readonly IAppLifecycle _lifecycle = lifecycle;

    public PerformanceStack Build(CoreStack core, GestureHost gestures, IAppThread appThread)
    {
        var cueRouting = new CueRouting(core.Decks, _masterCueOutput, _publisher);
        var performance = _performanceFactory.Build(core.Decks, core.Playback, _scratch, _magnetism, gestures.Clock);

        // Inspect is App state; Bench commands always run (only Controller/Keyboard are gated), but the
        // handlers are registered through the same gate the app uses.
        var inspectMode = new InspectMode(_publisher);
        var gatedRegistry = new InspectGatedCommandRegistry(_registry, inspectMode, _publisher,
            [InterfaceIds.Controller, InterfaceIds.Keyboard]);
        _handlersFactory.Register(gatedRegistry, _queryRegistry, core, cueRouting, inspectMode, performance.Platter, _lifecycle);
        _ = new MagnetEligibilityPublisher(performance.Magnet, _publisher);
        _subscriber.Subscribe<InspectModeChanged>(performance.Scratch);

        cueRouting.Start();
        var feedback = new ControllerFeedback(gestures.Surface, _subscriber);
        feedback.Subscribe();

        _controllerInputFactory.Create(gestures.Surface, _sender, appThread, gestures.Clock).Start();

        gestures.Start(performance);
        return performance;
    }
}
