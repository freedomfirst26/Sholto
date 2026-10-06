using Sholto.App.Library;
using Sholto.App.Glance;
using Sholto.Data;

namespace Sholto.App.Tests.Glance;

/// <summary>Recent loads: per-run, newest first, deduplicated and capped at six.</summary>
public class RecentLoadsTests
{
    private static readonly Track[] Seven = Enumerable.Range(1, 7)
        .Select(i => new Track($"/music/t{i}.mp3", $"T{i}", "A", TimeSpan.FromMinutes(3)))
        .ToArray();

    private readonly LibrarySessionRig _rig = ScannedRig();
    private readonly FakeTrackLoader _loader = new();
    private readonly RecordingHandler<RecentLoadsChanged> _announced = new();

    private static LibrarySessionRig ScannedRig()
    {
        var rig = new LibrarySessionRig(Seven);
        Task.Run(() => rig.Library.ScanAsync("/music", null)).GetAwaiter().GetResult();
        return rig;
    }

    private RecentLoads Create()
    {
        _rig.Bus.Subscribe(_announced);
        return new RecentLoads(_loader, _rig.Library, _rig.Bus);
    }

    private string[] LastTitles() => _announced.Received[^1].Tracks.Select(t => t.Title).ToArray();

    [Fact]
    public void A_new_run_starts_with_no_recent_loads()
    {
        var recent = Create();

        Assert.Empty(Assert.Single(_announced.Received).Tracks);
        Assert.Empty(recent.Paths);
    }

    [Fact]
    public void Seven_loads_keep_the_six_newest_newest_first()
    {
        var recent = Create();

        foreach (var track in Seven) _loader.Raise(0, track);

        Assert.Equal(new[] { "T7", "T6", "T5", "T4", "T3", "T2" }, LastTitles());
        Assert.Equal(6, recent.Paths.Count);
        Assert.Equal(6, recent.Capacity);
    }

    [Fact]
    public void Reloading_a_track_moves_it_to_the_front_without_duplicating_it()
    {
        Create();
        foreach (var track in Seven.Take(4)) _loader.Raise(0, track);

        _loader.Raise(1, Seven[1]);

        Assert.Equal(new[] { "T2", "T4", "T3", "T1" }, LastTitles());
    }

    [Fact]
    public void A_recent_track_whose_summary_changes_is_announced_again()
    {
        Create();
        _loader.Raise(0, Seven[0]);
        Assert.False(_announced.Received[^1].Tracks[0].IsPlayed);

        _rig.Library.ApplyReanalysis(Seven[0].FilePath, 120.0, null);

        Assert.Equal(120.0, _announced.Received[^1].Tracks[0].Bpm);
    }

    [Fact]
    public void A_load_the_real_loader_accepts_is_recorded()
    {
        var rig = new TrackLoaderRig(new FakeAudioFileDecoder());
        var announced = new RecordingHandler<RecentLoadsChanged>();
        rig.Library.Bus.Subscribe(announced);
        var recent = new RecentLoads(rig.Loader, rig.Library.Library, rig.Library.Bus);

        rig.Highlight(LibrarySessionRig.Bravo);
        rig.Loader.Handle(new LoadSelectedIntoDeck(0, TrackLoaderRig.Origin));

        Assert.Equal(new[] { LibrarySessionRig.Bravo.FilePath }, recent.Paths);
        Assert.Equal(new[] { "Bravo" }, announced.Received[^1].Tracks.Select(t => t.Title));
    }
}
