using Sholto.App.Glance;
using Sholto.App.Library.Tags;
using Sholto.Data;
using Sholto.App.Library;

namespace Sholto.App.Tests;

/// <summary>The library commands against a real library session over fake tag and crate services: selection
/// (clamped or as given), the tag and crate filters, and the tag and crate edits with the facts they publish
/// on the bus. Every fake answers with a completed task and the app thread is immediate, so each handler's
/// work is done by the time it returns.</summary>
public class LibraryCommandHandlersTests
{
    private readonly Origin _from = new(InterfaceIds.Bench, "test", "library");
    private readonly LibrarySessionRig _rig = new();
    private readonly F9TagService _tags = new();
    private readonly LibraryCommandHandlers _handlers;
    private readonly Guid _bravoId;
    private readonly Guid _charlieId;
    private readonly Guid _alphaId;

    public LibraryCommandHandlersTests()
    {
        _handlers = new LibraryCommandHandlers(_rig.Library, new ImmediateAppThread(), _rig.Bus, new CrateMembershipCache(_rig.Library));
        _alphaId = _rig.Catalog.Assign(LibrarySessionRig.Alpha.FilePath);
        _bravoId = _rig.Catalog.Assign(LibrarySessionRig.Bravo.FilePath);
        _charlieId = _rig.Catalog.Assign(LibrarySessionRig.Charlie.FilePath);
    }

    /// <summary>Scan the three tracks (Bravo, Charlie, Alpha in display order) and attach the services.</summary>
    private async Task ScanAndAttachAsync()
    {
        await _rig.Library.ScanAsync("/music", _rig.Stack());
        _rig.Library.AttachServices(_tags, _rig.Crates);
    }

    private IEnumerable<string> VisiblePaths => _rig.Library.Rows.Select(r => r.FilePath);

    // ---- SelectTrack ----------------------------------------------------------------------------

    [Fact]
    public async Task SelectTrack_with_clamp_clamps_a_too_high_index_to_the_last_row()
    {
        await _rig.Library.ScanAsync("/music", null);

        _handlers.Handle(new SelectTrack(99, true, _from));

        Assert.Equal(2, _rig.Library.SelectedIndex);
    }

    [Fact]
    public async Task SelectTrack_with_clamp_clamps_a_negative_index_to_the_first_row()
    {
        await _rig.Library.ScanAsync("/music", null);

        _handlers.Handle(new SelectTrack(-5, true, _from));

        Assert.Equal(0, _rig.Library.SelectedIndex);
    }

    [Fact]
    public void SelectTrack_with_clamp_and_no_rows_selects_nothing()
    {
        _handlers.Handle(new SelectTrack(3, true, _from));

        Assert.Equal(-1, _rig.Library.SelectedIndex);
    }

    [Fact]
    public async Task SelectTrack_without_clamp_takes_the_index_as_given_and_allows_minus_one()
    {
        await _rig.Library.ScanAsync("/music", null);

        _handlers.Handle(new SelectTrack(1, false, _from));
        Assert.Equal(1, _rig.Library.SelectedIndex);
        Assert.Equal(LibrarySessionRig.Charlie.FilePath, _rig.Library.SelectedTrack!.FilePath);

        _handlers.Handle(new SelectTrack(-1, false, _from));
        Assert.Equal(-1, _rig.Library.SelectedIndex);
        Assert.Null(_rig.Library.SelectedTrack);
    }

    [Fact]
    public async Task SelectTrack_announces_the_new_selection_on_the_bus()
    {
        await _rig.Library.ScanAsync("/music", null);
        var changes = new RecordingHandler<SelectionChanged>();
        using var sub = _rig.Bus.Subscribe(changes);
        changes.Received.Clear();

        _handlers.Handle(new SelectTrack(2, false, _from));

        Assert.Equal(2, Assert.Single(changes.Received).Index);
    }

    // ---- Filters --------------------------------------------------------------------------------

    [Fact]
    public async Task FilterLibraryByTag_shows_only_the_tracks_with_that_tag()
    {
        _tags.TrackIdsByTag["peak"] = [_bravoId, _charlieId];
        await ScanAndAttachAsync();

        _handlers.Handle(new FilterLibraryByTag("peak", _from));

        Assert.Equal([LibrarySessionRig.Bravo.FilePath, LibrarySessionRig.Charlie.FilePath], VisiblePaths);
        Assert.Equal("peak", _rig.Library.ActiveFilter);
    }

    [Fact]
    public async Task FilterLibraryByTag_publishes_the_filter_label_and_the_visible_rows()
    {
        _tags.TrackIdsByTag["peak"] = [_alphaId];
        await ScanAndAttachAsync();
        var labels = new RecordingHandler<LibraryFilterChanged>();
        var rows = new RecordingHandler<LibraryRowsChanged>();
        using var labelSub = _rig.Bus.Subscribe(labels);
        using var rowsSub = _rig.Bus.Subscribe(rows);

        _handlers.Handle(new FilterLibraryByTag("peak", _from));

        Assert.Equal("peak", labels.Received[^1].Label);
        Assert.Equal([LibrarySessionRig.Alpha.FilePath], rows.Received[^1].Rows.Select(r => r.FilePath));
    }

    [Fact]
    public async Task FilterLibraryByCrate_shows_the_crates_tracks_under_a_labelled_chip()
    {
        await ScanAndAttachAsync();
        var crateId = await _rig.Crates.CreateAsync("Warmup");
        await _rig.Crates.AddTrackAsync(crateId, _alphaId);

        _handlers.Handle(new FilterLibraryByCrate(crateId, "Warmup", _from));

        Assert.Equal([LibrarySessionRig.Alpha.FilePath], VisiblePaths);
        Assert.Equal("📦 Warmup", _rig.Library.ActiveFilter);
    }

    [Fact]
    public async Task ClearLibraryFilter_brings_every_track_back()
    {
        _tags.TrackIdsByTag["peak"] = [_bravoId];
        await ScanAndAttachAsync();
        _handlers.Handle(new FilterLibraryByTag("peak", _from));
        Assert.Single(_rig.Library.Rows);

        _handlers.Handle(new ClearLibraryFilter(_from));

        Assert.Equal(3, _rig.Library.Rows.Count);
        Assert.Null(_rig.Library.ActiveFilter);
    }

    [Fact]
    public async Task A_filter_before_the_services_attach_does_nothing()
    {
        await _rig.Library.ScanAsync("/music", _rig.Stack());

        _handlers.Handle(new FilterLibraryByTag("peak", _from));
        _handlers.Handle(new FilterLibraryByCrate(1, "Warmup", _from));

        Assert.Equal(3, _rig.Library.Rows.Count);
        Assert.Null(_rig.Library.ActiveFilter);
    }

    // ---- AddTagToTrack --------------------------------------------------------------------------

    private async Task<TagAddAttempted> AddTagAsync(Guid trackId, string name)
    {
        var attempt = new F9AwaitableEventHandler<TagAddAttempted>();
        using var sub = _rig.Bus.Subscribe(attempt);

        _handlers.Handle(new AddTagToTrack(trackId, name, _from));

        return await attempt.NextAsync();
    }

    [Fact]
    public async Task Adding_a_tag_asks_the_service_and_publishes_Added_with_the_stored_name()
    {
        await ScanAndAttachAsync();
        _tags.AddAnswer = _ => new AddTagResult(AddTagOutcome.Added, "Deep House");

        var attempt = await AddTagAsync(_alphaId, "  deep   house ");

        Assert.Equal([(_alphaId, "  deep   house ")], _tags.Added);
        Assert.Equal(new TagAddAttempted(_alphaId, TagAddOutcome.Added, "Deep House", 0), attempt);
    }

    [Fact]
    public async Task An_already_present_tag_is_reported_as_such()
    {
        await ScanAndAttachAsync();
        _tags.AddAnswer = _ => new AddTagResult(AddTagOutcome.AlreadyPresent, "peak");

        var attempt = await AddTagAsync(_alphaId, "peak");

        Assert.Equal(new TagAddAttempted(_alphaId, TagAddOutcome.AlreadyPresent, "peak", 0), attempt);
    }

    [Fact]
    public async Task A_too_long_tag_carries_the_name_length_limit()
    {
        await ScanAndAttachAsync();
        _tags.AddAnswer = _ => new AddTagResult(AddTagOutcome.RejectedTooLong, null);

        var attempt = await AddTagAsync(_alphaId, new string('x', 101));

        Assert.Equal(TagAddOutcome.RejectedTooLong, attempt.Outcome);
        Assert.Equal(TagNameNormalizer.MaxLength, attempt.Limit);
        Assert.Null(attempt.StoredName);
    }

    [Fact]
    public async Task A_tag_past_the_per_track_cap_carries_the_cap()
    {
        await ScanAndAttachAsync();
        _tags.AddAnswer = _ => new AddTagResult(AddTagOutcome.RejectedLimitReached, null);

        var attempt = await AddTagAsync(_alphaId, "one more");

        Assert.Equal(TagAddOutcome.RejectedLimitReached, attempt.Outcome);
        Assert.Equal(ITagService.MaxTagsPerTrack, attempt.Limit);
    }

    [Theory]
    [InlineData(AddTagOutcome.RejectedEmpty, TagAddOutcome.RejectedEmpty)]
    [InlineData(AddTagOutcome.RejectedTrackNotFound, TagAddOutcome.RejectedTrackNotFound)]
    public async Task The_other_rejections_map_across_with_no_limit(AddTagOutcome outcome, TagAddOutcome expected)
    {
        await ScanAndAttachAsync();
        _tags.AddAnswer = _ => new AddTagResult(outcome, null);

        var attempt = await AddTagAsync(_alphaId, "x");

        Assert.Equal(expected, attempt.Outcome);
        Assert.Equal(0, attempt.Limit);
    }

    [Fact]
    public async Task Adding_a_tag_with_no_tag_service_publishes_Unavailable()
    {
        await _rig.Library.ScanAsync("/music", _rig.Stack());

        var attempt = await AddTagAsync(_alphaId, "peak");

        Assert.Equal(new TagAddAttempted(_alphaId, TagAddOutcome.Unavailable, null, 0), attempt);
        Assert.Empty(_tags.Added);
    }

    [Fact]
    public async Task A_tag_service_that_throws_publishes_nothing_and_the_handler_returns()
    {
        await ScanAndAttachAsync();
        _tags.AddAnswer = _ => throw new IOException("database is gone");
        var attempts = new RecordingHandler<TagAddAttempted>();
        using var sub = _rig.Bus.Subscribe(attempts);

        _handlers.Handle(new AddTagToTrack(_alphaId, "peak", _from));

        Assert.Empty(attempts.Received);
    }

    // ---- RemoveTagFromTrack ---------------------------------------------------------------------

    [Fact]
    public async Task Removing_a_tag_asks_the_service_to_remove_it()
    {
        await ScanAndAttachAsync();

        _handlers.Handle(new RemoveTagFromTrack(_alphaId, "peak", _from));

        Assert.Equal([(_alphaId, "peak")], _tags.Removed);
    }

    [Fact]
    public async Task Removing_a_tag_with_no_tag_service_does_nothing()
    {
        await _rig.Library.ScanAsync("/music", _rig.Stack());

        _handlers.Handle(new RemoveTagFromTrack(_alphaId, "peak", _from));

        Assert.Empty(_tags.Removed);
    }

    // ---- AddTrackToCrate ------------------------------------------------------------------------

    [Fact]
    public async Task Adding_to_an_existing_crate_files_the_track_and_publishes_TrackAddedToCrate()
    {
        await ScanAndAttachAsync();
        var crateId = await _rig.Crates.CreateAsync("Warmup");
        var added = new RecordingHandler<TrackAddedToCrate>();
        using var sub = _rig.Bus.Subscribe(added);

        _handlers.Handle(new AddTrackToCrate(_alphaId, crateId, "Warmup", false, _from));

        Assert.Equal([_alphaId], _rig.Crates.Members("Warmup"));
        Assert.Equal([new TrackAddedToCrate("Warmup", _alphaId)], added.Received);
    }

    [Fact]
    public async Task Adding_with_create_makes_the_crate_first_then_files_the_track()
    {
        await ScanAndAttachAsync();
        var added = new RecordingHandler<TrackAddedToCrate>();
        using var sub = _rig.Bus.Subscribe(added);

        _handlers.Handle(new AddTrackToCrate(_alphaId, 0, "Fresh", true, _from));

        Assert.Equal([_alphaId], _rig.Crates.Members("Fresh"));
        Assert.Equal([new TrackAddedToCrate("Fresh", _alphaId)], added.Received);
    }

    [Fact]
    public async Task Adding_to_a_crate_that_does_not_exist_publishes_nothing()
    {
        await ScanAndAttachAsync();
        var added = new RecordingHandler<TrackAddedToCrate>();
        using var sub = _rig.Bus.Subscribe(added);

        _handlers.Handle(new AddTrackToCrate(_alphaId, 99, "Ghost", false, _from));

        Assert.Empty(added.Received);
    }

    [Fact]
    public async Task Adding_to_a_crate_with_no_crate_service_does_nothing()
    {
        await _rig.Library.ScanAsync("/music", _rig.Stack());
        var added = new RecordingHandler<TrackAddedToCrate>();
        using var sub = _rig.Bus.Subscribe(added);

        _handlers.Handle(new AddTrackToCrate(_alphaId, 0, "Fresh", true, _from));

        Assert.Empty(added.Received);
        Assert.Empty(_rig.Crates.Members("Fresh"));
    }

    // ---- Through the bus ------------------------------------------------------------------------

    [Fact]
    public async Task A_command_sent_on_a_bus_reaches_the_handler()
    {
        await _rig.Library.ScanAsync("/music", null);
        var bus = new DataBus(new ThrowingFailureSink());
        bus.Register<SelectTrack>(_handlers);

        bus.Send(new SelectTrack(1, true, _from));

        Assert.Equal(1, _rig.Library.SelectedIndex);
    }
}
