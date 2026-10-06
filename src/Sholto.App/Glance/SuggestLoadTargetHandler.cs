using Sholto.App.Decks;
using Sholto.Data;

namespace Sholto.App.Glance;

/// <summary>Answers <see cref="SuggestLoadTarget"/>: an empty deck first (deck 1, then deck 2); else the deck that is
/// not playing; when both play, the one with less real time left (duration minus position, divided by playback speed;
/// a tie is deck 1); when neither plays, deck 1.</summary>
public sealed class SuggestLoadTargetHandler(IDecks decks) : IQueryHandler<SuggestLoadTarget, int>
{
    private readonly IDecks _decks = decks;

    public int Handle(in SuggestLoadTarget query)
    {
        var first = _decks.DeckFor(0);
        var second = _decks.DeckFor(1);
        if (first.LoadedTrack is null) return 0;
        if (second.LoadedTrack is null) return 1;
        if (first.IsPlaying && !second.IsPlaying) return 1;
        if (second.IsPlaying && !first.IsPlaying) return 0;
        if (first.IsPlaying && second.IsPlaying)
            return TimeLeft(second) < TimeLeft(first) ? 1 : 0;
        return 0;
    }

    private double TimeLeft(IDeckSession deck) =>
        (deck.LoadedTrack!.Duration.TotalSeconds - deck.PlaybackSeconds) / Math.Max(deck.Tempo.PlaybackSpeed, 0.01);
}
