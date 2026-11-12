namespace Sholto.Data;

/// <summary>Double the jump of every BEAT JUMP pad on a deck (up to the App's largest size).</summary>
public readonly record struct DoubleBeatJump(int Deck, Origin Origin) : ICommand;
