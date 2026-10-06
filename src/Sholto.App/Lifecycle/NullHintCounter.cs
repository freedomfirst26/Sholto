using Sholto.Data;

namespace Sholto.App.Lifecycle;

/// <summary>A hint counter that saves nothing and always counts 0: for hosts with no database (the bench and the
/// UI harness).</summary>
public sealed class NullHintCounter : IHintCounter
{
    public void Handle(in RecordHintShown command) { }

    public Task<int> Handle(in GetHintShownCount query) => Task.FromResult(0);
}
