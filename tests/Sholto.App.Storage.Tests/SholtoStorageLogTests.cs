using Sholto.App.Analysis.Harmony;
using Sholto.App.Storage;
using Sholto.App.Storage.Backup;
using Sholto.Data;

namespace Sholto.App.Storage.Tests;

public class SholtoStorageLogTests
{
    [Fact]
    public async Task OpenAsync_logs_opened_path_under_Storage_tag_at_Info()
    {
        using var temp = new TempSqliteDatabase();
        var logs = new RecordingLogFactory();
        var storage = new SholtoStorage(
            new DatabaseStackFactory(new KeyFactory(), logs), new SholtoDbContextOptionsFactory(), new LibraryBackup(), logs);

        await storage.OpenAsync(temp.Path);

        var line = Assert.Single(logs.Lines);
        Assert.Equal(new LoggedLine("Storage", LogLevel.Info, $"opened {temp.Path}"), line);
    }
}
