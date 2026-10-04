namespace Sholto.Data;

/// <summary>An app thread that runs posted work at once, on the caller. For Bench and tests, where the
/// caller already is the app thread.</summary>
public sealed class ImmediateAppThread : IAppThread
{
    public void Post(Action action) => action();

    public bool IsCurrent => true;
}
