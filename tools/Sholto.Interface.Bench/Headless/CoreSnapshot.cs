using Sholto.App;
using Sholto.App.Audio;
using Sholto.App.Decks;

namespace Sholto.Interface.Bench.Headless;

/// <summary>Reads the core's ports and the concrete decks (for the internal members the ports do not
/// expose: the loaded file path). Index 0 and 1 of <c>decks</c> are Deck1 and Deck2. Every per-deck key
/// is reported for both decks as <c>Deck1.*</c> and <c>Deck2.*</c>.</summary>
public sealed class CoreSnapshot : ICoreSnapshot
{
    public Dictionary<string, object?> Take(CoreStack core, IReadOnlyList<Deck> decks)
    {
        var snap = new Dictionary<string, object?> { ["Crossfader"] = core.Mixer.Crossfader };
        AddDeck(snap, "Deck1", core.Decks.Deck1, decks[0]);
        AddDeck(snap, "Deck2", core.Decks.Deck2, decks[1]);
        snap["Library.SelectedIndex"] = core.Library.SelectedIndex;
        snap["Library.Rows"] = core.Library.Rows.Count;
        return snap;
    }

    private void AddDeck(Dictionary<string, object?> snap, string prefix, IDeckSession session, Deck deck)
    {
        snap[$"{prefix}.IsLoaded"] = session.Loading.IsLoaded;
        snap[$"{prefix}.IsPlaying"] = session.Loading.IsPlaying;
        snap[$"{prefix}.FilePath"] = deck.CurrentFilePath;
        snap[$"{prefix}.PositionFrames"] = session.Playhead.PositionFrames;
        snap[$"{prefix}.PlayPosition"] = session.Playhead.PlayPosition;
        snap[$"{prefix}.Volume"] = session.Mixer.Volume;
        snap[$"{prefix}.TempoPosition"] = session.Tempo.TempoPosition;
        snap[$"{prefix}.CueActive"] = session.Mixer.CueActive;
        snap[$"{prefix}.EchoActive"] = session.EchoActive;
        snap[$"{prefix}.Loop"] = session.Looping.ActiveLoop;
        snap[$"{prefix}.TempoRange"] = session.Tempo.TempoRange;
        snap[$"{prefix}.PlayState"] = session.PlayState;
        snap[$"{prefix}.EffectiveBpm"] = session.EffectiveBpm;
        snap[$"{prefix}.BpmMultiplier"] = session.BpmMultiplier;
        snap[$"{prefix}.MarkerSecsCount"] = session.MarkerSecs.Length;
        snap[$"{prefix}.SectionsCount"] = session.Sections.Count;
        snap[$"{prefix}.DrumsActive"] = session.DrumsActive;
        snap[$"{prefix}.VocalsActive"] = session.VocalsActive;
        snap[$"{prefix}.InstrumentalActive"] = session.InstrumentalActive;
        snap[$"{prefix}.DrumsLevel"] = session.DrumsLevel;
        snap[$"{prefix}.VocalsLevel"] = session.VocalsLevel;
        snap[$"{prefix}.InstrumentalLevel"] = session.InstrumentalLevel;
        snap[$"{prefix}.EditOpen"] = session.EditOpen;
        snap[$"{prefix}.IsScratching"] = session.IsScratching;
    }
}
