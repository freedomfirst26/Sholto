using Sholto.Data;
using Sholto.App.Library;
using Sholto.App.Loading;

namespace Sholto.App;

/// <summary>Executes the browse knob's turn: it always moves the library selection, whether or not the search
/// overlay is open (search is driven from the keyboard and mouse only). A short press has no effect on the App
/// (the translator reports it as <see cref="ReportControl"/>). Loading the highlighted track into a deck
/// (LOAD 1/2, the keyboard's 1 and 2) and re-analysing it (the held browse knob) are the track loader's
/// (<see cref="ITrackLoader"/>).</summary>
public sealed class BrowseCommandHandlers(ITrackSelection selection) : ICommandHandler<RotateBrowse>
{
    private readonly ITrackSelection _selection = selection;

    public void Handle(in RotateBrowse command) => _selection.Rotate(command.Delta);
}
