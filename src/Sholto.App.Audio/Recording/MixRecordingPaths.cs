using System.Globalization;
using Microsoft.Extensions.Options;

namespace Sholto.App.Audio.Recording;

/// <summary>See <see cref="IMixRecordingPaths"/>. The folder is <see cref="MixRecordingOptions.Folder"/>, or else the
/// OS Music folder (or <c>~/Music</c> where the OS names none); it is created if missing. The file is named
/// <c>yyyy-MM-dd HH-mm-ss.wav</c>, with <c> (2)</c>, <c> (3)</c>, … added when that name is taken.</summary>
public sealed class MixRecordingPaths : IMixRecordingPaths
{
    private const string NameFormat = "yyyy-MM-dd HH-mm-ss";
    private const string Extension = ".wav";
    /// <summary>The first suffix a clashing name gets: the second file of that second.</summary>
    private const int FirstClashNumber = 2;
    /// <summary>The Music folder under the home folder, where the OS names no Music folder of its own.</summary>
    private const string MusicFolderName = "Music";

    private readonly string _folder;

    public MixRecordingPaths(IOptions<MixRecordingOptions> options)
    {
        var configured = options.Value.Folder;
        var music = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
        _folder = configured.Length > 0 ? configured
            : music.Length > 0 ? music
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), MusicFolderName);
    }

    public string Next(DateTime localNow)
    {
        Directory.CreateDirectory(_folder);
        var stem = localNow.ToString(NameFormat, CultureInfo.InvariantCulture);
        var path = Path.Combine(_folder, stem + Extension);
        for (int clash = FirstClashNumber; File.Exists(path); clash++)
            path = Path.Combine(_folder, $"{stem} ({clash}){Extension}");
        return path;
    }
}
