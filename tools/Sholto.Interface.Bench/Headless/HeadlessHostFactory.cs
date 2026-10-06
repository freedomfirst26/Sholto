using Microsoft.Extensions.Options;
using Sholto.App;
using Sholto.App.Audio;
using Sholto.App.Dsp;
using Sholto.App.Lifecycle;
using Sholto.App.Performance;
using Sholto.Data;
using Sholto.Interface.Bench.Behaviour;
using Sholto.Interface.Bench.Controller;
using Sholto.Interface.Bench.Rendering;
using Sholto.Interface.Bench.Scenario;
using Sholto.Interface.Controller;
using Sholto.Interface.Controller.Mappings;

namespace Sholto.Interface.Bench.Headless;

/// <summary>
/// Composes the headless graph: the one bus (deck sessions publish, the controller adapter subscribes,
/// the command handlers register), the core factory, the input stack factory, and the
/// <see cref="HeadlessHost"/> over them. Everything runs on the caller's thread.
/// </summary>
/// <param name="benchDeck">The deck the core factory renders through.</param>
/// <param name="deckFactory">The factory the deck sessions' decks are built through.</param>
/// <param name="decoder">Real MP3/WAV decode for files the streaming provider cannot resample.</param>
/// <param name="deckAdvance">Moves deck position on a "wait" step.</param>
/// <param name="appThread">The immediate app thread the controller input is posted on.</param>
/// <param name="crossfade">The crossfade curve the scenario runner applies.</param>
public sealed class HeadlessHostFactory(IBenchDeck benchDeck, IBenchDeckFactory deckFactory, IAudioFileDecoder decoder,
    IDeckAdvance deckAdvance, IAppThread appThread, ICrossfadeCurve crossfade) : IHeadlessHostFactory
{
    private readonly IBenchDeck _benchDeck = benchDeck;
    private readonly IBenchDeckFactory _deckFactory = deckFactory;
    private readonly IAudioFileDecoder _decoder = decoder;
    private readonly IDeckAdvance _deckAdvance = deckAdvance;
    private readonly IAppThread _appThread = appThread;
    private readonly ICrossfadeCurve _crossfade = crossfade;

    public IHeadlessHost Create()
    {
        var bus = new DataBus(new BenchHandlerFailureSink());
        var coreFactory = new HeadlessCoreFactory(_benchDeck, _deckFactory, _decoder, bus, new ManualFrameClock(), _appThread);
        var stackFactory = new HeadlessInputStackFactory(
            bus, bus, bus, bus, bus,
            new CommandHandlersFactory(_appThread, bus, new NullHintCounter(), Options.Create(new GlanceOptions())),
            new PerformanceFactory(),
            new ControllerInputFactory(),
            Options.Create(new ScratchOptions()),
            Options.Create(new MagnetismOptions()),
            new MasterCueOutput(),
            new NullAppLifecycle());
        var routers = new CueOutputRouterFactory();
        return new HeadlessHost(
            coreFactory,
            new GestureHostFactory(Options.Create(new ControllerMappingsOptions())),
            stackFactory,
            _appThread,
            _deckAdvance,
            new ScenarioGestureFactory(),
            new CoreSnapshot(),
            new StateDiff(),
            _crossfade,
            new OfflineRenderer(_benchDeck, _crossfade, routers),
            routers,
            _deckFactory);
    }
}
