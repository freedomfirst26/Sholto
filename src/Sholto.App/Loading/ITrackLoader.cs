using Sholto.App.Library;
using Sholto.Data;

namespace Sholto.App.Loading;

/// <summary>Loads the highlighted library track (or the search pick, while search is active) into a deck (LOAD buttons, keys, the search overlay), and
/// re-analyses it (browse-hold, library double-click). The one loader: there is no other load path.</summary>
public interface ITrackLoader : ICommandHandler<LoadSelectedIntoDeck>, ICommandHandler<ReanalyzeSelected>, ICommandHandler<UndoLastLoad>
{
    /// <summary>A load started on a deck: raised on the app thread, once per load, after <see cref="LoadAccepted"/>
    /// is published. For in-App listeners.</summary>
    event Action<int, Track>? Accepted;
}
