using Sholto.Data;

namespace Sholto.App;

/// <summary>The App's Inspect mode: the one source of truth for whether the guide is explaining controls
/// instead of the controls acting. Changed only by the <see cref="SetInspectMode"/> command.</summary>
public interface IInspectMode : ICommandHandler<SetInspectMode>
{
    /// <summary>True while Inspect mode is on. A plain field read: the command gate checks it on every command.</summary>
    bool IsOn { get; }
}
