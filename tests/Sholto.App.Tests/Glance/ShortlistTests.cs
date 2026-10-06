using Sholto.App.Library;
using Sholto.App.Glance;
using Sholto.Data;

namespace Sholto.App.Tests.Glance;

/// <summary>The shortlist: toggling, what is announced, and what survives a track that is not in the catalog.</summary>
public class ShortlistTests
{
    private static readonly Origin From = new(InterfaceIds.Bench, "test", "shortlist");
    private static readonly string AlphaPath = LibrarySessionRig.Alpha.FilePath;
    private static readonly string BravoPath = LibrarySessionRig.Bravo.FilePath;
    private const string Gone = "/unmounted/gone.mp3";

    private readonly LibrarySessionRig _rig = ScannedRig();
    private readonly RecordingHandler<ShortlistChanged> _announced = new();

    private static LibrarySessionRig ScannedRig()
    {
        var rig = new LibrarySessionRig();
        Task.Run(() => rig.Library.ScanAsync("/music", null)).GetAwaiter().GetResult();
        return rig;
    }

    private Shortlist Create()
    {
        _rig.Bus.Subscribe(_announced);
        return new Shortlist(_rig.Library, _rig.Bus);
    }

    private string[] LastTitles() => _announced.Received[^1].Tracks.Select(t => t.Title).ToArray();

    [Fact]
    public void It_starts_empty_and_announces_so()
    {
        Create();

        Assert.Empty(Assert.Single(_announced.Received).Tracks);
    }

    [Fact]
    public void Toggling_adds_then_removes_and_each_change_is_announced()
    {
        var shortlist = Create();

        shortlist.Handle(new ToggleShortlist(AlphaPath, From));
        shortlist.Handle(new ToggleShortlist(BravoPath, From));
        Assert.Equal(new[] { "Alpha", "Bravo" }, LastTitles());
        Assert.Equal(new[] { AlphaPath, BravoPath }, shortlist.Paths);

        shortlist.Handle(new ToggleShortlist(AlphaPath, From));
        Assert.Equal(new[] { "Bravo" }, LastTitles());
        Assert.Equal(new[] { BravoPath }, shortlist.Paths);
    }

    [Fact]
    public void A_toggle_raises_Changed_so_the_list_can_be_saved()
    {
        var shortlist = Create();
        var raised = 0;
        shortlist.Changed += () => raised++;

        shortlist.Handle(new ToggleShortlist(AlphaPath, From));
        shortlist.Handle(new ToggleShortlist(AlphaPath, From));

        Assert.Equal(2, raised);
    }

    [Fact]
    public void A_path_missing_from_the_catalog_is_not_announced_but_is_kept_and_saved_with_the_next_change()
    {
        var shortlist = Create();
        var raised = 0;
        shortlist.Changed += () => raised++;

        shortlist.Restore([AlphaPath, Gone]);

        Assert.Equal(new[] { "Alpha" }, LastTitles());
        Assert.Equal(new[] { AlphaPath, Gone }, shortlist.Paths);
        Assert.Equal(0, raised);

        shortlist.Handle(new ToggleShortlist(BravoPath, From));

        Assert.Equal(new[] { "Alpha", "Bravo" }, LastTitles());
        Assert.Equal(new[] { AlphaPath, Gone, BravoPath }, shortlist.Paths);
        Assert.Equal(1, raised);
    }

    [Fact]
    public void A_restore_puts_the_saved_paths_before_anything_toggled_in_meanwhile()
    {
        var shortlist = Create();
        shortlist.Handle(new ToggleShortlist(BravoPath, From));

        shortlist.Restore([AlphaPath, BravoPath, Gone]);

        Assert.Equal(new[] { AlphaPath, BravoPath, Gone }, shortlist.Paths);
    }

    [Fact]
    public void A_listed_track_whose_summary_changes_is_announced_again()
    {
        var shortlist = Create();
        shortlist.Handle(new ToggleShortlist(AlphaPath, From));
        Assert.Null(_announced.Received[^1].Tracks[0].Bpm);

        _rig.Library.ApplyReanalysis(AlphaPath, 123.0, null);

        Assert.Equal(123.0, _announced.Received[^1].Tracks[0].Bpm);
    }

    [Fact]
    public void A_scan_brings_back_a_track_that_was_not_in_the_catalog_when_the_list_was_restored()
    {
        var late = new Track("/music/late.mp3", "Late", "Q", TimeSpan.FromMinutes(1));
        var rig = new LibrarySessionRig(late);
        var seen = new RecordingHandler<ShortlistChanged>();
        rig.Bus.Subscribe(seen);
        var shortlist = new Shortlist(rig.Library, rig.Bus);

        shortlist.Restore([late.FilePath]);
        Assert.Empty(seen.Received[^1].Tracks);
        Task.Run(() => rig.Library.ScanAsync("/music", null)).GetAwaiter().GetResult();

        Assert.Equal(new[] { "Late" }, seen.Received[^1].Tracks.Select(t => t.Title));
    }
}
