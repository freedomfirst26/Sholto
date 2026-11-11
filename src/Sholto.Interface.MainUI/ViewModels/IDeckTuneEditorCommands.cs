using Sholto.Data;

namespace Sholto.Interface.MainUI.ViewModels;

/// <summary>The actions of the tune editor and grid on one deck's screen: each one sends exactly one command.</summary>
public interface IDeckTuneEditorCommands
{
    /// <summary>Click the BPM: open the tune editor over the disc, or close it if it is open.</summary>
    void ToggleEditor();

    void CloseEditor();

    /// <summary>The editor's half button, and the ÷2 / ×2 / reset gestures.</summary>
    void ChangeBpm(BpmMultiplierOp op);

    void AdjustBpm(double delta);

    void NudgeGridFine(double seconds);

    void NudgeGrid(int beats);

    void ResetToAnalysis();

    /// <summary>A waveform click while the grid-edit mode is on.</summary>
    void ClickGrid(double seconds);
}
