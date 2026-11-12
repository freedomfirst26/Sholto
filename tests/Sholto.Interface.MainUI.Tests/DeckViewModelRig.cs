using Microsoft.Extensions.Options;
using Sholto.App.Analysis.Analyzers;
using Sholto.App.Analysis.Analyzers.Segments;
using Sholto.App.Analysis.Analyzers.Waveform;
using Sholto.App.Audio;
using Sholto.App.Library;
using Sholto.Data;
using Sholto.Interface.MainUI.Theming;
using Sholto.Interface.MainUI.ViewModels;

namespace Sholto.Interface.MainUI.Tests;

/// <summary>The shared setup of the DeckViewModel test classes: a bus, a deck 0 view model with a change
/// log, and the helpers that build decks, analyses and content events.</summary>
internal sealed class DeckViewModelRig
{
    public DeckTrack SomeTrack { get; } = new("/music/a.mp3", "Alpha", "Zed", TimeSpan.FromMinutes(3));

    public DataBus Bus { get; } = new(new ThrowingFailureSink());
    public FakeFrameClock Clock { get; } = new();
    public List<string?> Changed { get; } = [];
    public DeckViewModel Deck { get; }

    public DeckViewModelRig()
    {
        AvaloniaTestApp.EnsureStarted();
        Deck = NewDeck(0);
        Deck.PropertyChanged += (_, e) => Changed.Add(e.PropertyName);
    }

    public DeckViewModel NewDeck(int index, IThemeContext? theme = null) =>
        new DeckViewModelFactory(Bus, Bus, theme ?? new ThemeStackFactory().Build().Context,
            Options.Create(new FeatureOptions { ShowSectionMap = false }), Options.Create(new DeckViewOptions()),
            new NoPeaksFactory(), new DiscBloomFactory(Clock)).Create(index);

    public DeckAnalysis AnalysisWithBasic(double bpm = 128.0) => new(
        new WaveformPeaks(Min: [-0.5f], Max: [0.5f], Low: [0.1f], Mid: [0.2f], High: [0.3f], SamplesPerPeak: 1024, SampleRate: 48000),
        bpm, [], [], null, false, null);

    public DeckAnalysis EmptyAnalysis() => new(null, 0, [], [], null, false, null);

    public DeckContentChanged Content(
        int deck, DeckLoadState state, bool isLoaded, DeckAnalysis? analysis = null, DeckTrack? track = null) =>
        new(deck, track, state, isLoaded, analysis);

    public F9RecordingCommandHandler<T> Record<T>() where T : struct, ICommand
    {
        var handler = new F9RecordingCommandHandler<T>();
        Bus.Register<T>(handler);
        return handler;
    }
}
