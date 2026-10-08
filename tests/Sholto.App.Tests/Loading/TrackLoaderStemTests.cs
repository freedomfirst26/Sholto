using Sholto.App.Analysis.Harmony;
using Sholto.App.Analysis.Processing;
using Sholto.App.Analysis.Reporting;
using Sholto.App.Analysis.Stems;
using Sholto.App.Library;
using Sholto.Data;

namespace Sholto.App.Tests;

/// <summary>Re-analysing a track also separates its stems, so the row ends up ticked (tempo, beats AND stems).</summary>
public class TrackLoaderStemTests
{
    private static readonly Origin From = TrackLoaderRig.Origin;
    private static readonly string Bravo = LibrarySessionRig.Bravo.FilePath;

    private static async Task Eventually(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException("The condition never became true.");
            await Task.Delay(10);
        }
    }

    private static TrackLoaderRig Rig(
        IStemAnalysisStep step, IStemPresence presence, IStemGate gate)
    {
        // The separator reports on the reporter the library listens to, as the app wires it.
        return new TrackLoaderRig(
            new FakeAudioFileDecoder(), new FakeAnalysisProvider(bpm: 126.5), new Key(PitchClass: 7, IsMajor: true),
            reporter => new GatedStemSeparator(new CachingStemAnalysisStep(step, presence), gate, reporter));
    }

    [Fact]
    public async Task Reanalysis_of_a_track_without_stems_runs_the_stem_step_once_and_ticks_the_row()
    {
        var step = new FakeStemAnalysisStep();
        var rig = Rig(step, new FakeStemPresence(), new SemaphoreStemGate());
        rig.Highlight(LibrarySessionRig.Bravo);

        rig.Loader.Handle(new ReanalyzeSelected(From));

        await Eventually(() => rig.Library.Row(LibrarySessionRig.Bravo).StemsReady);
        Assert.Equal(Bravo, Assert.Single(step.Runs));
        var row = rig.Library.Row(LibrarySessionRig.Bravo);
        Assert.Equal(126.5, row.Bpm);
        Assert.NotNull(row.MusicalKey);
    }

    [Fact]
    public async Task Reanalysis_of_a_track_with_cached_stems_does_not_run_demucs_again_but_still_ticks_it()
    {
        var step = new FakeStemAnalysisStep();
        var rig = Rig(step, new FakeStemPresence(Bravo), new SemaphoreStemGate());
        rig.Highlight(LibrarySessionRig.Bravo);

        rig.Loader.Handle(new ReanalyzeSelected(From));

        await Eventually(() => rig.Library.Row(LibrarySessionRig.Bravo).StemsReady);
        Assert.Empty(step.Runs);
        Assert.Equal(126.5, rig.Library.Row(LibrarySessionRig.Bravo).Bpm);
    }

    [Fact]
    public async Task A_reanalysis_stem_run_waits_for_a_deck_load_stem_run_in_progress()
    {
        var step = new FakeStemAnalysisStep();
        var gate = new SemaphoreStemGate();
        var rig = Rig(step, new FakeStemPresence(), gate);
        rig.Highlight(LibrarySessionRig.Bravo);
        // A deck load of another track holds the demucs slot.
        var deckRun = await gate.EnterAsync();

        rig.Loader.Handle(new ReanalyzeSelected(From));

        await Eventually(() => rig.Library.Row(LibrarySessionRig.Bravo).Bpm == 126.5);
        await Task.Delay(200);
        Assert.Empty(step.Runs);
        Assert.False(rig.Library.Row(LibrarySessionRig.Bravo).StemsReady);

        deckRun.Dispose();

        await Eventually(() => rig.Library.Row(LibrarySessionRig.Bravo).StemsReady);
        Assert.Single(step.Runs);
    }

    [Fact]
    public async Task A_stem_failure_during_reanalysis_shows_on_the_row_and_leaves_the_bpm_and_key_applied()
    {
        var step = new FakeStemAnalysisStep(failure: new InvalidOperationException("demucs exploded"));
        var rig = Rig(step, new FakeStemPresence(), new SemaphoreStemGate());
        rig.Highlight(LibrarySessionRig.Bravo);

        rig.Loader.Handle(new ReanalyzeSelected(From));

        await Eventually(() => rig.Library.Row(LibrarySessionRig.Bravo).AnalysisFailure is not null);
        var row = rig.Library.Row(LibrarySessionRig.Bravo);
        Assert.Contains("demucs exploded", row.AnalysisFailure);
        Assert.Equal(126.5, row.Bpm);
        Assert.NotNull(row.MusicalKey);
        Assert.False(row.StemsReady);
    }

    [Fact]
    public async Task A_superseded_reanalysis_stem_run_is_cancelled_and_reported_cancelled()
    {
        var step = new FakeStemAnalysisStep(gated: true);
        var gate = new SemaphoreStemGate();
        var rig = Rig(step, new FakeStemPresence(), gate);
        rig.Highlight(LibrarySessionRig.Bravo);
        var deckRun = await gate.EnterAsync();

        rig.Loader.Handle(new ReanalyzeSelected(From));
        await Eventually(() => rig.Library.Row(LibrarySessionRig.Bravo).Bpm == 126.5);
        rig.Loader.Handle(new ReanalyzeSelected(From));
        deckRun.Dispose();
        step.Release();

        await Eventually(() => rig.Library.Row(LibrarySessionRig.Bravo).StemsReady);
        // One separation survived; the superseded one never reached demucs and left no failure behind.
        Assert.Single(step.Runs);
        Assert.Null(rig.Library.Row(LibrarySessionRig.Bravo).AnalysisFailure);
    }
}
