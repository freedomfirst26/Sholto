namespace Sholto.Interface.Keyboard;

/// <summary>What a key press means, private to the keyboard interface: the gesture id, the deck it acts
/// on (-1 for none) and the key the Origin names.</summary>
internal readonly record struct KeyboardGesture(string Id, int Deck, string ControlId);
