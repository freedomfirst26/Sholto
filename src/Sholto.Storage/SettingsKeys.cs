namespace Sholto.Storage;

/// <summary>Keys used in the <c>settings</c> table. Keep keys stable — renaming
/// one is effectively a schema migration (and needs one to copy the old row).</summary>
public static class SettingsKeys
{
    public const string MusicDir       = "music_dir";
    public const string OutputDevice   = "output_device";
}
