using Microsoft.Extensions.Options;
using Sholto.Analysis;
using Sholto.Audio;
using Sholto.Controller;
using Sholto.Library;
using Sholto.App.Theming;
using Sholto.App.ViewModels;
using Sholto.App.Views;

namespace Sholto.App;

/// <summary>The leaf collaborators <see cref="LiveEntities"/> needs to assemble the
/// real application entity — every one of them built by the composition root
/// (<c>App</c>), never here. Grouped into one record so handing them over is a
/// single readable call rather than a ten-argument one, and so adding a
/// <c>MainViewModel</c> dependency is a change to this record, not to
/// <see cref="ISholtoEntities"/>.</summary>
public sealed record ApplicationLeaves(
    IOptions<FeatureOptions> Features,
    IAudioFileDecoder Decoder,
    IDeckFactory Decks,
    IThemeContext Theme,
    DemucsStemPresence StemCache,
    IAnalysisReporter Reporter,
    ITrackScanner TrackScanner,
    IHarmonicKeys HarmonicKeys,
    IKeyAnalyzer KeyAnalyzer,
    ISongSegmentAnalyzer SongSegments);

/// <summary>The real assembly of the three entities: a real DDJ-FLX4
/// <see cref="Controller"/>, the real <see cref="MainWindow"/> as the keyboard, and
/// a real <see cref="MainViewModel"/> as the app. Constructed by
/// <c>Program.BuildAvaloniaApp</c> and handed to <c>App</c>'s constructor (Avalonia's
/// <c>AppBuilder.Configure&lt;TApp&gt;(Func&lt;TApp&gt;)</c> overload makes that
/// possible), so the choice of "which world does this process run against" is made
/// at the entry point and nowhere else.
///
/// <para><b>Why the Use… seeders exist.</b> This factory is created before any leaf
/// exists — <c>App</c> is the thing that builds the leaves, and <c>App</c> is
/// constructed after this. So the composition root hands each bundle of leaves over
/// when it has them (<see cref="UseApplicationLeaves"/> during framework init,
/// <see cref="UseMidi"/> once the MIDI stack is up after first paint) and the factory
/// decides how leaves become entities. Deliberately NOT on
/// <see cref="ISholtoEntities"/>: that interface stays three factory methods, and a
/// scripted implementation has an entirely different set of leaves.</para>
///
/// <para>Each Create… is idempotent — repeated calls return the SAME entity. That is
/// what makes the three consistent: <see cref="CreateKeyboard"/> returns the window
/// whose DataContext is exactly the object <see cref="CreateApplication"/> returns,
/// so a keyboard shortcut and a controller gesture cannot end up acting on two
/// different view models.</para></summary>
public sealed class LiveEntities : ISholtoEntities
{
    private ApplicationLeaves? _leaves;
    private MidiManager? _midi;
    private MainViewModel? _app;
    private MainWindow? _keyboard;
    private IControlSurface? _surface;

    /// <summary>Composition root → factory: the leaves the real app is built from.
    /// Must be called before <see cref="CreateApplication"/>.</summary>
    public void UseApplicationLeaves(ApplicationLeaves leaves) => _leaves = leaves;

    /// <summary>Composition root → factory: the MIDI stack the real control surface
    /// talks through. Must be called before <see cref="CreateControlSurface"/>.
    /// Separate from <see cref="UseApplicationLeaves"/> only because the MIDI stack
    /// is deliberately built late, after the window has painted its first frame.</summary>
    public void UseMidi(MidiManager midi) => _midi = midi;

    public IApplication CreateApplication() => Application;

    /// <summary>The concrete view model, for the composition root's own startup work
    /// (library scan, theme, debug stats) — none of which is <see cref="Orchestrator"/>'s
    /// business and so none of which belongs on <see cref="IApplication"/>.</summary>
    public MainViewModel Application => _app ??= BuildApplication();

    private MainViewModel BuildApplication()
    {
        var l = _leaves ?? throw new InvalidOperationException(
            $"{nameof(UseApplicationLeaves)} must be called before the application is created.");
        return new MainViewModel(
            l.Features, l.Decoder, l.Decks, l.Theme, l.StemCache, l.Reporter,
            l.TrackScanner, l.HarmonicKeys, l.KeyAnalyzer, l.SongSegments);
    }

    public IKeyboard CreateKeyboard() => Window;

    /// <summary>The same object as <see cref="CreateKeyboard"/>, typed as the window
    /// Avalonia's desktop lifetime needs to be handed. Built with the application
    /// entity as its DataContext — the pairing this factory exists to guarantee.</summary>
    public MainWindow Window => _keyboard ??= new MainWindow { DataContext = Application };

    public IControlSurface CreateControlSurface() =>
        _surface ??= new Sholto.Controller.Controller(
            _midi ?? throw new InvalidOperationException(
                $"{nameof(UseMidi)} must be called before the control surface is created."));
}
