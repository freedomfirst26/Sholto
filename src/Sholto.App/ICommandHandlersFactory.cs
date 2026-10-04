using Sholto.Data;
using Sholto.App;
using Sholto.App.Lifecycle;
using Sholto.App.Performance;

namespace Sholto.App;

/// <summary>Builds the command handlers and registers each on the bus.</summary>
public interface ICommandHandlersFactory
{
    /// <summary>Register the handler for every command the interfaces send. The platter commands are the
    /// <paramref name="platter"/>'s, loading and re-analysing the core's track loader's; Inspect mode
    /// handles <c>SetInspectMode</c>; the folder, device and theme commands are the
    /// <paramref name="lifecycle"/>'s. The library queries (tags, crates) are registered on
    /// <paramref name="queries"/>.</summary>
    void Register(ICommandRegistry registry, IQueryRegistry queries, CoreStack core, ICueRouting cueRouting,
        IInspectMode inspectMode, IPlatter platter, IAppLifecycle lifecycle);
}
