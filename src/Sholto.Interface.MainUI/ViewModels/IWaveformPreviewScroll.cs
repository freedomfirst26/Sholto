namespace Sholto.Interface.MainUI.ViewModels;

/// <summary>Where the Layout Wizard's preview cards are in the demo track. All cards share it, so they
/// scroll in step. Position is the column (one per peak) under the fixed playhead.</summary>
public interface IWaveformPreviewScroll
{
    /// <summary>Column under the playhead, 0 up to <see cref="Length"/>; wraps when the demo loops.</summary>
    double Position { get; }

    /// <summary>Columns in the demo track.</summary>
    double Length { get; }

    /// <summary>True while the wizard is open and the position is advancing.</summary>
    bool IsRunning { get; }

    /// <summary>Raised each frame the position moved.</summary>
    event EventHandler? Moved;

    /// <summary>Begin advancing from <paramref name="startColumn"/>.</summary>
    void Start(double length, double columnsPerSecond, double startColumn);

    void Stop();
}
