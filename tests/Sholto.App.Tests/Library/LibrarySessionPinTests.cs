using Sholto.App.Analysis.Analyzers.Keys;
using Sholto.App.Analysis.Harmony;
using Sholto.App.Analysis.Stores;
using Sholto.Data;
using Sholto.App.Library;

namespace Sholto.App.Tests;

/// <summary>Pins the order in which the library session announces changes, and how a failing multiplier
/// save is handled, so a restructuring of the session cannot reorder or lose them.</summary>
[Collection(ConsoleCaptureCollection.Name)]
public class LibrarySessionPinTests
{
    private static readonly string[] SelectionThenRows =
        ["SelectedIndexChanged", "bus:SelectionChanged", "RowsChanged", "bus:LibraryRowsChanged"];

    private static readonly Key Key8B = new(PitchClass: 0, IsMajor: true);

    private sealed class LogHandler<T>(List<string> log, string name) : IEventHandler<T> where T : struct, IEvent
    {
        private readonly List<string> _log = log;
        private readonly string _name = name;

        public void Handle(in T e) => _log.Add(_name);
    }

    private sealed class ThrowingTempoMultiplierStore : ITempoMultiplierStore
    {
        public Task<IReadOnlyDictionary<string, double>> GetAllAsync() =>
            Task.FromResult<IReadOnlyDictionary<string, double>>(new Dictionary<string, double>());

        public Task PutAsync(string path, double multiplier) => throw new IOException("disk full");
    }

    private static async Task<(LibrarySessionRig Rig, List<string> Log)> SelectionRigAsync(int selected)
    {
        var rig = new LibrarySessionRig();
        await rig.Library.ScanAsync("/music", rig.Stack());
        rig.Library.Select(selected);
        var log = new List<string>();
        rig.Library.SelectedIndexChanged += () => log.Add("SelectedIndexChanged");
        rig.Library.RowsChanged += _ => log.Add("RowsChanged");
        rig.Bus.Subscribe(new LogHandler<SelectionChanged>(log, "bus:SelectionChanged"));
        rig.Bus.Subscribe(new LogHandler<LibraryRowsChanged>(log, "bus:LibraryRowsChanged"));
        log.Clear();   // state events replay their last value on subscribe
        return (rig, log);
    }

    private static List<string> HarmonyLog(LibrarySessionRig rig)
    {
        var log = new List<string>();
        rig.Library.RowUpdated += _ => log.Add("RowUpdated");
        rig.Library.HarmonyReferenceChanged += _ => log.Add("HarmonyReferenceChanged");
        return log;
    }

    [Fact]
    public async Task Hiding_the_highlighted_song_keeps_the_row_number_and_announces_selection_before_rows()
    {
        var (rig, log) = await SelectionRigAsync(selected: 0);   // Bravo

        rig.Library.ShowTrackList([LibrarySessionRig.Charlie.FilePath, LibrarySessionRig.Alpha.FilePath]);

        Assert.Equal(0, rig.Library.SelectedIndex);
        Assert.Equal(SelectionThenRows, log);
    }

    [Fact]
    public async Task Moving_the_highlighted_song_announces_selection_before_rows()
    {
        var (rig, log) = await SelectionRigAsync(selected: 1);   // Charlie

        rig.Library.ShowTrackList([LibrarySessionRig.Charlie.FilePath, LibrarySessionRig.Alpha.FilePath]);

        Assert.Equal(0, rig.Library.SelectedIndex);
        Assert.Equal(SelectionThenRows, log);
    }

    [Fact]
    public async Task Seeding_a_key_updates_the_row_before_the_harmony_reference()
    {
        var rig = new LibrarySessionRig();
        await rig.Library.ScanAsync("/music", null);
        rig.Deck1.Analysis.Set(new KeyAnalysis(Key8B));
        var log = HarmonyLog(rig);

        rig.Library.SeedKnownKeys(new Dictionary<string, Key> { [LibrarySessionRig.Alpha.FilePath] = Key8B });

        Assert.Equal(["RowUpdated", "HarmonyReferenceChanged"], log);
    }

    [Fact]
    public async Task A_reanalysis_updates_the_row_before_the_harmony_reference()
    {
        var rig = new LibrarySessionRig();
        await rig.Library.ScanAsync("/music", null);
        rig.Deck1.Analysis.Set(new KeyAnalysis(Key8B));
        var log = HarmonyLog(rig);

        rig.Library.ApplyReanalysis(LibrarySessionRig.Alpha.FilePath, 120, Key8B);

        Assert.Equal(["RowUpdated", "HarmonyReferenceChanged"], log);
    }

    [Fact]
    public async Task Deck_analysis_updates_the_row_before_the_harmony_reference()
    {
        var rig = new LibrarySessionRig();
        await rig.Library.ScanAsync("/music", null);
        rig.Deck1.LoadTrack(LibrarySessionRig.Alpha, LibrarySessionRig.Alpha.FilePath, [], bpmMultiplier: 1.0);
        rig.Deck1.Analysis.Set(new KeyAnalysis(Key8B));
        var log = HarmonyLog(rig);

        ((ScriptedLoading)rig.Deck1.Loading).RaiseAnalysisUpdated();

        Assert.Equal(["RowUpdated", "HarmonyReferenceChanged"], log);
    }

    [Fact]
    public async Task Deck_analysis_of_a_song_not_in_the_catalog_still_refreshes_the_harmony_reference()
    {
        var rig = new LibrarySessionRig();
        await rig.Library.ScanAsync("/music", null);
        var stranger = new Track("/elsewhere/x.mp3", "Xray", "Yank", TimeSpan.FromMinutes(2));
        rig.Deck1.LoadTrack(stranger, stranger.FilePath, [], bpmMultiplier: 1.0);
        rig.Deck1.Analysis.Set(new KeyAnalysis(Key8B));
        var log = HarmonyLog(rig);

        ((ScriptedLoading)rig.Deck1.Loading).RaiseAnalysisUpdated();

        Assert.Equal(["HarmonyReferenceChanged"], log);
    }

    [Fact]
    public async Task A_failing_multiplier_save_is_swallowed_logged_and_the_row_still_shows_it()
    {
        var rig = new LibrarySessionRig();
        await rig.Library.ScanAsync("/music", rig.Stack());
        rig.Library.AttachMultiplierStore(new ThrowingTempoMultiplierStore());
        rig.Deck1.LoadTrack(LibrarySessionRig.Alpha, LibrarySessionRig.Alpha.FilePath, [], bpmMultiplier: 1.0);
        var original = Console.Out;
        var captured = new StringWriter();
        Console.SetOut(captured);
        try
        {
            rig.Deck1.HalveBpm();
        }
        finally
        {
            Console.SetOut(original);
        }

        Assert.Equal(0.5, rig.Row(LibrarySessionRig.Alpha).BpmMultiplier);
        var lines = captured.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Where(l => l.Contains("[DB] save bpm override failed")).ToList();
        Assert.Single(lines);
    }
}
