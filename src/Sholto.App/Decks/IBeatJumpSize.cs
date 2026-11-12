namespace Sholto.App.Decks;

/// <summary>Each deck's BEAT JUMP size: the scale every BEAT JUMP pad's beats are multiplied by. Starts at 1
/// for both decks, is not tied to the loaded track, and stays within <see cref="BeatJumpOptions"/>.</summary>
public interface IBeatJumpSize
{
    /// <summary>The deck's current scale; 1 for a deck number it does not know.</summary>
    double ScaleFor(int deck);

    /// <summary>Halve the deck's scale, unless that would go below the smallest size.</summary>
    void Halve(int deck);

    /// <summary>Double the deck's scale, unless that would go above the largest size.</summary>
    void Double(int deck);
}
