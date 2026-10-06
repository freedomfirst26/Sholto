using Sholto.Data;
using Sholto.App.Library;
using Sholto.App.Loading;

namespace Sholto.App;

/// <summary>Executes the browse knob: a turn moves the library selection, or, while the search overlay is
/// active, is relayed as <see cref="SearchCursorMoved"/>; a push is relayed as <see cref="SearchRequested"/>.
/// Loading the highlighted track into a deck (LOAD 1/2, the keyboard's 1 and 2) and re-analysing it (the held
/// browse knob) are the track loader's (<see cref="ITrackLoader"/>).</summary>
public sealed class BrowseCommandHandlers(ITrackSelection selection, ISearchPick pick, IEventPublisher publisher)
    : ICommandHandler<RotateBrowse>, ICommandHandler<OpenSearch>
{
    private readonly ITrackSelection _selection = selection;
    private readonly ISearchPick _pick = pick;
    private readonly IEventPublisher _publisher = publisher;

    public void Handle(in RotateBrowse command)
    {
        if (_pick.Active) _publisher.Publish(new SearchCursorMoved(command.Delta));
        else _selection.Rotate(command.Delta);
    }

    public void Handle(in OpenSearch command) => _publisher.Publish(new SearchRequested(command.Origin));
}
