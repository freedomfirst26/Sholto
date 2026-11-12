using Microsoft.Extensions.Options;
using Sholto.App.Analysis.Analyzers;
using Sholto.App.Analysis.Analyzers.Segments;
using Sholto.App.Analysis.Analyzers.Waveform;
using Sholto.App.Audio;
using Sholto.App.Library;
using Sholto.Data;
using Sholto.Interface.MainUI.Theming;
using Sholto.Interface.MainUI.ViewModels;

namespace Sholto.Interface.MainUI.Tests;

/// <summary>Every deck action is exactly one command, sent by the tune-editor commands.</summary>
[Collection(DeckViewModelCollection.Name)]
public class DeckViewModelTuneEditorCommandsTests
{
    private readonly DeckViewModelRig rig = new();

    [Fact]
    public void ToggleEditor_sends_ToggleTuneEditor_for_its_deck()
    {
        var sent = rig.Record<ToggleTuneEditor>();

        rig.NewDeck(1).ToggleEditor();

        var command = Assert.Single(sent.Received);
        Assert.Equal(1, command.Deck);
        Assert.Equal(InterfaceIds.MainUI, command.Origin.InterfaceId);
    }

    [Fact]
    public void CloseEditor_sends_CloseTuneEditor_for_its_deck()
    {
        var sent = rig.Record<CloseTuneEditor>();

        rig.NewDeck(1).CloseEditor();

        var command = Assert.Single(sent.Received);
        Assert.Equal(1, command.Deck);
        Assert.Equal(InterfaceIds.MainUI, command.Origin.InterfaceId);
    }

    [Theory]
    [InlineData(BpmMultiplierOp.Toggle)]
    [InlineData(BpmMultiplierOp.Halve)]
    [InlineData(BpmMultiplierOp.Double)]
    [InlineData(BpmMultiplierOp.Reset)]
    public void ChangeBpm_sends_ChangeBpmMultiplier_with_the_op(BpmMultiplierOp op)
    {
        var sent = rig.Record<ChangeBpmMultiplier>();

        rig.NewDeck(0).ChangeBpm(op);

        var command = Assert.Single(sent.Received);
        Assert.Equal(0, command.Deck);
        Assert.Equal(op, command.Op);
        Assert.Equal(InterfaceIds.MainUI, command.Origin.InterfaceId);
    }

    [Fact]
    public void AdjustBpm_sends_AdjustBpm_with_the_delta()
    {
        var sent = rig.Record<AdjustBpm>();

        rig.NewDeck(1).AdjustBpm(-0.1);

        var command = Assert.Single(sent.Received);
        Assert.Equal(1, command.Deck);
        Assert.Equal(-0.1, command.Delta);
        Assert.Equal(InterfaceIds.MainUI, command.Origin.InterfaceId);
    }

    [Fact]
    public void NudgeGridFine_sends_NudgeGridFine_with_the_seconds()
    {
        var sent = rig.Record<NudgeGridFine>();

        rig.NewDeck(1).NudgeGridFine(0.01);

        var command = Assert.Single(sent.Received);
        Assert.Equal(1, command.Deck);
        Assert.Equal(0.01, command.Seconds);
        Assert.Equal(InterfaceIds.MainUI, command.Origin.InterfaceId);
    }

    [Fact]
    public void NudgeGrid_sends_NudgeGrid_with_the_beats()
    {
        var sent = rig.Record<NudgeGrid>();

        rig.NewDeck(0).NudgeGrid(-1);

        var command = Assert.Single(sent.Received);
        Assert.Equal(0, command.Deck);
        Assert.Equal(-1, command.Beats);
        Assert.Equal(InterfaceIds.MainUI, command.Origin.InterfaceId);
    }

    [Fact]
    public void ResetToAnalysis_sends_ResetDeckToAnalysis_for_its_deck()
    {
        var sent = rig.Record<ResetDeckToAnalysis>();

        rig.NewDeck(1).ResetToAnalysis();

        var command = Assert.Single(sent.Received);
        Assert.Equal(1, command.Deck);
        Assert.Equal(InterfaceIds.MainUI, command.Origin.InterfaceId);
    }

    [Fact]
    public void ClickGrid_sends_ClickGrid_with_the_seconds()
    {
        var sent = rig.Record<ClickGrid>();

        rig.NewDeck(0).ClickGrid(12.5);

        var command = Assert.Single(sent.Received);
        Assert.Equal(0, command.Deck);
        Assert.Equal(12.5, command.Seconds);
        Assert.Equal(InterfaceIds.MainUI, command.Origin.InterfaceId);
    }

    [Fact]
    public void An_action_changes_nothing_on_the_view_model_until_the_App_answers()
    {
        rig.Record<ToggleTuneEditor>();

        rig.Deck.ToggleEditor();

        Assert.False(rig.Deck.EditOpen);
        Assert.Empty(rig.Changed);
    }
}
