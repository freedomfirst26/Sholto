namespace Sholto.Data;

/// <summary>The one app thread (the UI thread), lent to the App. The bus never marshals; an interface
/// that receives input on another thread (MIDI) posts onto the app thread before sending commands.</summary>
public interface IAppThread
{
    /// <summary>Run <paramref name="action"/> on the app thread, later.</summary>
    void Post(Action action);

    /// <summary>True when the caller is already on the app thread.</summary>
    bool IsCurrent { get; }
}
