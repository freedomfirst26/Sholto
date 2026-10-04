using Sholto.Data;

namespace Sholto.TestSupport;

/// <summary>A frame clock whose time the test sets. It never ticks anyone: a test calls the code under
/// test itself.</summary>
internal sealed class SettableFrameClock : IFrameClock
{
    public DateTime Now { get; set; } = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public void Subscribe(IFrameTickHandler handler, int order)
    {
    }
}
