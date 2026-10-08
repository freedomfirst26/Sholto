using Microsoft.Extensions.Options;
using Sholto.Data;
using Sholto.Interface.MainUI.Models;
using Sholto.Interface.MainUI.Theming;
using Sholto.Interface.MainUI.ViewModels.Glance;

namespace Sholto.Interface.MainUI.Tests.Glance;

/// <summary>The faint "#drum" completion: which tag wins, when nothing completes, and what Tab-accept does.</summary>
public class GlanceTagCompletionTests
{
    private readonly GlanceRig _rig = new();
    private GlanceViewModel Glance => _rig.Glance;

    public GlanceTagCompletionTests()
    {
        _rig.Tags.Add(new TagHit("Drum and Bass", 9));
        _rig.Tags.Add(new TagHit("Deep House", 4));
        _rig.AttachDatabase();
        Glance.Open();
    }

    [Fact]
    public void A_hash_fragment_completes_to_the_rest_of_the_tag_name()
    {
        Glance.Query = "#drum";

        Assert.Equal(" and Bass", Glance.CompletionSuffix);
    }

    [Fact]
    public void Accepting_replaces_the_token_with_a_tag_chip_and_ranks_by_it()
    {
        Glance.Query = "artemas #drum";

        Assert.True(Glance.AcceptTagCompletion());

        Assert.Equal("artemas", Glance.Query);
        var chip = Assert.Single(Glance.Chips);
        Assert.Equal(GlanceChipKind.Tag, chip.Kind);
        Assert.Equal("Drum and Bass", chip.Name);
        Assert.Equal(["Drum and Bass"], _rig.Ranker.Asked[^1].Tags);
        Assert.Null(Glance.CompletionSuffix);
    }

    [Fact]
    public void A_fragment_that_is_only_inside_a_name_does_not_complete()
    {
        Glance.Query = "#bass";

        Assert.Null(Glance.CompletionSuffix);
        Assert.False(Glance.AcceptTagCompletion());
    }

    [Theory]
    [InlineData("#")]
    [InlineData("#drum ")]
    public void A_lone_hash_or_a_trailing_space_does_not_complete(string query)
    {
        Glance.Query = query;

        Assert.Null(Glance.CompletionSuffix);
        Assert.False(Glance.AcceptTagCompletion());
    }

    [Fact]
    public void The_most_recently_used_matching_tag_wins()
    {
        var recency = new TagRecency();
        _rig.Tags.Add(new TagHit("Drumstep", 1));
        recency.MarkUsed("Drumstep");
        var glance = new GlanceViewModel(
            _rig.Bus, _rig.Bus, _rig.Bus, new ImmediateAppThread(), _rig.Clock, _rig.Library, recency, _rig.Header,
            new FixedMotionPreference(false), Options.Create(new GlanceViewOptions()));
        glance.Open();

        glance.Query = "#drum";

        Assert.Equal("step", glance.CompletionSuffix);
    }

    [Fact]
    public void A_tag_that_is_already_a_chip_is_skipped()
    {
        _rig.Tags.Add(new TagHit("Drumstep", 1));
        Glance.Query = "#drum";
        Glance.AcceptTagCompletion();

        Glance.Query = "#drum";

        Assert.Equal("step", Glance.CompletionSuffix);
    }

    [Fact]
    public void A_shorter_fragment_completes_from_the_same_start()
    {
        Glance.Query = "#dru";

        Assert.Equal("m and Bass", Glance.CompletionSuffix);
    }

    [Fact]
    public void Accepting_after_a_crate_chip_keeps_both_chips()
    {
        _rig.Crates.Add(new CrateRef(7, "Peak time", 12));
        Glance.Close();
        Glance.Open();
        Glance.ToggleZone();
        var crate = Glance.RailItems.ToList().FindIndex(i => i is GlanceRailCrate);
        Glance.Move(crate - Glance.RailIndex);
        Glance.ActivateRailItem();
        Assert.Equal(GlanceChipKind.Crate, Assert.Single(Glance.Chips).Kind);

        Glance.Query = "#drum";
        Assert.True(Glance.AcceptTagCompletion());

        Assert.Equal([GlanceChipKind.Crate, GlanceChipKind.Tag], Glance.Chips.Select(c => c.Kind));
    }
}
