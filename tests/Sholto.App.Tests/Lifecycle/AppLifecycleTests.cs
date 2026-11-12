using Sholto.App.Lifecycle;
using Sholto.App.Audio;
using Sholto.App.Settings;
using Sholto.Data;

namespace Sholto.App.Tests;

/// <summary>The startup sequence on its own, with no window and no Avalonia: which questions the App asks
/// and when, what it persists, and what it does when the library database is there or is not. The numbered
/// properties are the ones the old App.InitializeServices kept (P1-P8, task C38).</summary>
public class AppLifecycleTests
{
    private static string NewMusicDir() => Directory.CreateTempSubdirectory("sholto-music-").FullName;

    /// <summary>The sequence runs on the thread pool, so a test waits for its effect.</summary>
    private static async Task Eventually(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException("The condition never became true.");
            await Task.Delay(10);
        }
    }

    // ---- Tool check -----------------------------------------------------------------------------

    [Fact]
    public async Task Starting_announces_the_boot_time_tool_check()
    {
        var check = new SystemCheck([new ToolPresence(ExternalToolNames.Madmom, ToolCapabilities.Beats, true, null)]);
        var rig = new AppLifecycleRig(systemCheck: check);
        var reported = new RecordingHandler<SystemCheckReported>();
        rig.Library.Bus.Subscribe(reported);

        await rig.OnAppThreadAsync(rig.Lifecycle.Start);

        var announced = Assert.Single(reported.Received);
        Assert.Equal(SystemHealth.Offline, announced.Health);
        Assert.Equal(check.ToReported().Tools, announced.Tools);
    }

    // ---- Database services ----------------------------------------------------------------------

    [Fact]
    public async Task With_the_database_the_tag_and_crate_services_markers_and_multiplier_store_are_attached()
    {
        var rig = new AppLifecycleRig(musicDirOverride: NewMusicDir());
        var attached = 0;
        rig.Library.Library.ServicesAttached += (_, _) => attached++;

        rig.Lifecycle.Start();
        await Eventually(() => rig.Library.Library.Rows.Count == 3);

        Assert.Equal(1, attached);
        var alpha = LibrarySessionRig.Alpha;
        await rig.OnAppThreadAsync(() => rig.Library.Deck1.LoadTrack(alpha, alpha.FilePath, [], bpmMultiplier: 1.0));
        await rig.OnAppThreadAsync(() => rig.Library.Deck1.HalveBpm());
        Assert.Equal(new[] { (alpha.FilePath, 0.5) }, rig.Library.Multipliers.Puts);
        var marked = new List<(int Deck, double Secs)>();
        rig.DeckMarkers.MarkerAdded += (deck, secs) => marked.Add((deck, secs));
        await rig.DeckMarkers.AddAsync(0);
        Assert.Single(marked);
    }

    [Fact]
    public async Task Without_the_database_none_of_that_is_attached()
    {
        var rig = new AppLifecycleRig(databaseAvailable: false, musicDirOverride: NewMusicDir());
        var attached = 0;
        rig.Library.Library.ServicesAttached += (_, _) => attached++;

        rig.Lifecycle.Start();
        await rig.Database.Opened;
        await Eventually(() => rig.Library.Library.Rows.Count == 3);

        Assert.Equal(0, attached);
        var alpha = LibrarySessionRig.Alpha;
        await rig.OnAppThreadAsync(() => rig.Library.Deck1.LoadTrack(alpha, alpha.FilePath, [], bpmMultiplier: 1.0));
        await rig.OnAppThreadAsync(() => rig.Library.Deck1.HalveBpm());
        Assert.Empty(rig.Library.Multipliers.Puts);
        Assert.Equal(0.5, rig.Library.Library.GetBpmMultiplierFor(alpha.FilePath));
        var marked = 0;
        rig.DeckMarkers.MarkerAdded += (_, _) => marked++;
        await rig.DeckMarkers.AddAsync(0);
        Assert.Equal(0, marked);
    }

    [Fact]
    public void Stopping_stops_the_audio_engine()
    {
        var rig = new AppLifecycleRig();

        rig.Lifecycle.Stop();

        Assert.True(rig.Audio.Stopped);
    }
}
