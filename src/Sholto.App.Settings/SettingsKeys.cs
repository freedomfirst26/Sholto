namespace Sholto.App.Settings;

/// <summary>Keys used in the <c>settings</c> table. Keep keys stable — renaming
/// one is effectively a schema migration (and needs one to copy the old row).</summary>
public sealed class SettingsKeys
{
    public const string MusicDir       = "music_dir";
    // Semantics as of the dual-sound-card master routing feature: this holds
    // the user's chosen MASTER SPEAKER (the "Choose Audio Output" picker
    // excludes the DDJ-FLX4 — it's auto-selected whenever present, for its
    // headphone cue bus + shared clock; master gets PipeWire-routed to this
    // sink). When no FLX4 is connected it's still just the device opened
    // directly, as before. Repurposed rather than adding a new key — the row
    // already round-trips through App.axaml.cs/AudioEngine and a rename would
    // just be a silent migration for existing installs.
    public const string OutputDevice   = "output_device";
    public const string Theme          = "theme";
    // The waveform style's stable id ("three-band", "rgb"); absent = the default style.
    public const string WaveformStyle  = "waveform_style";
    // The backspin coast time in seconds (0..3, invariant culture, e.g. "0.6"); absent = 0.6.
    public const string BackspinTimeSeconds = "backspin_time_seconds";
    // The backspin coast distance in beats (0..16, invariant culture, e.g. "2"); absent = 2.
    public const string BackspinDistanceBeats = "backspin_distance_beats";
    // The Glance shortlist: a JSON array of file paths in insertion order; absent = empty.
    public const string GlanceShortlist = "glance_shortlist";
    // Prefix of one key per one-time hint ("hint_shown:faceplate"): how many times it has been shown, as an integer.
    public const string HintShownPrefix = "hint_shown:";
}
