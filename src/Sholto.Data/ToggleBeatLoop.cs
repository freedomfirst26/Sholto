namespace Sholto.Data;

/// <summary>Engage a loop on a deck, or exit the loop that is running. The loop is <see cref="Bars"/> whole
/// bars, unless <see cref="Beats"/> is given (greater than zero): then it is that many beats, which may be a
/// fraction of a beat (the BEAT LOOP pads go down to 1/4 beat).</summary>
public readonly record struct ToggleBeatLoop(int Deck, int Bars, Origin Origin, double Beats = 0) : ICommand
{
    /// <summary>A bar is four beats (the glossary's bar).</summary>
    private const int BeatsPerBar = 4;

    /// <summary>The loop's length in beats: <see cref="Beats"/> when given, else <see cref="Bars"/> bars.</summary>
    public double LengthBeats => Beats > 0 ? Beats : Bars * BeatsPerBar;
}
