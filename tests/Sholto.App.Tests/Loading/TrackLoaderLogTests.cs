using Sholto.Data;

namespace Sholto.App.Tests;

/// <summary>The track loader writes its lines through its logs, with the tag and text the console has always shown.</summary>
public class TrackLoaderLogTests
{
    private static readonly Origin From = TrackLoaderRig.Origin;

    [Fact]
    public void A_reanalysis_logs_the_gesture_and_the_file_under_the_TrackLoader_tag()
    {
        var rig = new TrackLoaderRig(new FakeAudioFileDecoder(), new FakeAnalysisProvider(bpm: 120));
        rig.Highlight(LibrarySessionRig.Bravo);

        rig.Loader.Handle(new ReanalyzeSelected(From));

        Assert.Contains(
            new LoggedLine("TrackLoader", LogLevel.Info, $"load → re-analyzing {LibrarySessionRig.Bravo.FilePath}"),
            rig.Logs.Lines);
    }

    [Fact]
    public async Task A_load_that_cannot_decode_logs_an_error_under_the_Track_tag()
    {
        var rig = new TrackLoaderRig(new FakeAudioFileDecoder(new IOException("disk gone")));
        rig.Highlight(LibrarySessionRig.Alpha);

        rig.Loader.Handle(new LoadSelectedIntoDeck(0, From));

        var expected = new LoggedLine("Track", LogLevel.Error, "load into deck 1 FAILED: disk gone");
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!rig.Logs.Lines.Contains(expected))
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException($"Never logged; got: {string.Join(" | ", rig.Logs.Lines)}");
            await Task.Delay(10);
        }
    }
}
