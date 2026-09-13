using Sholto.Audio;

namespace Sholto.Bench.State;

/// <summary>
/// JSON snapshot of what one <see cref="Deck"/> believes right now, read
/// directly off the real object's public (and, for stems/track path,
/// internal — see Deck.cs) members. No parallel model: if a field isn't on
/// Deck, it isn't in this snapshot.
/// </summary>
public sealed class DeckStateSnapshot
{
    public string? FilePath { get; init; }
    public bool IsLoaded { get; init; }
    public bool IsPlaying { get; init; }
    public long PositionFrames { get; init; }
    public double PlayPosition { get; init; }
    public float Volume { get; init; }
    public float MasterGain { get; init; }
    public bool CueActive { get; init; }
    public double TempoPosition { get; init; }
    public float PlaybackSpeed { get; init; }
    public bool StemsLoaded { get; init; }

    public static DeckStateSnapshot From(Deck deck) => new()
    {
        FilePath = deck.CurrentFilePath,
        IsLoaded = deck.IsLoaded,
        IsPlaying = deck.IsPlaying,
        PositionFrames = deck.PositionFrames,
        PlayPosition = deck.PlayPosition,
        Volume = deck.Volume,
        MasterGain = deck.MasterGain,
        CueActive = deck.CueActive,
        TempoPosition = deck.TempoPosition,
        PlaybackSpeed = deck.PlaybackSpeed,
        StemsLoaded = deck.StemsLoaded,
    };
}
