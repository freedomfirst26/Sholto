using Microsoft.Extensions.Options;
using Sholto.App.Audio.Recording;

namespace Sholto.App.Audio.Tests.Recording;

public sealed class MixRecordingPathsTests : IDisposable
{
    private readonly TempFolder _folder = new();
    private readonly DateTime _start = new(2026, 11, 12, 21, 4, 33);

    public void Dispose() => _folder.Dispose();

    private MixRecordingPaths Paths() => new(Options.Create(new MixRecordingOptions { Folder = _folder.Path }));

    [Fact]
    public void Names_the_file_from_the_local_start_time_in_the_configured_folder()
    {
        var path = Paths().Next(_start);

        Assert.Equal(_folder.Path, Path.GetDirectoryName(path));
        Assert.EndsWith("2026-11-12 21-04-33.wav", path);
    }

    [Fact]
    public void A_taken_name_gets_a_number_and_nothing_is_overwritten()
    {
        var paths = Paths();
        File.WriteAllText(paths.Next(_start), "first");

        var second = paths.Next(_start);
        File.WriteAllText(second, "second");
        var third = paths.Next(_start);

        Assert.EndsWith("2026-11-12 21-04-33 (2).wav", second);
        Assert.EndsWith("2026-11-12 21-04-33 (3).wav", third);
        Assert.Equal("first", File.ReadAllText(Path.Combine(_folder.Path, "2026-11-12 21-04-33.wav")));
    }

    [Fact]
    public void A_missing_folder_is_created()
    {
        Assert.False(Directory.Exists(_folder.Path));

        Paths().Next(_start);

        Assert.True(Directory.Exists(_folder.Path));
    }

    [Fact]
    public void An_empty_folder_option_means_the_OS_Music_folder()
    {
        var music = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
        var expected = music.Length > 0 ? music
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Music");

        var path = new MixRecordingPaths(Options.Create(new MixRecordingOptions())).Next(_start);

        Assert.Equal(expected, Path.GetDirectoryName(path));
    }
}
