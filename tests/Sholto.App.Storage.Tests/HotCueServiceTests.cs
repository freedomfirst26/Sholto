using Microsoft.EntityFrameworkCore;
using Sholto.App.Storage;
using Sholto.App.Storage.Entities;
using Sholto.Data;

namespace Sholto.App.Storage.Tests;

public class HotCueServiceTests : IAsyncLifetime
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"sholto-hotcue-{Guid.NewGuid():N}.db");
    private IDbContextFactory<SholtoDbContext> _factory = null!;
    private HotCueService _service = null!;
    private Guid _trackA;
    private Guid _trackB;

    public async Task InitializeAsync()
    {
        _factory = await new ContextCapturingStorage().OpenAsync(_path);
        _service = new HotCueService(_factory);
        await using var db = _factory.CreateDbContext();
        var a = new Track { Path = "/a.flac", Title = "A", Artist = "A" };
        var b = new Track { Path = "/b.flac", Title = "B", Artist = "B" };
        db.Tracks.AddRange(a, b);
        await db.SaveChangesAsync();
        _trackA = a.Id;
        _trackB = b.Id;
    }

    public Task DisposeAsync()
    {
        if (File.Exists(_path)) File.Delete(_path);
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Set_then_list_returns_the_cue()
    {
        await _service.SetAsync(_trackA, new HotCue(3, 12.5, CueColour.Green));
        Assert.Equal([new HotCue(3, 12.5, CueColour.Green)], await _service.ListAsync(_trackA));
    }

    [Fact]
    public async Task Set_on_an_occupied_pad_replaces_it()
    {
        await _service.SetAsync(_trackA, new HotCue(3, 12.5, CueColour.Green));
        await _service.SetAsync(_trackA, new HotCue(3, 20, CueColour.Red));
        Assert.Equal([new HotCue(3, 20, CueColour.Red)], await _service.ListAsync(_trackA));
    }

    [Fact]
    public async Task Delete_removes_only_that_pad()
    {
        await _service.SetAsync(_trackA, new HotCue(1, 5, CueColour.Pink));
        await _service.SetAsync(_trackA, new HotCue(3, 12.5, CueColour.Green));
        await _service.DeleteAsync(_trackA, 3);
        Assert.Equal([new HotCue(1, 5, CueColour.Pink)], await _service.ListAsync(_trackA));
    }

    [Fact]
    public async Task Delete_of_an_absent_pad_does_nothing()
    {
        await _service.DeleteAsync(_trackA, 6);
        Assert.Empty(await _service.ListAsync(_trackA));
    }

    [Fact]
    public async Task Hot_cues_are_listed_in_pad_order()
    {
        await _service.SetAsync(_trackA, new HotCue(5, 1, CueColour.Blue));
        await _service.SetAsync(_trackA, new HotCue(2, 9, CueColour.Aqua));
        Assert.Equal([2, 5], (await _service.ListAsync(_trackA)).Select(c => c.Pad));
    }

    [Fact]
    public async Task Hot_cues_do_not_leak_across_tracks()
    {
        await _service.SetAsync(_trackA, new HotCue(3, 12.5, CueColour.Green));
        Assert.Empty(await _service.ListAsync(_trackB));
        await _service.DeleteAsync(_trackB, 3);
        Assert.Single(await _service.ListAsync(_trackA));
    }

    [Fact]
    public async Task Memory_markers_are_not_listed_as_hot_cues()
    {
        await new MarkerService(_factory).AddAsync(_trackA, 7.0);
        Assert.Empty(await _service.ListAsync(_trackA));
    }

    [Fact]
    public async Task Hot_cues_still_show_as_marker_positions()
    {
        await _service.SetAsync(_trackA, new HotCue(0, 7.0, CueColour.Pink));
        Assert.Equal([7.0], await new MarkerService(_factory).ListPositionsAsync(_trackA));
    }
}
