using Sholto.Data;

namespace Sholto.App.Recording;

/// <summary>Ignores <see cref="ToggleMixRecording"/>: the handler for composition roots that record nothing (the
/// bench, the UI harness, tests).</summary>
public sealed class NullMixRecording : ICommandHandler<ToggleMixRecording>
{
    public void Handle(in ToggleMixRecording command)
    {
    }
}
