using Sholto.Data;

namespace Sholto.Interface.MainUI.ViewModels;

public sealed class DeckTuneEditorCommands(int deck, ICommandSender sender) : IDeckTuneEditorCommands
{
    private readonly int _deck = deck;
    private readonly ICommandSender _sender = sender;

    private Origin Ui(string control, string gesture) =>
        new(InterfaceIds.MainUI, $"deck{_deck + 1}.{control}", gesture, _deck);

    public void ToggleEditor() => _sender.Send(new ToggleTuneEditor(_deck, Ui("bpm", "click")));

    public void CloseEditor() => _sender.Send(new CloseTuneEditor(_deck, Ui("tune-editor", "close")));

    public void ChangeBpm(BpmMultiplierOp op) =>
        _sender.Send(new ChangeBpmMultiplier(_deck, op, Ui("tune-editor.bpm-multiplier", "click")));

    public void AdjustBpm(double delta) =>
        _sender.Send(new AdjustBpm(_deck, delta, Ui("tune-editor.bpm", "step")));

    public void NudgeGridFine(double seconds) =>
        _sender.Send(new NudgeGridFine(_deck, seconds, Ui("tune-editor.grid", "nudge")));

    public void NudgeGrid(int beats) =>
        _sender.Send(new NudgeGrid(_deck, beats, Ui("tune-editor.grid", "nudge-beat")));

    public void ResetToAnalysis() =>
        _sender.Send(new ResetDeckToAnalysis(_deck, Ui("tune-editor.reset", "click")));

    public void ClickGrid(double seconds) => _sender.Send(new ClickGrid(_deck, seconds, Ui("waveform", "click")));
}
