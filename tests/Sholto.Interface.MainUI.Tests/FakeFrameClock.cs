using Sholto.Data;

namespace Sholto.Interface.MainUI.Tests;

/// <summary>A frame clock whose time moves only when a test says so.</summary>
internal sealed class FakeFrameClock : IFrameClock
{
    public DateTime Now { get; private set; } = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public void Advance(double seconds) => Now = Now.AddSeconds(seconds);

    public void Subscribe(IFrameTickHandler handler, int order) { }
}
