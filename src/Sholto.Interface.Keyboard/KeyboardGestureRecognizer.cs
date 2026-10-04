using Avalonia.Input;

namespace Sholto.Interface.Keyboard;

/// <summary>Only keys with a DDJ-FLX4 equivalent (or that act on a deck like one) are gestures;
/// everything else a key does is UI chrome and stays in <c>MainWindow</c>. Shift picks deck 2 for the
/// per-deck keys.</summary>
internal sealed class KeyboardGestureRecognizer : IKeyboardGestureRecognizer
{
    public KeyboardGesture? Recognize(KeyboardEvent key)
    {
        var shiftDeck = (key.Modifiers & KeyModifiers.Shift) != 0 ? 1 : 0;
        return key.Key switch
        {
            Key.D1 or Key.NumPad1 => new KeyboardGesture(KeyboardGestureIds.LoadPress, 0, "key.1"),
            Key.D2 or Key.NumPad2 => new KeyboardGesture(KeyboardGestureIds.LoadPress, 1, "key.2"),
            Key.P => new KeyboardGesture(KeyboardGestureIds.PlayPress, shiftDeck, "key.p"),
            Key.M => new KeyboardGesture(KeyboardGestureIds.MarkerAdd, shiftDeck, "key.m"),
            Key.G => new KeyboardGesture(KeyboardGestureIds.GridEditOpen, -1, "key.g"),
            _ => null,
        };
    }
}
