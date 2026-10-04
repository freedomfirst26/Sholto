using Sholto.App.Audio;

namespace Sholto.TestSupport;

/// <summary>A playhead the test moves.</summary>
internal sealed class ScriptedPlayhead : IDeckPlayhead
{
    public long PositionFrames { get; set; }

    public double PlayPosition { get; set; }
}
