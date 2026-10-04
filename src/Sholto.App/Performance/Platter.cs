using Microsoft.Extensions.Options;
using Sholto.Data;
using Sholto.App.Decks;

namespace Sholto.App.Performance;

/// <summary>Routes platter commands: loop-lock check, then scratch or silent seek, then mark the deck as
/// jogged — all in one place. Runs at ~100 Hz on the app thread: no allocation, no LINQ, no closures.</summary>
public sealed class Platter : IPlatter
{
    private readonly IDecks _decks;
    private readonly IScratchEngine _scratch;
    private readonly IJogSeek _seek;
    private readonly IJogRecencyWriter _recency;
    private readonly ScratchOptions _options;

    public Platter(IDecks decks, IScratchEngine scratch, IJogSeek seek, IJogRecencyWriter recency,
                   IOptions<ScratchOptions> options)
    {
        _decks = decks;
        _scratch = scratch;
        _seek = seek;
        _recency = recency;
        _options = options.Value;
    }

    /// <summary>The platter's touch sensor, both edges. <see cref="TouchPlatter.Shifted"/> is the
    /// interface's Shift state: Shift + touch is the silent fast-search, not a grab.</summary>
    public void Handle(in TouchPlatter command) => _scratch.Touch(command.Deck, command.Touching, command.Shifted);

    /// <summary>The platter turned. Shift + top platter is the silent 2x seek through the track,
    /// bypassing the audible scratch. (The "4x" in the device mapping's comment is stale.)</summary>
    public void Handle(in TurnPlatter command)
    {
        var deck = command.Deck;
        // Loop locked: the jog wheel is ignored while a loop is active, else
        // scrubbing could pull the playhead outside the loop and break the wrap.
        var session = _decks.DeckFor(deck);
        if (session.Looping.ActiveLoop is not null) return;

        if (command.Shifted)
        {
            _seek.Add(deck, command.Delta * _options.TopPlatterSecsPerTick * 2);
            _recency.MarkJogged(deck);
            return;
        }

        // Top platter on a scratch-capable deck: route into the scratch
        // velocity accumulator instead of the silent-seek pipeline —
        // the tick turns this into an audible varispeed rate rather than a
        // Seek. Side ring always keeps the old nudge behaviour, and the
        // top platter falls back to it too on a deck that can't scratch
        // yet (still on the streaming/pre-decode provider).
        if (command.Surface == PlatterSurface.Top && session.Scratch.CanScratch)
        {
            _scratch.Turn(deck, command.Delta);
        }
        else
        {
            // Accumulate; the frame flushes it into one Seek per deck — each Seek
            // flushes SoundFlow's buffer, so per-event seeks (~100/s) would glitch.
            double secsPerTick = command.Surface == PlatterSurface.Top ? _options.TopPlatterSecsPerTick : _options.SideRingSecsPerTick;
            _seek.Add(deck, command.Delta * secsPerTick);
        }
        _recency.MarkJogged(deck);
    }
}
