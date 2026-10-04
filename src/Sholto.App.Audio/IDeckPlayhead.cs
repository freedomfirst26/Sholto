namespace Sholto.App.Audio;

/// <summary>Read-only view of where the deck's playhead is.</summary>
public interface IDeckPlayhead
{
    long PositionFrames { get; }
    double PlayPosition { get; }
}
