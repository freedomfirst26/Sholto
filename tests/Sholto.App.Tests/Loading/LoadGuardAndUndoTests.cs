using Sholto.App.Decks;
using Sholto.App.Library;
using Sholto.Data;

namespace Sholto.App.Tests;

/// <summary>The confirm guard (a load onto a playing deck needs a second request within 3 s) and undo (the last
/// load can be reverted for 10 s), driven through <c>LoadSelectedIntoDeck</c> and <c>UndoLastLoad</c>.</summary>
public class LoadGuardAndUndoTests
{
    private static readonly Origin From = TrackLoaderRig.Origin;
    private static readonly Track Alpha = LibrarySessionRig.Alpha;
    private static readonly Track Bravo = LibrarySessionRig.Bravo;
    private static readonly Track Charlie = LibrarySessionRig.Charlie;

    private static async Task Eventually(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException("The condition never became true.");
            await Task.Delay(10);
        }
    }

    private static void Load(TrackLoaderRig rig, Track track, int deck)
    {
        rig.Highlight(track);
        rig.Loader.Handle(new LoadSelectedIntoDeck(deck, From));
    }

    /// <summary>Deck 1 holds Alpha, loaded, with the playhead at <paramref name="position"/>, and is playing.</summary>
    private static async Task<IDeckSession> PlayingAlphaAsync(TrackLoaderRig rig, double position = 0.4)
    {
        var deck = rig.Library.Deck1;
        // IsLoaded is the last thing LoadTrack raises; starting play before it would be undone by LoadTrack's own reset.
        var landed = false;
        deck.Changed += change => landed |= change == DeckChange.IsLoaded;
        Load(rig, Alpha, 0);
        await Eventually(() => landed);
        ((ScriptedPlayhead)deck.Playhead).PlayPosition = position;
        ((SpyTransport)deck.Transport).Play();
        deck.SyncPlayPosition();
        Assert.True(deck.IsPlaying);
        return deck;
    }

    private static RecordingHandler<T> Record<T>(TrackLoaderRig rig) where T : struct, IEvent
    {
        var handler = new RecordingHandler<T>();
        rig.Library.Bus.Subscribe(handler);
        return handler;
    }

    private static DeckTrack Projected(Track t) => new(t.FilePath, t.Title, t.Artist, t.Duration);

    /// <summary>Follow deck 1's content the way an interface does (the rig does not wire the publisher), keeping
    /// only what is published from now on.</summary>
    private static RecordingHandler<DeckContentChanged> RecordContent(TrackLoaderRig rig)
    {
        new DeckEventPublisher(rig.Library.Deck1, rig.Library.Bus).Start();
        var handler = Record<DeckContentChanged>(rig);
        handler.Received.Clear();
        return handler;
    }

    private static bool Shows(RecordingHandler<DeckContentChanged> content, Track track) =>
        content.Received.LastOrDefault() is { LoadState: DeckLoadState.Loaded, Track: { } t } && t == Projected(track);

    private static TrackLoaderRig NewRig() => new(new FakeAudioFileDecoder());

    // ---- Guard ----------------------------------------------------------------------------------

    [Fact]
    public async Task A_load_onto_a_stopped_deck_loads_at_once_with_no_warning()
    {
        var rig = NewRig();
        var pending = Record<LoadConfirmPending>(rig);

        Load(rig, Alpha, 0);

        Assert.Equal(Alpha, rig.Library.Deck1.LoadedTrack);
        await Eventually(() => rig.Library.Deck1.LoadState == DeckLoadState.Loaded);
        Assert.Empty(pending.Received);
    }

    [Fact]
    public async Task A_load_onto_a_playing_deck_warns_and_does_not_load()
    {
        var rig = NewRig();
        var deck = await PlayingAlphaAsync(rig);
        var pending = Record<LoadConfirmPending>(rig);
        var accepted = Record<LoadAccepted>(rig);

        Load(rig, Bravo, 0);

        var e = Assert.Single(pending.Received);
        Assert.True(e.Pending);
        Assert.Equal(0, e.Deck);
        Assert.Equal("Bravo", e.IncomingTitle);
        Assert.Equal("Alpha", e.PlayingTitle);
        Assert.Equal(Alpha, deck.LoadedTrack);
        Assert.True(deck.IsPlaying);
        Assert.Empty(accepted.Received);
    }

    [Fact]
    public async Task The_same_request_again_within_three_seconds_loads()
    {
        var rig = NewRig();
        var deck = await PlayingAlphaAsync(rig);
        var pending = Record<LoadConfirmPending>(rig);
        Load(rig, Bravo, 0);

        rig.Clock.Now += TimeSpan.FromSeconds(2.9);
        Load(rig, Bravo, 0);

        Assert.Equal(Bravo, deck.LoadedTrack);
        Assert.Equal([true, false], pending.Received.Select(p => p.Pending));
        await Eventually(() => deck.LoadState == DeckLoadState.Loaded);
    }

    [Fact]
    public async Task After_three_seconds_the_request_expires_and_a_press_warns_again_without_loading()
    {
        var rig = NewRig();
        var deck = await PlayingAlphaAsync(rig);
        var pending = Record<LoadConfirmPending>(rig);
        Load(rig, Bravo, 0);

        rig.Clock.Now += TimeSpan.FromSeconds(3.1);
        rig.Guard.OnFrame(rig.Clock.Now);
        Assert.Equal([true, false], pending.Received.Select(p => p.Pending));

        Load(rig, Bravo, 0);

        Assert.Equal(Alpha, deck.LoadedTrack);
        Assert.Equal([true, false, true], pending.Received.Select(p => p.Pending));
    }

    [Fact]
    public async Task A_second_press_after_expiry_without_a_tick_still_does_not_load()
    {
        var rig = NewRig();
        var deck = await PlayingAlphaAsync(rig);
        Load(rig, Bravo, 0);

        rig.Clock.Now += TimeSpan.FromSeconds(3.1);
        Load(rig, Bravo, 0);

        Assert.Equal(Alpha, deck.LoadedTrack);
    }

    [Fact]
    public async Task A_different_track_on_the_second_press_warns_afresh_and_does_not_load()
    {
        var rig = NewRig();
        var deck = await PlayingAlphaAsync(rig);
        var pending = Record<LoadConfirmPending>(rig);
        Load(rig, Bravo, 0);

        Load(rig, Charlie, 0);

        Assert.Equal(Alpha, deck.LoadedTrack);
        Assert.Equal(["Bravo", "Charlie"], pending.Received.Select(p => p.IncomingTitle));
    }

    // ---- Undo -----------------------------------------------------------------------------------

    /// <summary>Alpha playing at 0.4 on deck 1, then Bravo confirmed onto it.</summary>
    private static async Task<IDeckSession> ReplaceAlphaWithBravoAsync(TrackLoaderRig rig)
    {
        var deck = await PlayingAlphaAsync(rig, 0.4);
        Load(rig, Bravo, 0);
        Load(rig, Bravo, 0);
        await Eventually(() => deck.LoadState == DeckLoadState.Loaded && deck.LoadedTrack == Bravo);
        return deck;
    }

    [Fact]
    public async Task Undo_within_ten_seconds_restores_the_previous_track_and_position_paused()
    {
        var rig = NewRig();
        var deck = await ReplaceAlphaWithBravoAsync(rig);
        var content = RecordContent(rig);
        var accepted = Record<LoadAccepted>(rig);

        rig.Clock.Now += TimeSpan.FromSeconds(9.9);
        rig.Loader.Handle(new UndoLastLoad(From));

        Assert.Equal(Alpha, deck.LoadedTrack);
        await Eventually(() => deck.LoadState == DeckLoadState.Loaded && deck.LoadedTrack == Alpha);
        await Eventually(() => ((SpyTransport)deck.Transport).LastSeekFraction is not null);
        Assert.InRange(((SpyTransport)deck.Transport).LastSeekFraction!.Value, 0.39, 0.41);
        Assert.False(deck.IsPlaying);
        await Eventually(() => Shows(content, Alpha));
        Assert.Empty(accepted.Received);
    }

    [Fact]
    public async Task Undo_onto_a_previously_empty_deck_empties_it()
    {
        var rig = NewRig();
        var deck = rig.Library.Deck1;
        Load(rig, Alpha, 0);
        await Eventually(() => deck.LoadState == DeckLoadState.Loaded);
        var content = RecordContent(rig);

        rig.Loader.Handle(new UndoLastLoad(From));

        Assert.Equal(DeckLoadState.Idle, deck.LoadState);
        Assert.Null(deck.LoadedTrack);
        var last = content.Received.Last();
        Assert.Null(last.Track);
        Assert.Equal(DeckLoadState.Idle, last.LoadState);
    }

    [Fact]
    public async Task A_longer_configured_undo_window_still_undoes_at_fifteen_seconds()
    {
        var rig = new TrackLoaderRig(new FakeAudioFileDecoder(), load: new LoadOptions { UndoWindowSeconds = 20 });
        var deck = await ReplaceAlphaWithBravoAsync(rig);

        rig.Clock.Now += TimeSpan.FromSeconds(15);
        rig.Loader.Handle(new UndoLastLoad(From));

        Assert.Equal(Alpha, deck.LoadedTrack);
    }

    [Fact]
    public async Task Undo_after_ten_seconds_does_nothing()
    {
        var rig = NewRig();
        var deck = await ReplaceAlphaWithBravoAsync(rig);
        var content = RecordContent(rig);

        rig.Clock.Now += TimeSpan.FromSeconds(10.1);
        rig.Loader.Handle(new UndoLastLoad(From));

        Assert.Equal(Bravo, deck.LoadedTrack);
        Assert.DoesNotContain(content.Received, c => c.Track == Projected(Alpha));
    }

    [Fact]
    public async Task A_second_undo_does_nothing()
    {
        var rig = NewRig();
        var deck = await ReplaceAlphaWithBravoAsync(rig);
        rig.Loader.Handle(new UndoLastLoad(From));
        await Eventually(() => deck.LoadState == DeckLoadState.Loaded && deck.LoadedTrack == Alpha);
        var content = RecordContent(rig);

        rig.Loader.Handle(new UndoLastLoad(From));

        Assert.Equal(Alpha, deck.LoadedTrack);
        Assert.DoesNotContain(content.Received, c => c.Track != Projected(Alpha));
    }

    [Fact]
    public async Task Undo_while_the_new_tracks_decode_is_pending_leaves_the_previous_track_on_the_deck()
    {
        var gated = new GatedAudioFileDecoder();
        var rig = new TrackLoaderRig(gated);
        gated.Release(Alpha.FilePath);
        var deck = await PlayingAlphaAsync(rig);
        Load(rig, Bravo, 0);
        Load(rig, Bravo, 0);   // confirmed; Bravo's decode is held at the gate

        rig.Loader.Handle(new UndoLastLoad(From));
        await Eventually(() => deck.LoadState == DeckLoadState.Loaded && deck.LoadedTrack == Alpha);
        gated.Release(Bravo.FilePath);
        await Task.Delay(200);

        Assert.Equal(Alpha, deck.LoadedTrack);
        Assert.Equal(DeckLoadState.Loaded, deck.LoadState);
    }
}
