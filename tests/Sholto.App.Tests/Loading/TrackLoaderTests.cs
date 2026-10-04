using Sholto.App.Analysis.Harmony;
using Sholto.Data;
using Sholto.App.Decks;
using Sholto.App.Library;

namespace Sholto.App.Tests;

/// <summary>The one track loader: the LOAD buttons, the keyboard's 1 and 2 and the search overlay all send
/// <c>LoadSelectedIntoDeck</c>; the browse knob held and a library double-click both send
/// <c>ReanalyzeSelected</c>. This used to be two loaders, one on the main view model and one here.</summary>
public class TrackLoaderTests
{
    private static readonly Origin From = TrackLoaderRig.Origin;

    private static async Task Eventually(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException("The condition never became true.");
            await Task.Delay(10);
        }
    }

    // ---- Load -----------------------------------------------------------------------------------

    [Fact]
    public async Task Loading_shows_the_track_at_once_then_hands_the_decoded_samples_to_the_deck()
    {
        var rig = new TrackLoaderRig(new FakeAudioFileDecoder());
        rig.Highlight(LibrarySessionRig.Charlie);

        rig.Loader.Handle(new LoadSelectedIntoDeck(1, From));

        var deck = rig.Library.Deck2;
        Assert.Equal(LibrarySessionRig.Charlie, deck.LoadedTrack);   // BeginLoad ran on the caller
        await Eventually(() => deck.LoadState == DeckLoadState.Loaded);
        Assert.Equal(new[] { LibrarySessionRig.Charlie.FilePath }, rig.Decoder.Decoded);
        Assert.Equal(DeckLoadState.Idle, rig.Library.Deck1.LoadState);
    }

    [Fact]
    public async Task Loading_applies_the_tracks_saved_bpm_multiplier()
    {
        var rig = new TrackLoaderRig(new FakeAudioFileDecoder());
        rig.Library.Library.SeedKnownBpmMultipliers(
            new Dictionary<string, double> { [LibrarySessionRig.Bravo.FilePath] = 0.5 });
        rig.Highlight(LibrarySessionRig.Bravo);

        rig.Loader.Handle(new LoadSelectedIntoDeck(0, From));

        await Eventually(() => rig.Library.Deck1.LoadState == DeckLoadState.Loaded);
        Assert.Equal(0.5, rig.Library.Deck1.BpmMultiplier);
    }

    [Fact]
    public async Task A_loaded_track_is_marked_played_in_the_library()
    {
        var rig = new TrackLoaderRig(new FakeAudioFileDecoder());
        rig.Highlight(LibrarySessionRig.Alpha);

        rig.Loader.Handle(new LoadSelectedIntoDeck(0, From));

        await Eventually(() => rig.Library.Row(LibrarySessionRig.Alpha).IsPlayed);
    }

    [Fact]
    public async Task A_failed_decode_leaves_the_deck_failed_but_usable_for_the_next_load()
    {
        var failing = new FakeAudioFileDecoder(new IOException("corrupt file"));
        var rig = new TrackLoaderRig(failing);
        rig.Highlight(LibrarySessionRig.Alpha);

        rig.Loader.Handle(new LoadSelectedIntoDeck(0, From));

        await Eventually(() => rig.Library.Deck1.LoadState == DeckLoadState.Failed);
        Assert.False(rig.Library.Row(LibrarySessionRig.Alpha).IsPlayed);
    }

    [Fact]
    public void Loading_with_nothing_highlighted_does_nothing()
    {
        var rig = new TrackLoaderRig(new FakeAudioFileDecoder());

        rig.Loader.Handle(new LoadSelectedIntoDeck(0, From));

        Assert.Equal(DeckLoadState.Idle, rig.Library.Deck1.LoadState);
        Assert.Empty(rig.Decoder.Decoded);
    }

    // ---- Re-analysis ----------------------------------------------------------------------------

    [Fact]
    public async Task Reanalysis_updates_the_highlighted_tracks_bpm_and_key_and_saves_the_key()
    {
        var key = new Key(PitchClass: 7, IsMajor: true);
        var rig = new TrackLoaderRig(new FakeAudioFileDecoder(), new FakeAnalysisProvider(bpm: 126.5), key);
        rig.Highlight(LibrarySessionRig.Bravo);

        rig.Loader.Handle(new ReanalyzeSelected(From));

        await Eventually(() => rig.Library.Row(LibrarySessionRig.Bravo).Bpm == 126.5);
        Assert.Equal((KeyRef?)key.ToRef(), rig.Library.Row(LibrarySessionRig.Bravo).MusicalKey);
        Assert.Equal(LibrarySessionRig.Bravo.FilePath, Assert.Single(rig.KeyStore.Puts).Path);
        Assert.Null(rig.Library.Row(LibrarySessionRig.Alpha).Bpm);
    }

    [Fact]
    public async Task A_reanalysis_that_throws_shows_its_failure_on_the_track()
    {
        // The default test deck's analysis provider throws NotImplementedException.
        var rig = new TrackLoaderRig(new FakeAudioFileDecoder());
        rig.Highlight(LibrarySessionRig.Alpha);

        rig.Loader.Handle(new ReanalyzeSelected(From));

        await Eventually(() => rig.Library.Row(LibrarySessionRig.Alpha).AnalysisFailure is not null);
        Assert.StartsWith("NotImplementedException", rig.Library.Row(LibrarySessionRig.Alpha).AnalysisFailure);
    }

    [Fact]
    public void Reanalysis_with_nothing_highlighted_does_nothing()
    {
        var rig = new TrackLoaderRig(new FakeAudioFileDecoder(), new FakeAnalysisProvider(bpm: 120));

        rig.Loader.Handle(new ReanalyzeSelected(From));

        Assert.Empty(rig.Decoder.Decoded);
    }
}
