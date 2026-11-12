using Sholto.App.Analysis.Harmony;
using Sholto.App.Storage.Backup;

namespace Sholto.App.Storage.Tests;

/// <summary>Pins the default library database path per OS: unchanged on Linux,
/// <c>%LOCALAPPDATA%\sholto\library.db</c> on Windows.</summary>
public class SholtoStorageDefaultPathTests
{
    private static SholtoStorage NewStorage() => new(
        new DatabaseStackFactory(new KeyFactory(), new RecordingLogFactory()),
        new SholtoDbContextOptionsFactory(),
        new LibraryBackup(),
        new RecordingLogFactory());

    [Fact]
    public void DefaultDbPath_is_local_share_on_linux_and_local_app_data_on_windows()
    {
        var expected = OperatingSystem.IsWindows()
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "sholto", "library.db")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share", "sholto", "library.db");

        Assert.Equal(expected, NewStorage().DefaultDbPath());
    }
}
