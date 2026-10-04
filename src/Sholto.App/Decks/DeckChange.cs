namespace Sholto.App.Decks;

/// <summary>What changed on a <see cref="IDeckSession"/>. A plain enum, so raising it allocates nothing
/// even every frame; <see cref="DeckEventPublisher"/> maps each value to the bus events (and so the display
/// properties) that depend on it.</summary>
public enum DeckChange
{
    /// <summary>The loaded track reference changed.</summary>
    LoadedTrack,
    /// <summary>Load lifecycle state changed (also moves CanPlay).</summary>
    LoadState,
    /// <summary>Whether the deck holds a loaded track changed.</summary>
    IsLoaded,
    IsPlaying,
    /// <summary>Transport phase changed (Stopped / Playing / Ending).</summary>
    PlayState,
    /// <summary>The end-of-track flash toggled (driven from the frame clock while Ending).</summary>
    EndFlash,
    /// <summary>Play position moved; raised once per frame by the tick.</summary>
    PlayPosition,
    /// <summary>A fresh track analysis replaced the old one (new load or unload): everything derived from
    /// analysis is stale.</summary>
    AnalysisReset,
    /// <summary>Basic analysis (BPM, beats, peaks) landed.</summary>
    BasicReady,
    KeyReady,
    StemsReady,
    VocalRegionsReady,
    Segments,
    Markers,
    BpmMultiplier,
    /// <summary>The tempo fader position moved.</summary>
    Tempo,
    TempoRange,
    MagnetAdjusted,
    Loop,
    GridNudged,
    GridEdit,
    /// <summary>The tune editor opened or closed.</summary>
    EditOpen,
    Cue,
    Echo,
    DrumsActive,
    VocalsActive,
    InstrumentalActive,
    DrumsLevel,
    VocalsLevel,
    InstrumentalLevel,
    Scrubbing,
    Scratching,
    MagneticGlow,
    /// <summary>The channel fader value changed.</summary>
    ChannelGain,
    /// <summary>The combined output gain changed (channel fader or crossfader).</summary>
    Volume,
}
