using Microsoft.Extensions.Options;
using Sholto.Data;

namespace Sholto.App.Performance;

/// <summary>When each deck was last jogged, and which deck was jogged most recently. The one shared
/// instance: the jog paths write it, the scrub flag and the magnet read it. Every call reads the clock
/// afresh.</summary>
public sealed class JogRecency : IJogRecencyWriter, IJogRecencyReader
{
    private readonly IFrameClock _clock;

    public JogRecency(IFrameClock clock, IOptions<ScratchOptions> options)
    {
        _clock = clock;
        ActiveJogWindow = options.Value.ActiveJogWindow;
    }

    public int LastJoggedDeck { get; private set; } = -1;
    public DateTime LastJogAt { get; private set; } = DateTime.MinValue;
    public DateTime LastJogAt1 { get; private set; } = DateTime.MinValue;
    public DateTime LastJogAt2 { get; private set; } = DateTime.MinValue;
    public TimeSpan ActiveJogWindow { get; }

    public bool BothDecksActivelyJogging =>
        IsActivelyJogging(LastJogAt1) && IsActivelyJogging(LastJogAt2);

    private bool IsActivelyJogging(DateTime deckLastJog) =>
        deckLastJog != DateTime.MinValue
        && _clock.Now - deckLastJog < ActiveJogWindow;

    public void MarkJogged(int deck)
    {
        LastJoggedDeck = deck == 0 ? 1 : 2;
        var now = _clock.Now;
        LastJogAt = now;
        if (deck == 0) LastJogAt1 = now; else LastJogAt2 = now;
    }

    public void ClearAfterScratchEnd(bool isDeck1)
    {
        LastJogAt = DateTime.MinValue;
        if (isDeck1) LastJogAt1 = DateTime.MinValue; else LastJogAt2 = DateTime.MinValue;
    }
}
