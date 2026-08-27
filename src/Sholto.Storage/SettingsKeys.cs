namespace Sholto.Storage;

/// <summary>Keys used in the <c>settings</c> table. Keep keys stable — renaming
/// one is effectively a schema migration (and needs one to copy the old row).</summary>
public static class SettingsKeys
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
}
