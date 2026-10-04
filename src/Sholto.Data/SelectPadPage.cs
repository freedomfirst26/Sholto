namespace Sholto.Data;

/// <summary>Make <see cref="Page"/> the deck's active pad page.</summary>
public readonly record struct SelectPadPage(int Deck, PadPage Page, Origin Origin) : ICommand;
