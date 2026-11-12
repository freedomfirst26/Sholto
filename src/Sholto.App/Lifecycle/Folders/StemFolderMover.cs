using Microsoft.Extensions.Options;
using Sholto.App.Analysis.Stems;
using Sholto.Data;

namespace Sholto.App.Lifecycle.Folders;

/// <inheritdoc />
public sealed class StemFolderMover(IDirectoryRename rename, IOptions<AnalysisFolderOptions> options, ILog log) : IStemFolderMover
{
    private const string StemsDir = "stems";
    private const string PartialSuffix = ".partial";

    private readonly IDirectoryRename _rename = rename;
    private readonly AnalysisFolderOptions _options = options.Value;
    private readonly ILog _log = log;

    public Task<StemMoveOutcome> MoveAsync(string oldRoot, string newRoot, Action<int, int> progress, Func<Task> commit) =>
        Task.Run(() => Move(oldRoot, newRoot, progress, commit));

    private async Task<StemMoveOutcome> Move(string oldRoot, string newRoot, Action<int, int> progress, Func<Task> commit)
    {
        var source = Path.Combine(oldRoot, StemsDir);
        var target = Path.Combine(newRoot, StemsDir);
        var trackDirs = Directory.Exists(source) ? Directory.GetDirectories(source) : [];
        progress(0, trackDirs.Length);

        // Directories renamed so far, to put back on failure; source dirs whose copy is complete, to delete after the commit.
        var renamed = new List<(string From, string To)>();
        var copied = new List<string>();
        string? partial = null;
        try
        {
            Directory.CreateDirectory(newRoot);

            // Same filesystem and nothing there yet: the whole folder in one atomic step.
            if (trackDirs.Length > 0 && !Directory.Exists(target) && _rename.TryRename(source, target))
            {
                renamed.Add((source, target));
                progress(trackDirs.Length, trackDirs.Length);
            }
            else
            {
                Directory.CreateDirectory(target);
                var freeSpaceChecked = false;
                var crossDevice = false;
                for (var i = 0; i < trackDirs.Length; i++)
                {
                    var dir = trackDirs[i];
                    var place = Path.Combine(target, Path.GetFileName(dir));
                    if (IsComplete(place))
                    {
                        copied.Add(dir);   // already there in full: nothing to move, the source is surplus
                    }
                    else
                    {
                        if (Directory.Exists(place)) Directory.Delete(place, recursive: true);   // an unusable leftover
                        if (!crossDevice && _rename.TryRename(dir, place))
                            renamed.Add((dir, place));
                        else
                        {
                            crossDevice = true;
                            if (!freeSpaceChecked)
                            {
                                EnsureFreeSpace(trackDirs.Skip(i), newRoot);
                                freeSpaceChecked = true;
                            }
                            partial = place + PartialSuffix;
                            if (Directory.Exists(partial)) Directory.Delete(partial, recursive: true);
                            CopyVerified(dir, partial);
                            if (!_rename.TryRename(partial, place))
                                throw new IOException($"Could not put {place} in place.");
                            partial = null;
                            copied.Add(dir);
                        }
                    }
                    progress(i + 1, trackDirs.Length);
                }
            }

            await commit();
        }
        catch (Exception ex)
        {
            _log.Error($"failed, staying on {oldRoot}: {ex.Message}");
            Undo(renamed, partial);
            return new StemMoveOutcome(false, $"The stems could not be moved: {ex.Message}");
        }

        foreach (var dir in copied)
        {
            try { Directory.Delete(dir, recursive: true); }
            catch (Exception ex) { _log.Error($"could not delete the old copy {dir}: {ex.Message}"); }
        }
        return new StemMoveOutcome(true, null);
    }

    private bool IsComplete(string trackDir) =>
        Directory.Exists(trackDir) && new StemPaths(trackDir).All.All(File.Exists);

    private void EnsureFreeSpace(IEnumerable<string> dirs, string newRoot)
    {
        var needed = dirs.Sum(SizeOf) + _options.FreeSpaceMargin;
        var free = new DriveInfo(Path.GetFullPath(newRoot)).AvailableFreeSpace;
        if (free < needed)
            throw new IOException($"Not enough free space: {needed / (1024 * 1024)} MB needed, {free / (1024 * 1024)} MB free.");
    }

    private long SizeOf(string dir) =>
        new DirectoryInfo(dir).EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length);

    private void CopyVerified(string from, string to)
    {
        foreach (var file in new DirectoryInfo(from).EnumerateFiles("*", SearchOption.AllDirectories))
        {
            var copy = Path.Combine(to, Path.GetRelativePath(from, file.FullName));
            Directory.CreateDirectory(Path.GetDirectoryName(copy)!);
            File.Copy(file.FullName, copy);
            if (new FileInfo(copy).Length != file.Length)
                throw new IOException($"The copy of {file.Name} is the wrong size.");
        }
        if (SizeOf(from) != SizeOf(to))
            throw new IOException($"The copy of {from} does not match.");
    }

    private void Undo(List<(string From, string To)> renamed, string? partial)
    {
        if (partial is not null && Directory.Exists(partial))
        {
            try { Directory.Delete(partial, recursive: true); }
            catch (Exception ex) { _log.Error($"could not delete {partial}: {ex.Message}"); }
        }
        for (var i = renamed.Count - 1; i >= 0; i--)
            if (!_rename.TryRename(renamed[i].To, renamed[i].From))
                _log.Error($"could not put {renamed[i].To} back at {renamed[i].From}");
    }
}
