using Sholto.Data;
using Sholto.Interface.MainUI.ViewModels.Glance;

namespace Sholto.Interface.MainUI.Tests.Glance;

/// <summary>The Load button beside the search box: on something not in the Track List it sends what Ctrl L sends; on
/// something already in it, it takes that back out.</summary>
public class GlanceLoadButtonTests
{
    private readonly GlanceRig _rig = new();
    private GlanceViewModel Glance => _rig.Glance;

    private static readonly TrackSummary Alpha = GlanceRig.Track("Alpha");
    private static readonly CrateRef Featurecast = new(7, "Featurecast", 48);
    private static readonly TagHit Industrial = new("Industrial", 22);

    private void OpenOnRail(object wanted)
    {
        _rig.Crates.Add(Featurecast);
        _rig.Tags.Add(Industrial);
        _rig.AttachDatabase();
        Glance.Open();
        Glance.ToggleZone();
        var target = Glance.RailItems.ToList().FindIndex(i => i switch
        {
            GlanceRailCrate c => wanted is CrateRef w && w.Id == c.Id,
            GlanceRailTag t => wanted is TagHit g && g.Name == t.Name,
            _ => false,
        });
        Assert.True(target >= 0);
        while (Glance.RailIndex < target) Glance.Move(1);
    }

    private static TrackListChanged ListOf(string key, TrackListSourceKind kind, string name, int count) =>
        new([new TrackListSource(key, kind, name, count)], count);

    [Fact]
    public void On_a_crate_not_in_the_list_it_sends_what_Ctrl_L_sends()
    {
        OpenOnRail(Featurecast);

        Glance.ToggleTrackListLoad();

        var sent = Assert.Single(_rig.Log.OfType<LoadCrateToTrackList>());
        Assert.Equal(7, sent.CrateId);
        Assert.Empty(_rig.Log.OfType<RemoveSourceFromTrackList>());
        Assert.True(Glance.IsOpen);
    }

    [Fact]
    public void On_a_tag_not_in_the_list_it_sends_the_tag()
    {
        OpenOnRail(Industrial);

        Glance.ToggleTrackListLoad();

        Assert.Equal("Industrial", Assert.Single(_rig.Log.OfType<LoadTagToTrackList>()).Tag);
    }

    [Fact]
    public void On_a_crate_in_the_list_it_takes_that_crate_back_out()
    {
        OpenOnRail(Featurecast);
        _rig.Bus.Publish(ListOf("crate:7", TrackListSourceKind.Crate, "Featurecast", 48));

        Glance.ToggleTrackListLoad();

        Assert.Equal("crate:7", Assert.Single(_rig.Log.OfType<RemoveSourceFromTrackList>()).SourceKey);
        Assert.Empty(_rig.Log.OfType<LoadCrateToTrackList>());
    }

    [Fact]
    public void On_a_tag_in_the_list_it_takes_the_tag_out_by_its_lowercased_key()
    {
        OpenOnRail(Industrial);
        _rig.Bus.Publish(ListOf("tag:industrial", TrackListSourceKind.Tag, "Industrial", 22));

        Glance.ToggleTrackListLoad();

        Assert.Equal("tag:industrial", Assert.Single(_rig.Log.OfType<RemoveSourceFromTrackList>()).SourceKey);
    }

    [Fact]
    public void On_a_song_it_adds_then_removes()
    {
        _rig.Show(Alpha);
        Glance.Open();

        Glance.ToggleTrackListLoad();
        Assert.Equal(Alpha.FilePath, Assert.Single(_rig.Log.OfType<LoadSongToTrackList>()).Path);

        _rig.PutInList(Alpha);
        Glance.ToggleTrackListLoad();
        Assert.Equal(Alpha.FilePath, Assert.Single(_rig.Log.OfType<RemoveFromTrackList>()).Path);
    }

    [Fact]
    public void It_sends_nothing_with_nothing_highlighted_or_the_overlay_closed()
    {
        Glance.ToggleTrackListLoad();
        Glance.Open();
        Assert.Equal(GlanceLoadState.Off, Glance.TrackListLoadState);

        Glance.ToggleTrackListLoad();

        Assert.Empty(_rig.Log.OfType<LoadSongToTrackList>());
        Assert.Empty(_rig.Log.OfType<LoadCrateToTrackList>());
        Assert.Empty(_rig.Log.OfType<LoadTagToTrackList>());
        Assert.Empty(_rig.Log.OfType<RemoveFromTrackList>());
        Assert.Empty(_rig.Log.OfType<RemoveSourceFromTrackList>());
    }
}
