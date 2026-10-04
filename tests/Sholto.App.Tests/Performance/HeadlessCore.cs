using Sholto.App.Analysis.Analyzers.Keys;
using Sholto.App.Analysis.Processing;
using Sholto.App.Analysis.Analyzers.Segments;
using Sholto.App.Analysis.Reporting;
using Sholto.App.Analysis.Stems;
using Sholto.App.Analysis.Stores;
using Sholto.App.Audio;
using Sholto.App.Decks;
using Sholto.App.Dsp;
using Sholto.App.Library;
using Sholto.Data;

namespace Sholto.App.Tests;

/// <summary>A real headless <see cref="CoreStack"/> built from the Sholto.App factories: real pure-compute
/// collaborators, no database, no audio device, no stems on disk, decks from <see cref="TestDeckFactory"/>.
/// The app thread is immediate and the bus throws on a failed handler.</summary>
internal sealed class HeadlessCore
{
    public HeadlessCore()
    {
        var bus = new DataBus(new ThrowingFailureSink());
        var appThread = new ImmediateAppThread();
        var stemPresence = new DemucsStemPresence(_ => Path.Combine(Path.GetTempPath(), "sholto-tests-no-stems"));
        var sessions = new DeckSessionFactory(
            new TestDeckFactory(), new SongSegmentAnalyzer(), new SettableFrameClock(), appThread, bus);
        Core = new CoreFactory(
            new TrackScanner(), new NullKeyAnalysisStore(), stemPresence,
            new AnalysisReporter(new[] { AnalysisSteps.Beats }), new KeyAnalyzer(), new AudioFileDecoder([]),
            new EqualPowerCrossfade(), appThread, bus).Build(sessions.Create(0), sessions.Create(1));
        Bus = bus;
    }

    public CoreStack Core { get; }
    public DataBus Bus { get; }
}
