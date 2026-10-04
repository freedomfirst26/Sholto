using Sholto.Data;

namespace Sholto.Interface.MainUI.Tests;

/// <summary>The tag editor as a projection: it asks queries for a track's tags and suggestions, sends
/// commands to tag and untag, and shows the outcome of an add when the App reports it.</summary>
public class TagEditorViewModelTests
{
    [Fact]
    public async Task Tab_commits_input_and_keeps_editor_open()
    {
        var rig = new F9FixTagEditorRig();
        var vm = rig.Vm;
        int closeCount = 0;
        vm.RequestClose += () => closeCount++;
        await vm.OpenForAsync(rig.TrackId, "Artist", "Title");

        vm.Input = "deep house";
        await vm.CommitAsync();

        var add = Assert.Single(rig.Adds.Received);
        Assert.Equal(rig.TrackId, add.TrackId);
        Assert.Equal("deep house", add.Name);
        Assert.Equal("", vm.Input);
        Assert.Empty(vm.Chips);

        rig.Answer(TagAddOutcome.Added, "deep house");

        Assert.Equal(new[] { "deep house" }, vm.Chips);
        Assert.Equal(0, closeCount);
    }

    [Fact]
    public async Task Enter_commits_and_closes()
    {
        var rig = new F9FixTagEditorRig();
        var vm = rig.Vm;
        int closeCount = 0;
        vm.RequestClose += () => closeCount++;
        await vm.OpenForAsync(rig.TrackId, "Artist", "Title");

        vm.Input = "techno";
        await vm.CommitAndCloseAsync();
        rig.Answer(TagAddOutcome.Added, "techno");

        Assert.Equal("techno", Assert.Single(rig.Adds.Received).Name);
        Assert.Equal(new[] { "techno" }, vm.Chips);
        Assert.Equal(1, closeCount);
    }

    [Fact]
    public async Task RemoveLastChip_pops_most_recent()
    {
        var rig = new F9FixTagEditorRig();
        var vm = rig.Vm;
        await vm.OpenForAsync(rig.TrackId, "Artist", "Title");
        vm.Input = "a"; await vm.CommitAsync(); rig.Answer(TagAddOutcome.Added, "a");
        vm.Input = "b"; await vm.CommitAsync(); rig.Answer(TagAddOutcome.Added, "b");

        await vm.RemoveLastChipAsync();

        Assert.Equal(new[] { "a" }, vm.Chips);
        var removed = Assert.Single(rig.Removes.Received);
        Assert.Equal(rig.TrackId, removed.TrackId);
        Assert.Equal("b", removed.Name);
    }

    [Fact]
    public async Task Close_fires_RequestClose_without_committing()
    {
        var rig = new F9FixTagEditorRig();
        var vm = rig.Vm;
        int closeCount = 0;
        vm.RequestClose += () => closeCount++;
        await vm.OpenForAsync(rig.TrackId, "Artist", "Title");

        vm.Input = "uncommitted";
        vm.Close();

        Assert.Equal(1, closeCount);
        Assert.Empty(vm.Chips);
        Assert.Empty(rig.Adds.Received);
    }

    [Fact]
    public async Task Empty_input_commit_is_silent_noop()
    {
        var rig = new F9FixTagEditorRig();
        var vm = rig.Vm;
        await vm.OpenForAsync(rig.TrackId, "Artist", "Title");
        vm.Input = "   ";
        await vm.CommitAsync();
        Assert.Empty(vm.Chips);
        Assert.Null(vm.StatusMessage);
        Assert.Empty(rig.Adds.Received);
    }

    [Fact]
    public async Task TooLong_input_surfaces_status_message()
    {
        var rig = new F9FixTagEditorRig();
        var vm = rig.Vm;
        await vm.OpenForAsync(rig.TrackId, "Artist", "Title");
        vm.Input = new string('x', 200);
        await vm.CommitAsync();

        rig.Answer(TagAddOutcome.RejectedTooLong, null, limit: 50);

        Assert.Equal(200, Assert.Single(rig.Adds.Received).Name.Length);
        Assert.Empty(vm.Chips);
        Assert.NotNull(vm.StatusMessage);
        Assert.Contains("too long", vm.StatusMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AlreadyPresent_input_surfaces_status_message()
    {
        var rig = new F9FixTagEditorRig();
        var vm = rig.Vm;
        await vm.OpenForAsync(rig.TrackId, "Artist", "Title");
        vm.Input = "deep house"; await vm.CommitAsync(); rig.Answer(TagAddOutcome.Added, "deep house");
        vm.Input = "Deep House"; await vm.CommitAsync(); rig.Answer(TagAddOutcome.AlreadyPresent, "deep house");
        Assert.Single(vm.Chips);
        Assert.NotNull(vm.StatusMessage);
        Assert.Contains("already tagged", vm.StatusMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Suggestions_list_the_most_recently_committed_tag_first()
    {
        var rig = new F9FixTagEditorRig();
        // "zulu" sorts last alphabetically, so the database order alone would
        // never put it on top. Committing it makes it the newest selection.
        rig.Queries.Known.AddRange(["alpha", "zulu"]);
        var vm = rig.Vm;
        await vm.OpenForAsync(rig.TrackId, "Artist", "Title");
        vm.Input = "alpha"; await vm.CommitAsync(); rig.Answer(TagAddOutcome.Added, "alpha");
        vm.Input = "zulu";  await vm.CommitAsync(); rig.Answer(TagAddOutcome.Added, "zulu");

        // Untag both, so neither is filtered out of the suggestion list, then
        // re-open the editor the way the overlay does.
        await vm.RemoveChipAsync("zulu");
        await vm.RemoveChipAsync("alpha");
        await vm.OpenForAsync(rig.TrackId, "Artist", "Title");

        Assert.Equal("zulu", vm.Suggestions[0]);
        Assert.Equal("alpha", vm.Suggestions[1]);
    }
}
