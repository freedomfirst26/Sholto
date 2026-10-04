namespace Sholto.Data;

/// <summary>The ~60 Hz tick on the app thread. Handlers run in ascending <c>order</c>, ties in
/// subscription order. Convention: the App core subscribes at order 0..99 (it acts first), interfaces
/// at 100 and above (they observe the result). A handler that throws is not caught here.</summary>
public interface IFrameClock
{
    /// <summary>The current time (UTC).</summary>
    DateTime Now { get; }

    void Subscribe(IFrameTickHandler handler, int order);
}
