using Sholto.App;
using Sholto.App.Audio;

namespace Sholto.Interface.Bench.Headless;

/// <summary>Reads the core's ports and the concrete decks (for the internal members the ports do not
/// expose: the loaded file path). Index 0 and 1 of <c>decks</c> are Deck1 and Deck2.</summary>
public sealed class CoreSnapshot : ICoreSnapshot
{
    public Dictionary<string, object?> Take(CoreStack core, IReadOnlyList<Deck> decks) => new()
    {
        ["Crossfader"] = core.Mixer.Crossfader,
        ["Deck1.IsLoaded"] = core.Decks.Deck1.Loading.IsLoaded,
        ["Deck1.IsPlaying"] = core.Decks.Deck1.Loading.IsPlaying,
        ["Deck1.FilePath"] = decks[0].CurrentFilePath,
        ["Deck1.PositionFrames"] = core.Decks.Deck1.Playhead.PositionFrames,
        ["Deck1.PlayPosition"] = core.Decks.Deck1.Playhead.PlayPosition,
        ["Deck1.Volume"] = core.Decks.Deck1.Mixer.Volume,
        ["Deck1.TempoPosition"] = core.Decks.Deck1.Tempo.TempoPosition,
        ["Deck1.CueActive"] = core.Decks.Deck1.Mixer.CueActive,
        ["Deck2.IsLoaded"] = core.Decks.Deck2.Loading.IsLoaded,
        ["Deck2.IsPlaying"] = core.Decks.Deck2.Loading.IsPlaying,
        ["Deck2.FilePath"] = decks[1].CurrentFilePath,
        ["Deck2.PositionFrames"] = core.Decks.Deck2.Playhead.PositionFrames,
        ["Deck2.PlayPosition"] = core.Decks.Deck2.Playhead.PlayPosition,
        ["Deck2.Volume"] = core.Decks.Deck2.Mixer.Volume,
        ["Deck2.TempoPosition"] = core.Decks.Deck2.Tempo.TempoPosition,
        ["Deck2.CueActive"] = core.Decks.Deck2.Mixer.CueActive,
    };
}
