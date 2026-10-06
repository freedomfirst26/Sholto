using Sholto.App.Audio;
using Sholto.App.Dsp;
using Sholto.Data;
using Sholto.Interface.Bench.Controller;
using Sholto.Interface.Bench.Rendering;
using Sholto.Interface.Bench.Scenario;

namespace Sholto.Interface.Bench.Headless;

/// <summary>
/// The headless interface. Composes the app's core with no window (<see cref="IHeadlessCoreFactory"/>),
/// puts the controller-driven input stack over it (<see cref="IHeadlessInputStackFactory"/>) on an
/// immediate app thread and a manual frame clock, and runs a scenario: deck-level actions (load / gain /
/// play / crossfader / wait) land on the two decks behind the core's deck sessions, while
/// scan / gesture / midi go through <see cref="HeadlessScenarioDriver"/> — gestures as controller events
/// through the real input stack, so as Commands on the bus.
/// </summary>
/// <param name="coreFactory">Builds the headless core.</param>
/// <param name="gestureHostFactory">Builds the scripted-surface pipeline.</param>
/// <param name="stackFactory">Builds the input stack over the core.</param>
/// <param name="appThread">The immediate app thread the controller input is posted on.</param>
/// <param name="deckAdvance">Moves deck position on a "wait" step.</param>
/// <param name="gestureFactory">Handed to each <see cref="HeadlessScenarioDriver"/>.</param>
/// <param name="snapshot">Handed to each driver.</param>
/// <param name="diff">Handed to each driver.</param>
/// <param name="crossfade">The crossfade curve the scenario runner applies.</param>
/// <param name="renderer">Pulls frames through the router into the WAV for a headless render.</param>
/// <param name="routers">Builds the router a render session pulls the decks' mix through.</param>
/// <param name="deckFactory">Supplies the device format the render session writes in.</param>
public sealed class HeadlessHost(
    IHeadlessCoreFactory coreFactory,
    IGestureHostFactory gestureHostFactory,
    IHeadlessInputStackFactory stackFactory,
    IAppThread appThread,
    IDeckAdvance deckAdvance,
    IScenarioGestureFactory gestureFactory,
    ICoreSnapshot snapshot,
    IStateDiff diff,
    ICrossfadeCurve crossfade,
    IOfflineRenderer renderer,
    ICueOutputRouterFactory routers,
    IBenchDeckFactory deckFactory) : IHeadlessHost
{
    private readonly IHeadlessCoreFactory _coreFactory = coreFactory;
    private readonly IGestureHostFactory _gestureHostFactory = gestureHostFactory;
    private readonly IHeadlessInputStackFactory _stackFactory = stackFactory;
    private readonly IAppThread _appThread = appThread;
    private readonly IDeckAdvance _deckAdvance = deckAdvance;
    private readonly IScenarioGestureFactory _gestureFactory = gestureFactory;
    private readonly ICoreSnapshot _snapshot = snapshot;
    private readonly IStateDiff _diff = diff;
    private readonly ICrossfadeCurve _crossfade = crossfade;
    private readonly IOfflineRenderer _renderer = renderer;
    private readonly ICueOutputRouterFactory _routers = routers;
    private readonly IBenchDeckFactory _deckFactory = deckFactory;

    public HeadlessSession RunScenario(Scenario.Scenario scenario)
    {
        var session = Open();

        var decks = new Dictionary<int, Deck> { [1] = session.Deck1, [2] = session.Deck2 };
        var runner = new ScenarioRunner(decks, advance: span => _deckAdvance.Advance(decks.Values, span), _crossfade)
        {
            OnUiAction = session.Driver.Handle,
        };
        runner.Run(scenario);

        return session;
    }

    public HeadlessSession RunScenario(Scenario.Scenario scenario, string outWavPath)
    {
        var (headless, session) = OpenCore();

        using var render = new RenderSession(_renderer, _routers, [headless.Deck1, headless.Deck2], headless.Engine, _deckFactory.DeckFormat, outWavPath);
        var decks = new Dictionary<int, Deck> { [1] = session.Deck1, [2] = session.Deck2 };
        var runner = new ScenarioRunner(decks, advance: render.Advance, _crossfade)
        {
            OnUiAction = session.Driver.Handle,
        };
        runner.Run(scenario);

        return session;
    }

    public HeadlessSession Open() => OpenCore().Session;

    private (HeadlessCore Core, HeadlessSession Session) OpenCore()
    {
        var headless = _coreFactory.Build();
        var gestures = _gestureHostFactory.Create();
        _stackFactory.Build(headless.Core, gestures, _appThread);
        var driver = new HeadlessScenarioDriver(
            headless.Core, gestures, [headless.Deck1, headless.Deck2], _gestureFactory, _snapshot, _diff);
        return (headless, new HeadlessSession(headless.Core, headless.Deck1, headless.Deck2, gestures, driver));
    }
}
