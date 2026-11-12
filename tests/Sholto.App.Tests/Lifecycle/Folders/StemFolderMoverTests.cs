using Microsoft.Extensions.Options;
using Sholto.App.Lifecycle.Folders;

namespace Sholto.App.Tests;

/// <summary>Moving the stems to a new analysis folder: a rename when it can be, copy-verify-delete across
/// drives, and never losing the source before the commit.</summary>
[System.Runtime.Versioning.SupportedOSPlatform("linux")]
public class StemFolderMoverTests : IDisposable
{
    private readonly List<string> _dirs = [];
    private readonly StemTreeBuilder _stems = new();
    private readonly List<(int Done, int Total)> _progress = [];

    private string NewDir()
    {
        var dir = Directory.CreateTempSubdirectory("sholto-mover-").FullName;
        _dirs.Add(dir);
        return dir;
    }

    public void Dispose()
    {
        foreach (var dir in _dirs)
        {
            try
            {
                foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
                    File.SetUnixFileMode(file, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                Directory.Delete(dir, recursive: true);
            }
            catch (IOException) { }
        }
    }

    private StemFolderMover MoverOver(IDirectoryRename rename, long freeSpaceMargin = 0) =>
        new(rename, Options.Create(new AnalysisFolderOptions { FreeSpaceMargin = freeSpaceMargin }));

    private Task<StemMoveOutcome> MoveAsync(StemFolderMover mover, string from, string to, Func<Task>? commit = null) =>
        mover.MoveAsync(from, to, (done, total) => _progress.Add((done, total)), commit ?? (() => Task.CompletedTask));

    [Fact]
    public async Task On_one_filesystem_with_no_target_the_whole_stems_folder_is_renamed()
    {
        var from = NewDir();
        var to = NewDir();
        _stems.Add(from, "aaaa");
        _stems.Add(from, "bbbb");
        var rename = new RefusingDirectoryRename(allow: (_, _) => true);

        var outcome = await MoveAsync(MoverOver(rename), from, to);

        Assert.True(outcome.Succeeded);
        Assert.False(Directory.Exists(Path.Combine(from, "stems")));
        Assert.True(File.Exists(Path.Combine(to, "stems", "aaaa", "htdemucs", "vocals.wav")));
        Assert.True(File.Exists(Path.Combine(to, "stems", "bbbb", "htdemucs", "other.wav")));
        Assert.Equal((2, 2), _progress[^1]);
    }

    [Fact]
    public async Task Across_drives_the_source_is_kept_until_the_commit_then_deleted()
    {
        var from = NewDir();
        var to = NewDir();
        var one = _stems.Add(from, "aaaa");
        var two = _stems.Add(from, "bbbb");
        var sourceThere = false;
        var copyThere = false;

        var outcome = await MoveAsync(MoverOver(new RefusingDirectoryRename()), from, to, () =>
        {
            sourceThere = Directory.Exists(one) && Directory.Exists(two);
            copyThere = File.Exists(Path.Combine(to, "stems", "bbbb", "htdemucs", "drums.wav"));
            return Task.CompletedTask;
        });

        Assert.True(outcome.Succeeded);
        Assert.True(sourceThere);
        Assert.True(copyThere);
        Assert.False(Directory.Exists(one));
        Assert.False(Directory.Exists(two));
        Assert.Equal(100, new FileInfo(Path.Combine(to, "stems", "aaaa", "htdemucs", "bass.wav")).Length);
        Assert.Equal([(0, 2), (1, 2), (2, 2)], _progress);
    }

    [Fact]
    public async Task A_mid_copy_failure_cleans_up_the_partial_and_leaves_the_source_whole()
    {
        var from = NewDir();
        var to = NewDir();
        var first = _stems.Add(from, "aaaa");
        var second = _stems.Add(from, "bbbb");
        var unreadable = Path.Combine(second, "htdemucs", "other.wav");
        File.SetUnixFileMode(unreadable, UnixFileMode.None);
        if (CanRead(unreadable)) return;   // running as root: permissions do not bind
        var committed = false;

        var outcome = await MoveAsync(MoverOver(new RefusingDirectoryRename()), from, to, () =>
        {
            committed = true;
            return Task.CompletedTask;
        });

        Assert.False(outcome.Succeeded);
        Assert.NotNull(outcome.Problem);
        Assert.False(committed);
        Assert.True(Directory.Exists(first));
        Assert.True(Directory.Exists(second));
        Assert.Empty(Directory.GetDirectories(Path.Combine(to, "stems"), "*.partial"));
        Assert.False(Directory.Exists(Path.Combine(to, "stems", "bbbb")));
    }

    [Fact]
    public async Task A_failing_commit_puts_renamed_folders_back_and_fails_the_move()
    {
        var from = NewDir();
        var to = NewDir();
        var one = _stems.Add(from, "aaaa");
        var rename = new RefusingDirectoryRename(allow: (_, _) => true);

        var outcome = await MoveAsync(MoverOver(rename), from, to, () => throw new IOException("settings write failed"));

        Assert.False(outcome.Succeeded);
        Assert.Contains("settings write failed", outcome.Problem);
        Assert.True(Directory.Exists(one));
        Assert.False(Directory.Exists(Path.Combine(to, "stems", "aaaa")));
    }

    [Fact]
    public async Task A_track_folder_already_complete_in_the_target_is_skipped_and_its_source_dropped()
    {
        var from = NewDir();
        var to = NewDir();
        var source = _stems.Add(from, "aaaa", bytes: 100);
        _stems.Add(to, "aaaa", bytes: 7);   // already there: must not be overwritten
        var rename = new RefusingDirectoryRename();

        var outcome = await MoveAsync(MoverOver(rename), from, to);

        Assert.True(outcome.Succeeded);
        Assert.Equal(7, new FileInfo(Path.Combine(to, "stems", "aaaa", "htdemucs", "vocals.wav")).Length);
        Assert.False(Directory.Exists(source));
        Assert.Equal(0, rename.Refused);   // not even tried
    }

    [Fact]
    public async Task Short_free_space_fails_before_any_copy()
    {
        var from = NewDir();
        var to = NewDir();
        var source = _stems.Add(from, "aaaa");
        var mover = MoverOver(new RefusingDirectoryRename(), freeSpaceMargin: long.MaxValue / 2);

        var outcome = await MoveAsync(mover, from, to);

        Assert.False(outcome.Succeeded);
        Assert.Contains("free space", outcome.Problem);
        Assert.True(Directory.Exists(source));
        Assert.Empty(Directory.GetFileSystemEntries(Path.Combine(to, "stems")));
    }

    [Fact]
    public async Task An_incomplete_leftover_in_the_target_is_replaced_by_the_real_folder()
    {
        var from = NewDir();
        var to = NewDir();
        _stems.Add(from, "aaaa");
        var leftover = Path.Combine(to, "stems", "aaaa", "htdemucs");
        Directory.CreateDirectory(leftover);
        File.WriteAllBytes(Path.Combine(leftover, "vocals.wav"), new byte[3]);

        var outcome = await MoveAsync(MoverOver(new RefusingDirectoryRename()), from, to);

        Assert.True(outcome.Succeeded);
        Assert.Equal(100, new FileInfo(Path.Combine(leftover, "vocals.wav")).Length);
        Assert.True(File.Exists(Path.Combine(leftover, "other.wav")));
    }

    [Fact]
    public void Directory_Move_between_two_filesystems_throws_IOException()
    {
        // The claim the mover rests on: on Linux Directory.Move is rename(2), so EXDEV is an IOException
        // (unlike File.Move, which copies). Needs a second filesystem: /dev/shm is tmpfs, the temp folder
        // is normally the disk. When both are the same filesystem there is nothing to prove, so it passes.
        const string shm = "/dev/shm";
        if (!OperatingSystem.IsLinux() || !Directory.Exists(shm)) return;
        var tempRoot = Path.GetFullPath(Path.GetTempPath());
        if (new DriveInfo(shm).Name == new DriveInfo(tempRoot).Name) return;

        var source = Directory.CreateDirectory(Path.Combine(shm, "sholto-exdev-src-" + Guid.NewGuid().ToString("N"))).FullName;
        var targetParent = NewDir();
        try
        {
            File.WriteAllText(Path.Combine(source, "f.txt"), "x");

            Assert.Throws<IOException>(() => Directory.Move(source, Path.Combine(targetParent, "moved")));

            Assert.False(new DirectoryRename().TryRename(source, Path.Combine(targetParent, "moved")));
            Assert.True(File.Exists(Path.Combine(source, "f.txt")));
        }
        finally
        {
            Directory.Delete(source, recursive: true);
        }
    }

    private bool CanRead(string file)
    {
        try { File.OpenRead(file).Dispose(); return true; }
        catch (UnauthorizedAccessException) { return false; }
    }
}
