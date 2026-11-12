using Avalonia.Controls;
using Path = Avalonia.Controls.Shapes.Path;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Sholto.Data;
using Sholto.Interface.MainUI.ViewModels.Glance;

namespace Sholto.Interface.MainUI.Tests.Glance;

/// <summary>The Load button beside the search box in the real overlay: a click adds the highlighted crate, tag or song
/// to the Track List as Ctrl L does, the button then shows the tick, and it is disabled with nothing highlighted.</summary>
[Collection(AvaloniaXamlCollection.Name)]
public class SearchOverlayLoadButtonTests
{
    public SearchOverlayLoadButtonTests() => AvaloniaTestApp.EnsureStarted();

    private static readonly CrateRef Featurecast = new(7, "Featurecast", 48);
    private static readonly TagHit Industrial = new("Industrial", 22);

    private static SearchOverlayRig Open(Action<GlanceRig> before)
    {
        var rig = new SearchOverlayRig();
        before(rig.Rig);
        rig.Rig.Glance.Open();
        Settle(rig);
        return rig;
    }

    private static void Settle(SearchOverlayRig rig)
    {
        Dispatcher.UIThread.RunJobs();
        rig.Overlay.UpdateLayout();
    }

    /// <summary>Open on the rail with <paramref name="wanted"/> highlighted.</summary>
    private static SearchOverlayRig OpenOnRail(object wanted)
    {
        var rig = Open(g =>
        {
            g.Show(GlanceRig.Track("Alpha"));
            g.Crates.Add(Featurecast);
            g.Tags.Add(Industrial);
            g.AttachDatabase();
        });
        var glance = rig.Rig.Glance;
        glance.ToggleZone();
        var target = glance.RailItems.ToList().FindIndex(i => i switch
        {
            GlanceRailCrate c => wanted is CrateRef w && w.Id == c.Id,
            GlanceRailTag t => wanted is TagHit h && h.Name == t.Name,
            _ => false,
        });
        Assert.True(target >= 0);
        while (glance.RailIndex < target) glance.Move(1);
        Settle(rig);
        return rig;
    }

    private static Button LoadButton(SearchOverlayRig rig) => rig.Overlay.FindControl<Button>("LoadButton")!;

    private static void Click(SearchOverlayRig rig)
    {
        LoadButton(rig).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Settle(rig);
    }

    private static bool ShowsTick(SearchOverlayRig rig) =>
        rig.Overlay.FindControl<Path>("LoadButtonTickGlyph")!.IsVisible
        && !rig.Overlay.FindControl<Path>("LoadButtonPlusGlyph")!.IsVisible;

    [Fact]
    public void Clicking_it_on_a_crate_adds_the_crate_like_Ctrl_L_and_then_shows_the_tick()
    {
        var rig = OpenOnRail(Featurecast);
        Assert.True(LoadButton(rig).IsEnabled);
        Assert.False(ShowsTick(rig));

        Click(rig);

        var sent = Assert.Single(rig.Rig.Log.OfType<LoadCrateToTrackList>());
        Assert.Equal(7, sent.CrateId);
        Assert.True(rig.Rig.Glance.IsOpen);

        rig.Rig.Bus.Publish(new TrackListChanged([new TrackListSource("crate:7", TrackListSourceKind.Crate, "Featurecast", 48)], 48));
        Settle(rig);

        Assert.True(ShowsTick(rig));
        Assert.Contains("inlist", LoadButton(rig).Classes);
        Assert.True(LoadButton(rig).IsEnabled);
    }

    [Fact]
    public void Clicking_it_on_a_tag_adds_the_tag()
    {
        var rig = OpenOnRail(Industrial);

        Click(rig);

        Assert.Equal("Industrial", Assert.Single(rig.Rig.Log.OfType<LoadTagToTrackList>()).Tag);
    }

    [Fact]
    public void Clicking_the_tick_takes_the_crate_back_out()
    {
        var rig = OpenOnRail(Featurecast);
        rig.Rig.Bus.Publish(new TrackListChanged([new TrackListSource("crate:7", TrackListSourceKind.Crate, "Featurecast", 48)], 48));
        Settle(rig);
        Assert.True(ShowsTick(rig));

        Click(rig);

        Assert.Equal("crate:7", Assert.Single(rig.Rig.Log.OfType<RemoveSourceFromTrackList>()).SourceKey);
        Assert.Empty(rig.Rig.Log.OfType<LoadCrateToTrackList>());
    }

    [Fact]
    public void Clicking_it_on_a_song_adds_the_song()
    {
        var rig = Open(g => g.Show(GlanceRig.Track("Alpha")));

        Click(rig);

        Assert.Single(rig.Rig.Log.OfType<LoadSongToTrackList>());
    }

    [Fact]
    public void It_is_disabled_with_nothing_highlighted()
    {
        var rig = Open(_ => { });

        Assert.Equal(GlanceLoadState.Off, rig.Rig.Glance.TrackListLoadState);
        Assert.False(LoadButton(rig).IsEnabled);
        Assert.True(LoadButton(rig).IsVisible);
    }
}
