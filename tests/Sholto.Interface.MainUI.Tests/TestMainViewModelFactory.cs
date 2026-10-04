using Microsoft.Extensions.Options;
using Sholto.App.Analysis.Analyzers.Keys;
using Sholto.App.Analysis.Analyzers.Segments;
using Sholto.App.Analysis.Analyzers.Waveform;
using Sholto.App.Analysis.Processing;
using Sholto.App.Analysis.Reporting;
using Sholto.App.Analysis.Stems;
using Sholto.App.Analysis.Stores;
using Sholto.Data;
using Sholto.App;
using Sholto.App.Decks;
using Sholto.Interface.MainUI.Models;
using Sholto.Interface.MainUI.Theming;
using Sholto.Interface.MainUI.ViewModels;
using Sholto.App.Audio;
using Sholto.App.Dsp;
using Sholto.App.Library;

namespace Sholto.Interface.MainUI.Tests;

/// <summary>Builds a real <see cref="MainViewModel"/> over a real headless core the way Bench's
/// BenchAppFactory does: real pure-compute collaborators, no database, no audio device, no stems on
/// disk, and decks from <see cref="TestDeckFactory"/>. Callers run
/// <see cref="AvaloniaTestApp.EnsureStarted"/> first (themes load through the
/// AssetLoader). The app thread is immediate and the bus throws on a failed handler.</summary>
internal sealed class TestMainViewModelFactory
{
    public TestApp Create(IAudioFileDecoder decoder)
    {
        var themes = new ThemeStackFactory().Build();
        // A filesystem query against a directory that never exists always answers
        // "not present".
        var stemPresence = new DemucsStemPresence(_ => Path.Combine(Path.GetTempPath(), "sholto-tests-no-stems"));
        var tagRecency = new TagRecency();
        var bus = new DataBus(new ThrowingFailureSink());
        var appThread = new ImmediateAppThread();
        var sessions = new DeckSessionFactory(
            new TestDeckFactory(), new SongSegmentAnalyzer(), new SettableFrameClock(), appThread, bus);
        var deck1 = sessions.Create(0);
        var deck2 = sessions.Create(1);
        var core = new CoreFactory(
            new TrackScanner(), new NullKeyAnalysisStore(), stemPresence,
            new AnalysisReporter(new[] { AnalysisSteps.Beats }), new KeyAnalyzer(), decoder,
            new EqualPowerCrossfade(), appThread, bus).Build(deck1, deck2);
        var rows = new LibraryRowsViewModel(bus, new TrackRowFactory(themes.Context));
        var deckViewModels = new DeckViewModelFactory(
            bus, bus, themes.Context, Options.Create(new FeatureOptions()), new WaveformPeaksFactory());
        var vm = new MainViewModel(
            themes.Context,
            themes.Catalog,
            new OverlayViewModelFactory(tagRecency, bus, bus, bus, appThread),
            rows,
            bus,
            bus,
            appThread,
            new SearchViewModel(rows.Items, new LibrarySearch(), tagRecency, bus, bus, appThread),
            new TrackActionsViewModel(),
            new OutputPickerViewModel(),
            deckViewModels.Create(0),
            deckViewModels.Create(1));
        return new TestApp(vm, core, bus);
    }
}
