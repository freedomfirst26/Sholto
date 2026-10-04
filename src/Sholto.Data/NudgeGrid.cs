namespace Sholto.Data;

/// <summary>Move a deck's beatgrid by <see cref="Beats"/>. <see cref="Deck"/> is -1 when the interface could not pick one (the shared BEAT arrows with no Shift held); the App then picks the deck with a loop running, else deck 0.</summary>
public readonly record struct NudgeGrid(int Deck, int Beats, Origin Origin) : ICommand;
