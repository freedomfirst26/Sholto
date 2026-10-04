namespace Sholto.App.Performance;

/// <summary>The readers' side of jog recency: the scrub flag and the magnet's eligibility and quantize
/// logic. Deck numbers here are 1 or 2 (not 0-based), as they always were.</summary>
public interface IJogRecencyReader
{
    /// <summary>Deck most recently nudged by the jog wheel (1 or 2), or -1 if neither has been jogged yet.</summary>
    int LastJoggedDeck { get; }

    /// <summary>Time of the last jog tick on either deck, or <see cref="DateTime.MinValue"/>.</summary>
    DateTime LastJogAt { get; }

    /// <summary>Time of the last jog event on deck 1 specifically.</summary>
    DateTime LastJogAt1 { get; }

    /// <summary>Time of the last jog event on deck 2 specifically.</summary>
    DateTime LastJogAt2 { get; }

    /// <summary>How recently a jog event must have arrived for that deck to count as "actively being
    /// adjusted right now". The scrub flag and the both-decks check use the same window.</summary>
    TimeSpan ActiveJogWindow { get; }

    /// <summary>True when both decks were jogged within <see cref="ActiveJogWindow"/> (reads the clock afresh).</summary>
    bool BothDecksActivelyJogging { get; }
}
