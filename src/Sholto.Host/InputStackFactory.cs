using Sholto.Data;
using Sholto.Interface.Controller;
using Sholto.App;
using Sholto.App.Lifecycle;
using Sholto.App.Performance;
using Sholto.Interface.Keyboard;

namespace Sholto.Host;

/// <summary>Constructs the performance buckets and registers every command handler, wires the two input
/// interfaces (controller, keyboard) to send commands, installs the Inspect gate in front of every
/// handler, and the controller's LED adapter. Shared by
/// <c>App</c> and Bench's gesture host.</summary>
public sealed class InputStackFactory(
    IEventPublisher publisher,
    IEventSubscriber subscriber,
    ICommandRegistry registry,
    IQueryRegistry queryRegistry,
    ICommandSender sender,
    ICommandHandlersFactory handlersFactory,
    IPerformanceFactory performanceFactory,
    IControllerInputFactory controllerInputFactory,
    IKeyboardInputFactory keyboardInputFactory) : IInputStackFactory
{
    private readonly IEventPublisher _publisher = publisher;
    private readonly IEventSubscriber _subscriber = subscriber;
    private readonly ICommandRegistry _registry = registry;
    private readonly IQueryRegistry _queryRegistry = queryRegistry;
    private readonly ICommandSender _sender = sender;
    private readonly ICommandHandlersFactory _handlersFactory = handlersFactory;
    private readonly IPerformanceFactory _performanceFactory = performanceFactory;
    private readonly IControllerInputFactory _controllerInputFactory = controllerInputFactory;
    private readonly IKeyboardInputFactory _keyboardInputFactory = keyboardInputFactory;

    public InputStack Build(IControlSurface surface, CoreStack core, IKeyboard keyboard,
        SholtoOptions options, IFrameClock clock, IAppThread appThread, IMasterCueOutput masterCueOutput,
        IAppLifecycle lifecycle)
    {
        // Cue, master cue and pad page live here, in the App; the command handlers tell it and the
        // controller adapter follows its events.
        var cueRouting = new CueRouting(core.Decks, masterCueOutput, _publisher);
        var performance = _performanceFactory.Build(core.Decks, core.Playback, options.Scratch, options.Magnetism, clock,
            core.BackspinFeel);

        // Inspect mode is App state. Every command handler is registered through the gate, so while
        // Inspect is on the controller's and keyboard's commands are echoed (CommandReceived), not run;
        // MainUI, Faceplate and Bench commands always run. The interfaces know nothing about it.
        var inspectMode = new InspectMode(_publisher);
        var gatedRegistry = new InspectGatedCommandRegistry(_registry, inspectMode, _publisher,
            [InterfaceIds.Controller, InterfaceIds.Keyboard]);
        _handlersFactory.Register(gatedRegistry, _queryRegistry, core, cueRouting, inspectMode, performance.Platter, lifecycle);
        // The magnet's eligibility flips become a bus event (the centerline magnet glyph follows it).
        _ = new MagnetEligibilityPublisher(performance.Magnet, _publisher);
        // Leaving Inspect releases any scratch touch it stranded.
        _subscriber.Subscribe<InspectModeChanged>(performance.Scratch);

        // The lights: the decks' sessions publish their state as events (they did so from construction, so
        // the adapter's subscribe replays a full picture), cue routing publishes its own, and the
        // controller adapter maps them all to LEDs.
        cueRouting.Start();
        var feedback = new ControllerFeedback(surface, _subscriber);
        feedback.Subscribe();

        // Both input interfaces always send commands on the one bus; the gate above decides what happens
        // to them. The keyboard sends synchronously on the UI thread so Handled is set before the
        // view's key handling continues; the controller hops from the MIDI thread via the app thread.
        _controllerInputFactory.Create(surface, _sender, appThread, clock).Start();
        _keyboardInputFactory.Create(keyboard, _sender).Start();

        return new InputStack(performance, feedback);
    }
}
