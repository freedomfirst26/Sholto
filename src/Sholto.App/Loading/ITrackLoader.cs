using Sholto.Data;

namespace Sholto.App.Loading;

/// <summary>Loads the highlighted library track into a deck (LOAD buttons, keys, the search overlay), and
/// re-analyses it (browse-hold, library double-click). The one loader: there is no other load path.</summary>
public interface ITrackLoader : ICommandHandler<LoadSelectedIntoDeck>, ICommandHandler<ReanalyzeSelected>
{
}
