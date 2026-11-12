using Microsoft.Extensions.Options;
using Sholto.Data;

namespace Sholto.App.Lifecycle.Folders;

/// <inheritdoc />
public sealed class FolderCheck(IOptions<AnalysisFolderOptions> options) : IFolderCheck
{
    private readonly AnalysisFolderOptions _options = options.Value;

    public FolderCheckResult Check(string? music, string? analysis) =>
        new(MusicProblemFor(music), AnalysisProblemFor(music, analysis), SuggestFor(music));

    public string? SuggestFor(string? music)
    {
        if (string.IsNullOrWhiteSpace(music)) return null;
        var parent = Path.GetDirectoryName(Normalize(music));
        if (string.IsNullOrEmpty(parent) || parent == Path.GetPathRoot(parent)) return null;
        return IsWritable(parent) ? Path.Combine(parent, _options.FolderName) : null;
    }

    private string? MusicProblemFor(string? music)
    {
        if (string.IsNullOrWhiteSpace(music)) return null;
        return Directory.Exists(music) ? null : "That music folder does not exist.";
    }

    private string? AnalysisProblemFor(string? music, string? analysis)
    {
        if (string.IsNullOrWhiteSpace(analysis)) return null;
        var folder = Normalize(analysis);

        if (!string.IsNullOrWhiteSpace(music))
        {
            var musicFolder = Normalize(music);
            if (folder == musicFolder)
                return "The analysis folder cannot be the music folder.";
            if (IsInside(folder, musicFolder))
                return "The analysis folder cannot be inside the music folder: its stems would be imported as tracks.";
            if (IsInside(musicFolder, folder))
                return "The analysis folder cannot contain the music folder: its stems would be imported as tracks.";
        }

        if (Directory.Exists(folder))
            return IsWritable(folder) ? null : "Sholto cannot write to that folder.";

        var parent = Path.GetDirectoryName(folder);
        if (string.IsNullOrEmpty(parent) || !Directory.Exists(parent))
            return "That folder's parent does not exist (is the drive mounted?).";
        return IsWritable(parent) ? null : "Sholto cannot write there.";
    }

    public string Normalize(string path)
    {
        var full = Path.GetFullPath(path);
        var root = Path.GetPathRoot(full);
        return full == root ? full : full.TrimEnd(Path.DirectorySeparatorChar);
    }

    private bool IsInside(string path, string container) =>
        path.StartsWith(container.EndsWith(Path.DirectorySeparatorChar) ? container : container + Path.DirectorySeparatorChar,
            StringComparison.Ordinal);

    private bool IsWritable(string folder)
    {
        try
        {
            var probe = Path.Combine(folder, $".sholto-write-probe-{Guid.NewGuid():N}");
            using (File.Create(probe)) { }
            File.Delete(probe);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
