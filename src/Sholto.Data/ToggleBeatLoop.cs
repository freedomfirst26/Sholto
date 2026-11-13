namespace Sholto.Data;

/// <summary>Engage a loop on a deck, or exit or resize the loop that is running. The loop is <see cref="Bars"/>
/// whole bars (the 4 BEAT button: a press with a loop running exits it), unless <see cref="Beats"/> is given
/// (greater than zero): then it is that many beats, which may be a fraction of a beat (the BEAT LOOP pads go
/// down to 1/4 beat), and a press with a loop of another length running resizes that loop to it, while a
/// press matching the running loop's length exits it.</summary>
public readonly record struct ToggleBeatLoop(int Deck, int Bars, Origin Origin, double Beats = 0) : ICommand
{
    /// <summary>A bar is four beats (the glossary's bar).</summary>
    private const int BeatsPerBar = 4;

    /// <summary>The loop's length in beats: <see cref="Beats"/> when given, else <see cref="Bars"/> bars.</summary>
    public double LengthBeats => Beats > 0 ? Beats : Bars * BeatsPerBar;

    /// <summary>True when the length was given in <see cref="Beats"/> (a BEAT LOOP pad), so a running loop
    /// of another length is resized rather than exited.</summary>
    public bool IsBeatLength => Beats > 0;
}
