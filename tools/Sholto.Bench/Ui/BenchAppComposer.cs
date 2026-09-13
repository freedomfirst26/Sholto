using Sholto.Analysis;
using Sholto.App;
using Sholto.App.Theming;
using Sholto.App.ViewModels;
using Sholto.App.Views;
using Sholto.Audio;
using Sholto.Bench.Rendering;
using Sholto.Music;

namespace Sholto.Bench.Ui;

/// <summary>
/// Builds a real <see cref="MainViewModel"/> + <see cref="MainWindow"/> pair the
/// same way <c>App.OnFrameworkInitializationCompleted</c> does, but wired to the
/// same no-op decode/segment/stem fakes as <see cref="BenchDeck"/> instead of
/// real decoders, and with no DB, no audio device, and no MIDI controller behind
/// it. The two decks still need <see cref="Deck.AttachEngine"/> — normally done
/// deep inside <c>AudioEngine</c>'s constructor when it opens a real device —
/// so this calls it directly against a Bench offline engine (same trick as
/// <see cref="BenchDeck.Create"/>): a real port call, not a new seam.
/// </summary>
public static class BenchAppComposer
{
    public static (MainViewModel Vm, MainWindow Window) Create()
    {
        BenchHeadlessApp.EnsureStarted();

        var engine = BenchDeck.CreateEngine();
        var themeContext = new ThemeContext();
        var stems = NoOpStemAnalyzer.Instance; // IStemAnalysisStep + IStemCache, same no-op instance for both roles

        // Real instances, same reasoning as WaveformPeakAnalyzer above and in
        // BenchDeckFactory: these are pure compute (no subprocess/DB), so Bench
        // doesn't need a fake for them, just an object to satisfy the now
        // constructor-injected ports (pass 3v, 2026-09-12).
        var harmonicKeys = new CamelotKeys();
        var vm = new MainViewModel(
            SholtoOptions.Default.Feature,
            NoOpAudioFileDecoder.Instance,
            BenchDeckFactory.Instance,
            themeContext,
            stems,
            new AnalysisReporter(new[] { AnalysisSteps.Beats }),
            new TrackScanner(),
            harmonicKeys,
            new KeyAnalyzer(harmonicKeys),
            new SongSegmentAnalyzer());

        vm.Deck1.Player.AttachEngine(engine, AudioEngine.DeckFormat);
        vm.Deck2.Player.AttachEngine(engine, AudioEngine.DeckFormat);

        var window = new MainWindow { DataContext = vm };
        return (vm, window);
    }
}
