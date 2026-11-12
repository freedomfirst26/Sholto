using Microsoft.Extensions.Options;
using Sholto.Interface.MainUI.Controls.CollapseToIcon;
using Sholto.Interface.MainUI.Controls.Knob;
using Sholto.Interface.MainUI.Controls.WaveformStyles;
using Sholto.App.Analysis.Analyzers.Segments;
using Sholto.App.Analysis.Analyzers.Waveform;
using Sholto.App.Audio;
using Sholto.App.Library;
using Sholto.Data;
using Sholto.Interface.MainUI.Models;
using Sholto.Interface.MainUI.Theming;
using Sholto.Interface.MainUI.ViewModels;
using Sholto.Interface.MainUI.ViewModels.Glance;

namespace Sholto.Interface.MainUI.Tests;

/// <summary>The shared setup of the MainViewModel test classes: a real bus with the real library rows, search,
/// overlay factory and deck view models; the app thread is immediate so nothing touches Avalonia's
/// dispatcher.</summary>
internal sealed class MainViewModelRig
{
    public DataBus Bus { get; } = new(new ThrowingFailureSink());
    public List<string?> Changed { get; } = [];
    public MainViewModel Vm { get; }

    public MainViewModelRig()
    {
        AvaloniaTestApp.EnsureStarted();
        var themes = new ThemeStackFactory().Build();
        var appThread = new ImmediateAppThread();
        var recency = new TagRecency();
        var rows = new LibraryRowsViewModel(Bus, new TrackRowFactory(themes.Context));
        var clock = new FakeFrameClock();
        var deck1 = new DeckViewModelFactory(Bus, Bus, themes.Context, Options.Create(new FeatureOptions { ShowSectionMap = false }),
            Options.Create(new DeckViewOptions()), new NoPeaksFactory(), new DiscBloomFactory(new ManualFrameClock())).Create(0);
        var deck2 = new DeckViewModelFactory(Bus, Bus, themes.Context, Options.Create(new FeatureOptions { ShowSectionMap = false }),
            Options.Create(new DeckViewOptions()), new NoPeaksFactory(), new DiscBloomFactory(new ManualFrameClock())).Create(1);
        var clocks = new DeckViewModelClockSource(deck1, deck2);
        var glance = new GlanceViewModelFactory(Bus, Bus, Bus, appThread, clock, rows, recency,
            new GlanceHeaderViewModel(clocks, clock, Bus, new DeckSlotFactory(clocks, new FixedMotionPreference(false), Options.Create(new GlanceViewOptions()))),
            new FixedMotionPreference(false), Options.Create(new GlanceViewOptions())).Create();
        var waveformStyle = new WaveformStyleViewModel(new WaveformStylesFactory(Options.Create(new WaveformStyleOptions())).Create(), Bus);
        var themeViewModel = new ThemeViewModel(themes.Context, themes.Catalog, Bus);
        Vm = new MainViewModelFactory(
            themeViewModel,
            new OverlayViewModelFactory(recency, Bus, Bus, Bus, appThread),
            rows,
            Bus,
            Bus,
            appThread,
            glance,
            new LoadFeedbackViewModel(Bus, Bus),
            new TrackActionsViewModel(),
            new OutputPickerViewModel(),
            deck1,
            deck2,
            waveformStyle,
            new LayoutWizardViewModel(waveformStyle, new WaveformStyleOptionFactory(), new WaveformPreviewRenderer(),
                new DemoWaveformFactory(), new WaveformPreviewScroll(new ManualFrameClock()), appThread,
                themeViewModel, new ThemeOptionFactory()),
            new SettingsViewModel(Bus, Bus, new KnobScaleFactory()),
            new SystemReportViewModel(),
            new CollapseToIconSequence(new FakeFrameClock(), new FixedMotionPreference(false),
                new CollapseToIconOptions("faceplate", new CollapseToIconTimingsFactory().Standard()),
                new AlwaysHintPolicy()),
            new TrackListViewModel(Bus, Bus)).Create();
        Vm.PropertyChanged += (_, e) => Changed.Add(e.PropertyName);

        // The search overlay and overlay factory ask these once the database is attached.
        Bus.Register<SearchCrates, Task<IReadOnlyList<CrateRef>>>(
            new F9QueryHandler<SearchCrates, Task<IReadOnlyList<CrateRef>>>(
                _ => Task.FromResult<IReadOnlyList<CrateRef>>([])));
        Bus.Register<TagsByName, Task<IReadOnlyList<TagHit>>>(
            new F9QueryHandler<TagsByName, Task<IReadOnlyList<TagHit>>>(
                _ => Task.FromResult<IReadOnlyList<TagHit>>([])));
        Bus.Register<TopTags, Task<IReadOnlyList<TagHit>>>(
            new F9QueryHandler<TopTags, Task<IReadOnlyList<TagHit>>>(
                _ => Task.FromResult<IReadOnlyList<TagHit>>([])));
        Bus.Register<SearchTags, Task<IReadOnlyList<TagHit>>>(
            new F9QueryHandler<SearchTags, Task<IReadOnlyList<TagHit>>>(
                _ => Task.FromResult<IReadOnlyList<TagHit>>([])));
        // Opening the Glance overlay asks which deck to aim at and asks for a ranking.
        Bus.Register<SuggestLoadTarget, int>(new F9QueryHandler<SuggestLoadTarget, int>(_ => 0));
        Bus.Register<RankTracks, Task<RankedTracks>>(new F9QueryHandler<RankTracks, Task<RankedTracks>>(
            _ => Task.FromResult(new RankedTracks([], -1, null, null, false, []))));
        Record<SetSearchPick>();
    }

    public F9RecordingCommandHandler<T> Record<T>() where T : struct, ICommand
    {
        var handler = new F9RecordingCommandHandler<T>();
        Bus.Register<T>(handler);
        return handler;
    }

    public void ShowRows(params string[] titles) =>
        Bus.Publish(new LibraryRowsChanged(
            titles.Select(t => new TrackSummary($"/music/{t}.mp3", t, "Artist", TimeSpan.FromMinutes(3))
            {
                TrackId = Guid.NewGuid(),
            }).ToArray(),
            1));

    /// <summary>Put deck <paramref name="deck"/> in the loaded or empty state, the way the App announces it.</summary>
    public void SetLoaded(int deck, bool loaded) =>
        Bus.Publish(new DeckContentChanged(
            deck, null, loaded ? DeckLoadState.Loaded : DeckLoadState.Idle, loaded, null));

    public void SetPlaying(int deck, bool playing) =>
        Bus.Publish(new DeckPlayStateChanged(deck, playing ? PlayPhase.Playing : PlayPhase.Stopped, false));

    public SystemCheckReported MissingTool() =>
        new([new ToolStatus("demucs", ToolCapabilities.Stems, false, null, null)], SystemHealth.Degraded);

    public Sholto.Interface.Faceplate.ViewModels.FaceplateViewModel NewFaceplate() =>
        new(new Sholto.Interface.Faceplate.Model.FaceplateDocLoader().Load(
            new Sholto.Interface.Faceplate.Devices.DdjFlx4.DdjFlx4Faceplate()), Bus);
}
