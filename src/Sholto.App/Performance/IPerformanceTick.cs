using Sholto.Data;

namespace Sholto.App.Performance;

/// <summary>The performance engine's frame: the ordered per-frame run of seek flush, scrub flag, scratch and magnet.</summary>
public interface IPerformanceTick : IFrameTickHandler, IDisposable
{
    /// <summary>Subscribe to the frame clock at order 0 (the App acts first; interfaces tick after).</summary>
    void Start();
}
