using Avalonia.Input;
using Sholto.Data;

namespace Sholto.Interface.Keyboard.Tests;

public class KeyboardInputTests
{
    private readonly FakeKeyboard _keyboard = new();
    private readonly RecordingCommandSender _sender = new();

    public KeyboardInputTests() =>
        new KeyboardInputFactory().Create(_keyboard, _sender).Start();

    [Fact]
    public void P_sends_play_on_deck_one_synchronously_and_marks_the_key_handled()
    {
        var args = _keyboard.Press(Key.P);

        var play = Assert.IsType<TogglePlay>(Assert.Single(_sender.Sent));
        Assert.Equal(0, play.Deck);
        Assert.Equal(new Origin(InterfaceIds.Keyboard, "key.p", "play.press"), play.Origin);
        Assert.True(args.Handled);
    }

    [Fact]
    public void Shift_P_plays_deck_two()
    {
        _keyboard.Press(Key.P, KeyModifiers.Shift);
        Assert.Equal(1, Assert.IsType<TogglePlay>(Assert.Single(_sender.Sent)).Deck);
    }

    [Fact]
    public void The_number_keys_load_the_highlighted_track_into_a_deck()
    {
        _keyboard.Press(Key.D1);
        _keyboard.Press(Key.D2);
        Assert.Equal([0, 1], _sender.Sent.Cast<LoadSelectedIntoDeck>().Select(c => c.Deck));
    }

    [Fact]
    public void Shift_and_the_number_keys_load_directly_into_deck_one_or_two()
    {
        _keyboard.Press(Key.D1, KeyModifiers.Shift);
        _keyboard.Press(Key.NumPad2, KeyModifiers.Shift);
        Assert.Equal([0, 1], _sender.Sent.Cast<LoadSelectedIntoDeck>().Select(c => c.Deck));
    }

    [Fact]
    public void Ctrl_Z_undoes_the_last_load_and_nothing_else_with_Z_does()
    {
        var args = _keyboard.Press(Key.Z, KeyModifiers.Control);

        var undo = Assert.IsType<UndoLastLoad>(Assert.Single(_sender.Sent));
        Assert.Equal(new Origin(InterfaceIds.Keyboard, "key.ctrl+z", "load.undo"), undo.Origin);
        Assert.True(args.Handled);

        _keyboard.Press(Key.Z);
        _keyboard.Press(Key.Z, KeyModifiers.Control | KeyModifiers.Shift);
        Assert.Single(_sender.Sent);
    }

    [Fact]
    public void M_adds_a_marker_and_G_opens_the_grid_editor()
    {
        _keyboard.Press(Key.M, KeyModifiers.Shift);
        _keyboard.Press(Key.G);

        Assert.Equal(1, Assert.IsType<AddMarker>(_sender.Sent[0]).Deck);
        Assert.IsType<OpenGridEditor>(_sender.Sent[1]);
    }

    [Fact]
    public void A_key_that_is_not_a_gesture_sends_nothing_and_stays_unhandled_so_typing_in_search_works()
    {
        var args = _keyboard.Press(Key.A);

        Assert.Empty(_sender.Sent);
        Assert.False(args.Handled);
    }

    [Fact]
    public void Every_recognised_key_is_sent_and_handled_whatever_the_apps_mode()
    {
        // The keyboard has no notion of Inspect: the App's gate decides whether the command runs.
        foreach (var key in new[] { Key.P, Key.D1, Key.D2, Key.M, Key.G })
            Assert.True(_keyboard.Press(key).Handled, key.ToString());

        Assert.Equal(5, _sender.Sent.Count);
    }
}
