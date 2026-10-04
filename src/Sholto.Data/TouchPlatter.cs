namespace Sholto.Data;

/// <summary>The top platter of a deck was touched or released. <see cref="Shifted"/> is the deck's Shift state at that moment: Shift + touch is the silent fast search, not a scratch grab.</summary>
public readonly record struct TouchPlatter(int Deck, bool Touching, bool Shifted, Origin Origin) : ICommand;
