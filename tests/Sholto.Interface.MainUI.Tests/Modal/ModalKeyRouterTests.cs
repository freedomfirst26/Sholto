using Avalonia.Input;
using Sholto.Interface.MainUI.Controls.Modal;

namespace Sholto.Interface.MainUI.Tests.Modal;

public class ModalKeyRouterTests
{
    private readonly ModalKeyRouter _router = new();
    private static readonly ModalButtons Wizard = new("Cancel", "← Back", "Apply");

    private bool Route(FakeModalContent modal, Key key) => _router.Route(modal, key, KeyModifiers.None);

    [Fact]
    public void Escape_dismisses_once_and_is_consumed()
    {
        var modal = new FakeModalContent();
        Assert.True(Route(modal, Key.Escape));
        Assert.Equal(1, modal.DismissCalls);
    }

    [Fact]
    public void The_contents_own_key_wins_over_Escape()
    {
        var modal = new FakeModalContent();
        modal.HandledKeys.Add(Key.Escape);
        Assert.True(Route(modal, Key.Escape));
        Assert.Equal(0, modal.DismissCalls);
    }

    [Fact]
    public void Enter_confirms_when_there_is_a_primary_slot_and_it_can_confirm()
    {
        var modal = new FakeModalContent { Buttons = Wizard };
        Assert.True(Route(modal, Key.Enter));
        Assert.Equal(1, modal.ConfirmCalls);
    }

    [Fact]
    public void Enter_with_a_primary_slot_that_cannot_confirm_is_consumed_and_does_nothing()
    {
        var modal = new FakeModalContent { Buttons = Wizard, CanConfirm = false };
        Assert.True(Route(modal, Key.Enter));
        Assert.Equal(0, modal.ConfirmCalls);
    }

    [Fact]
    public void Enter_without_a_primary_slot_never_confirms()
    {
        var modal = new FakeModalContent { Buttons = new ModalButtons("Close", null, null) };
        Assert.True(Route(modal, Key.Enter));
        Assert.Equal(0, modal.ConfirmCalls);
    }

    [Fact]
    public void Backspace_goes_back_only_with_a_back_slot_that_can_go_back()
    {
        var modal = new FakeModalContent { Buttons = Wizard, CanGoBack = true };
        Assert.True(Route(modal, Key.Back));
        Assert.Equal(1, modal.BackCalls);

        var disabled = new FakeModalContent { Buttons = Wizard, CanGoBack = false };
        Assert.True(Route(disabled, Key.Back));
        Assert.Equal(0, disabled.BackCalls);

        var noSlot = new FakeModalContent { Buttons = new ModalButtons("Cancel", null, "Apply"), CanGoBack = true };
        Assert.True(Route(noSlot, Key.Back));
        Assert.Equal(0, noSlot.BackCalls);
    }

    [Fact]
    public void Backspace_in_a_text_modal_with_nothing_to_go_back_to_is_left_for_the_text_box()
    {
        var modal = new FakeModalContent { Buttons = Wizard, CanGoBack = false, CapturesText = true };
        Assert.False(Route(modal, Key.Back));
        Assert.Equal(0, modal.BackCalls);
    }

    [Fact]
    public void An_unhandled_letter_is_swallowed_unless_the_content_captures_text()
    {
        Assert.True(Route(new FakeModalContent { CapturesText = false }, Key.A));
        Assert.False(Route(new FakeModalContent { CapturesText = true }, Key.A));
    }

    [Fact]
    public void A_plain_modal_without_content_chrome_still_gets_Escape()
    {
        var modal = new FakeModalContent();
        IModal asModal = modal;
        Assert.True(_router.Route(asModal, Key.Escape, KeyModifiers.None));
        Assert.Equal(1, modal.DismissCalls);
    }

    [Fact]
    public void The_order_is_content_then_Escape_then_Enter_then_Backspace()
    {
        var modal = new FakeModalContent { Buttons = Wizard };
        modal.HandledKeys.Add(Key.Enter);
        Assert.True(Route(modal, Key.Enter));
        Assert.Equal(0, modal.ConfirmCalls);
        Assert.Equal(new[] { Key.Enter }, modal.KeysSeen);
    }
}
