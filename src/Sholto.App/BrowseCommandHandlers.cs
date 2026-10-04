using Sholto.Data;
using Sholto.App.Library;

namespace Sholto.App;

/// <summary>Executes the browse knob turn. Loading the highlighted track into a deck (LOAD 1/2, the keyboard's
/// 1 and 2) and re-analysing it (the held browse knob) are the track loader's
/// (<see cref="Core.Loading.ITrackLoader"/>).</summary>
public sealed class BrowseCommandHandlers(ITrackSelection selection) : ICommandHandler<RotateBrowse>
{
    private readonly ITrackSelection _selection = selection;

    public void Handle(in RotateBrowse command) => _selection.Rotate(command.Delta);
}
