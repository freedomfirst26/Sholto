using Avalonia.Input;

namespace Sholto.Interface.Keyboard.Tests;

public class KeyboardGestureRecognizerTests
{
    private readonly KeyboardGestureRecognizer _recognizer = new();

    private KeyboardGesture? Recognize(Key key, KeyModifiers modifiers = KeyModifiers.None) =>
        _recognizer.Recognize(new KeyboardEvent(key, modifiers));

    [Theory]
    [InlineData(Key.D1, 0)]
    [InlineData(Key.NumPad1, 0)]
    [InlineData(Key.D2, 1)]
    [InlineData(Key.NumPad2, 1)]
    public void The_number_keys_load_into_their_deck(Key key, int deck)
    {
        var g = Recognize(key);
        Assert.Equal(("load.press", deck), (g!.Value.Id, g.Value.Deck));
    }

    [Fact]
    public void P_plays_deck_one_and_shift_P_plays_deck_two()
    {
        Assert.Equal(("play.press", 0), (Recognize(Key.P)!.Value.Id, Recognize(Key.P)!.Value.Deck));
        Assert.Equal(1, Recognize(Key.P, KeyModifiers.Shift)!.Value.Deck);
    }

    [Fact]
    public void M_drops_a_marker_on_the_shift_chosen_deck_and_G_opens_the_grid_editor_with_no_deck()
    {
        Assert.Equal(("marker.add", 0), (Recognize(Key.M)!.Value.Id, Recognize(Key.M)!.Value.Deck));
        Assert.Equal(1, Recognize(Key.M, KeyModifiers.Shift)!.Value.Deck);
        Assert.Equal(("gridedit.open", -1), (Recognize(Key.G)!.Value.Id, Recognize(Key.G)!.Value.Deck));
    }

    [Theory]
    [InlineData(Key.A)]
    [InlineData(Key.Enter)]
    [InlineData(Key.Space)]
    [InlineData(Key.D3)]
    public void Every_other_key_is_not_a_gesture(Key key) => Assert.Null(Recognize(key));
}
