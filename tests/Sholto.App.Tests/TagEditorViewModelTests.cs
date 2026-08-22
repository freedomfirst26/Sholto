using Microsoft.EntityFrameworkCore;
using Sholto.App.ViewModels;
using Sholto.Storage;
using Sholto.Storage.Entities;

namespace Sholto.App.Tests;

public class TagEditorViewModelTests
{
    private static async Task<(TagService svc, Guid trackId, string dbPath)> NewAsync()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"sholto-tageditor-{Guid.NewGuid():N}.db");
        var factory = await SholtoStorage.OpenAsync(dbPath);
        var trackId = Guid.NewGuid();
        await using (var db = factory.CreateDbContext())
        {
            db.Tracks.Add(new Track { Id = trackId, Path = "/m/a.flac", Title = "Title", Artist = "Artist" });
            await db.SaveChangesAsync();
        }
        return (new TagService(factory), trackId, dbPath);
    }

    [Fact]
    public async Task Tab_commits_input_and_keeps_editor_open()
    {
        var (svc, trackId, dbPath) = await NewAsync();
        try
        {
            var vm = new TagEditorViewModel(svc);
            int closeCount = 0;
            vm.RequestClose += () => closeCount++;
            await vm.OpenForAsync(trackId, "Artist", "Title");

            vm.Input = "deep house";
            await vm.CommitAsync();

            Assert.Equal(new[] { "deep house" }, vm.Chips);
            Assert.Equal("", vm.Input);
            Assert.Equal(0, closeCount);
        }
        finally { if (File.Exists(dbPath)) File.Delete(dbPath); }
    }

    [Fact]
    public async Task Enter_commits_and_closes()
    {
        var (svc, trackId, dbPath) = await NewAsync();
        try
        {
            var vm = new TagEditorViewModel(svc);
            int closeCount = 0;
            vm.RequestClose += () => closeCount++;
            await vm.OpenForAsync(trackId, "Artist", "Title");

            vm.Input = "techno";
            await vm.CommitAndCloseAsync();

            Assert.Equal(new[] { "techno" }, vm.Chips);
            Assert.Equal(1, closeCount);
        }
        finally { if (File.Exists(dbPath)) File.Delete(dbPath); }
    }

    [Fact]
    public async Task RemoveLastChip_pops_most_recent()
    {
        var (svc, trackId, dbPath) = await NewAsync();
        try
        {
            var vm = new TagEditorViewModel(svc);
            await vm.OpenForAsync(trackId, "Artist", "Title");
            vm.Input = "a"; await vm.CommitAsync();
            vm.Input = "b"; await vm.CommitAsync();

            await vm.RemoveLastChipAsync();
            Assert.Equal(new[] { "a" }, vm.Chips);
        }
        finally { if (File.Exists(dbPath)) File.Delete(dbPath); }
    }

    [Fact]
    public async Task Close_fires_RequestClose_without_committing()
    {
        var (svc, trackId, dbPath) = await NewAsync();
        try
        {
            var vm = new TagEditorViewModel(svc);
            int closeCount = 0;
            vm.RequestClose += () => closeCount++;
            await vm.OpenForAsync(trackId, "Artist", "Title");

            vm.Input = "uncommitted";
            vm.Close();

            Assert.Equal(1, closeCount);
            Assert.Empty(vm.Chips);
        }
        finally { if (File.Exists(dbPath)) File.Delete(dbPath); }
    }

    [Fact]
    public async Task Empty_input_commit_is_silent_noop()
    {
        var (svc, trackId, dbPath) = await NewAsync();
        try
        {
            var vm = new TagEditorViewModel(svc);
            await vm.OpenForAsync(trackId, "Artist", "Title");
            vm.Input = "   ";
            await vm.CommitAsync();
            Assert.Empty(vm.Chips);
            Assert.Null(vm.StatusMessage);
        }
        finally { if (File.Exists(dbPath)) File.Delete(dbPath); }
    }

    [Fact]
    public async Task TooLong_input_surfaces_status_message()
    {
        var (svc, trackId, dbPath) = await NewAsync();
        try
        {
            var vm = new TagEditorViewModel(svc);
            await vm.OpenForAsync(trackId, "Artist", "Title");
            vm.Input = new string('x', 200);
            await vm.CommitAsync();
            Assert.Empty(vm.Chips);
            Assert.NotNull(vm.StatusMessage);
            Assert.Contains("too long", vm.StatusMessage!, StringComparison.OrdinalIgnoreCase);
        }
        finally { if (File.Exists(dbPath)) File.Delete(dbPath); }
    }

    [Fact]
    public async Task AlreadyPresent_input_surfaces_status_message()
    {
        var (svc, trackId, dbPath) = await NewAsync();
        try
        {
            var vm = new TagEditorViewModel(svc);
            await vm.OpenForAsync(trackId, "Artist", "Title");
            vm.Input = "deep house"; await vm.CommitAsync();
            vm.Input = "Deep House"; await vm.CommitAsync();
            Assert.Single(vm.Chips);
            Assert.NotNull(vm.StatusMessage);
            Assert.Contains("already tagged", vm.StatusMessage!, StringComparison.OrdinalIgnoreCase);
        }
        finally { if (File.Exists(dbPath)) File.Delete(dbPath); }
    }
}
