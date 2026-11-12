using Microsoft.Extensions.Options;
using Sholto.App.Lifecycle.Folders;

namespace Sholto.App.Tests;

/// <summary>What makes an analysis folder acceptable beside a music folder: the library scan takes every
/// <c>.wav</c> under the music folder, so stems must never land in it, and the folder must be writable.</summary>
[System.Runtime.Versioning.SupportedOSPlatform("linux")]
public class FolderCheckTests : IDisposable
{
    private readonly List<string> _dirs = [];
    private readonly FolderCheck _check = new(Options.Create(new AnalysisFolderOptions()));

    private string NewDir()
    {
        var dir = Directory.CreateTempSubdirectory("sholto-check-").FullName;
        _dirs.Add(dir);
        return dir;
    }

    public void Dispose()
    {
        foreach (var dir in _dirs)
        {
            try
            {
                foreach (var sub in Directory.EnumerateDirectories(dir, "*", SearchOption.AllDirectories))
                    File.SetUnixFileMode(sub, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
                File.SetUnixFileMode(dir, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
                Directory.Delete(dir, recursive: true);
            }
            catch (IOException) { }
        }
    }

    [Fact]
    public void A_missing_music_folder_is_a_music_problem()
    {
        var result = _check.Check(Path.Combine(NewDir(), "gone"), null);

        Assert.NotNull(result.MusicProblem);
        Assert.Null(result.AnalysisProblem);
    }

    [Fact]
    public void An_analysis_folder_inside_the_music_folder_is_rejected()
    {
        var music = NewDir();
        var inside = Directory.CreateDirectory(Path.Combine(music, "stems")).FullName;

        var result = _check.Check(music, inside);

        Assert.Contains("inside the music folder", result.AnalysisProblem);
        Assert.Null(result.MusicProblem);
    }

    [Fact]
    public void An_analysis_folder_that_contains_the_music_folder_is_rejected()
    {
        var parent = NewDir();
        var music = Directory.CreateDirectory(Path.Combine(parent, "Music")).FullName;

        var result = _check.Check(music, parent);

        Assert.Contains("contain the music folder", result.AnalysisProblem);
    }

    [Fact]
    public void The_music_folder_itself_is_rejected_even_with_a_trailing_slash()
    {
        var music = NewDir();

        var result = _check.Check(music, music + "/");

        Assert.Contains("cannot be the music folder", result.AnalysisProblem);
    }

    [Fact]
    public void A_sibling_whose_name_starts_with_the_music_folders_name_is_not_inside_it()
    {
        var parent = NewDir();
        var music = Directory.CreateDirectory(Path.Combine(parent, "Music")).FullName;
        var sibling = Directory.CreateDirectory(Path.Combine(parent, "Music Analysis")).FullName;

        Assert.Null(_check.Check(music, sibling).AnalysisProblem);
    }

    [Fact]
    public void An_unwritable_analysis_folder_is_rejected()
    {
        var music = NewDir();
        var locked = Directory.CreateDirectory(Path.Combine(NewDir(), "locked")).FullName;
        File.SetUnixFileMode(locked, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        if (CanWriteInto(locked)) return;   // running as root: permissions do not bind

        var result = _check.Check(music, locked);

        Assert.Contains("cannot write", result.AnalysisProblem);
    }

    [Fact]
    public void A_missing_analysis_folder_whose_parent_is_missing_too_is_rejected()
    {
        var music = NewDir();

        var result = _check.Check(music, Path.Combine(NewDir(), "no-parent", "Sholto Analysis"));

        Assert.Contains("parent does not exist", result.AnalysisProblem);
    }

    [Fact]
    public void A_missing_analysis_folder_with_an_existing_parent_is_accepted_and_not_created()
    {
        var music = NewDir();
        var target = Path.Combine(NewDir(), "Sholto Analysis");

        var result = _check.Check(music, target);

        Assert.Null(result.AnalysisProblem);
        Assert.False(Directory.Exists(target));
    }

    [Fact]
    public void The_suggestion_is_a_folder_beside_the_music_folder()
    {
        var parent = NewDir();
        var music = Directory.CreateDirectory(Path.Combine(parent, "Music")).FullName;

        Assert.Equal(Path.Combine(parent, "Sholto Analysis"), _check.Check(music, null).SuggestedAnalysis);
    }

    [Fact]
    public void There_is_no_suggestion_without_a_music_folder_or_when_its_parent_is_the_root()
    {
        Assert.Null(_check.Check(null, null).SuggestedAnalysis);
        Assert.Null(_check.SuggestFor("/Music"));
    }

    private bool CanWriteInto(string dir)
    {
        try { File.Delete(Path.Combine(dir, "probe")); File.WriteAllText(Path.Combine(dir, "probe"), ""); File.Delete(Path.Combine(dir, "probe")); return true; }
        catch (UnauthorizedAccessException) { return false; }
    }
}
