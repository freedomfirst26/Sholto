namespace Sholto.Data;

/// <summary>A deck's channel fader, 0 to 1.</summary>
public readonly record struct SetChannelVolume(int Deck, double Value, Origin Origin) : ICommand;
