using Sholto.App.Glance;
using Sholto.App.Library.Crates;
using Sholto.App.Library.Tags;
using Sholto.Data;

namespace Sholto.App.Library;

/// <summary>Executes the library commands an interface sends: highlight a row, filter by tag or crate,
/// clear the filter, tag a track, remove a tag, add a track to a crate. Thin: each calls the library
/// session or its database services. The database calls are async; their outcomes are published on the
/// app thread as facts (<see cref="TagAddAttempted"/>, <see cref="TrackAddedToCrate"/>).</summary>
public sealed class LibraryCommandHandlers(ILibrarySession library, IAppThread appThread, IEventPublisher publisher, ICrateMembershipCache membership) :
    ICommandHandler<SelectTrack>,
    ICommandHandler<FilterLibraryByTag>,
    ICommandHandler<FilterLibraryByCrate>,
    ICommandHandler<ClearLibraryFilter>,
    ICommandHandler<AddTagToTrack>,
    ICommandHandler<RemoveTagFromTrack>,
    ICommandHandler<AddTrackToCrate>
{
    private readonly ILibrarySession _library = library;
    private readonly IAppThread _appThread = appThread;
    private readonly IEventPublisher _publisher = publisher;
    private readonly ICrateMembershipCache _membership = membership;

    public void Handle(in SelectTrack command)
    {
        if (command.Clamp) _library.Select(command.Index);
        else _library.SetSelectedIndex(command.Index);
    }

    public void Handle(in FilterLibraryByTag command) => _ = ObserveAsync(_library.FilterByTagAsync(command.Tag), "filter by tag");

    public void Handle(in FilterLibraryByCrate command) =>
        _ = ObserveAsync(_library.FilterByCrateAsync(new CrateSummary(command.CrateId, command.Name, 0)), "filter by crate");

    public void Handle(in ClearLibraryFilter command) => _library.ClearFilter();

    public void Handle(in AddTagToTrack command) => _ = AddTagAsync(command.TrackId, command.Name);

    public void Handle(in RemoveTagFromTrack command) => _ = RemoveTagAsync(command.TrackId, command.Name);

    public void Handle(in AddTrackToCrate command) =>
        _ = AddToCrateAsync(command.TrackId, command.CrateId, command.CrateName, command.Create);

    private async Task AddTagAsync(Guid trackId, string name)
    {
        var tags = _library.Tags;
        if (tags is null)
        {
            Announce(new TagAddAttempted(trackId, TagAddOutcome.Unavailable, null, 0));
            return;
        }
        try
        {
            var result = await tags.AddTagAsync(trackId, name, default);
            var limit = result.Outcome switch
            {
                AddTagOutcome.RejectedTooLong => TagNameNormalizer.MaxLength,
                AddTagOutcome.RejectedLimitReached => ITagService.MaxTagsPerTrack,
                _ => 0,
            };
            Announce(new TagAddAttempted(trackId, Map(result.Outcome), result.StoredName, limit));
        }
        catch (Exception ex) { Console.WriteLine($"[Tags] add failed: {ex.Message}"); }
    }

    private async Task RemoveTagAsync(Guid trackId, string name)
    {
        var tags = _library.Tags;
        if (tags is null) return;
        try { await tags.RemoveTagAsync(trackId, name, default); }
        catch (Exception ex) { Console.WriteLine($"[Tags] remove failed: {ex.Message}"); }
    }

    private async Task AddToCrateAsync(Guid trackId, int crateId, string crateName, bool create)
    {
        var crates = _library.Crates;
        if (crates is null) return;
        try
        {
            var id = create ? await crates.CreateAsync(crateName) : crateId;
            await crates.AddTrackAsync(id, trackId);
            Console.WriteLine($"[Crate] added track {trackId} to \"{crateName}\"");
            _appThread.Post(() =>
            {
                _membership.Invalidate();
                _publisher.Publish(new TrackAddedToCrate(crateName, trackId));
            });
        }
        catch (Exception ex) { Console.WriteLine($"[Crate] add failed: {ex.Message}"); }
    }

    private async Task ObserveAsync(Task task, string what)
    {
        try { await task; }
        catch (Exception ex) { Console.WriteLine($"[Library] {what} failed: {ex.Message}"); }
    }

    private void Announce(TagAddAttempted attempt) => _appThread.Post(() => _publisher.Publish(attempt));

    private TagAddOutcome Map(AddTagOutcome outcome) => outcome switch
    {
        AddTagOutcome.Added => TagAddOutcome.Added,
        AddTagOutcome.AlreadyPresent => TagAddOutcome.AlreadyPresent,
        AddTagOutcome.RejectedEmpty => TagAddOutcome.RejectedEmpty,
        AddTagOutcome.RejectedTooLong => TagAddOutcome.RejectedTooLong,
        AddTagOutcome.RejectedLimitReached => TagAddOutcome.RejectedLimitReached,
        _ => TagAddOutcome.RejectedTrackNotFound,
    };
}
