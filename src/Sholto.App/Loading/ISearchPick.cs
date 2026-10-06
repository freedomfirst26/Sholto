using Sholto.App.Library;
using Sholto.Data;

namespace Sholto.App.Loading;

/// <summary>What the search overlay's highlight points at. While <see cref="Active"/>, load, re-analyse and the
/// browse knob act on the search instead of the library selection. The Interface sends it as
/// <see cref="SetSearchPick"/> whenever the highlight changes.</summary>
public interface ISearchPick : ICommandHandler<SetSearchPick>
{
    /// <summary>The search overlay is open and owns load, re-analyse and the browse knob.</summary>
    bool Active { get; }

    /// <summary>The track load means while active: resolved from the catalog (so it need not be among the visible
    /// rows), or null when nothing is picked or the file is not in the catalog.</summary>
    Track? PickedTrack { get; }
}
