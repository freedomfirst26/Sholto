namespace Sholto.Data;

/// <summary>How <see cref="ChangeBpmMultiplier"/> changes a deck's half/double BPM override.</summary>
public enum BpmMultiplierOp
{
    /// <summary>One-click flip-flop: back to 1.0 if overridden, else the most likely correction (halve a high BPM, double a low one).</summary>
    Toggle,
    Halve,
    Double,
    /// <summary>Back to 1.0.</summary>
    Reset,
}
