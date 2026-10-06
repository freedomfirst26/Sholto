using Sholto.App.Dsp;
using Sholto.App.Library;
using Sholto.Data;
using Sholto.App.Decks;
using Sholto.App.Lifecycle;
using Sholto.App.Loading;
using Sholto.App.Mixer;
using Sholto.App.Performance;

namespace Sholto.App.Tests;

/// <summary>The facts the App publishes for interfaces to show: the controller connecting or dropping, the
/// magnet becoming eligible, the crossfader moving, a track failing to load and a marker being dropped.
/// Each is checked on a real bus through the production class that publishes it.</summary>
public class FactPublishingTests
{
    private readonly Origin _from = new(InterfaceIds.Bench, "test", "fact");
    private readonly DataBus _bus = new(new ThrowingFailureSink());

    private async Task EventuallyAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException("The condition never became true.");
            await Task.Delay(10);
        }
    }

    // ---- Device connection ----------------------------------------------------------------------

    [Fact]
    public void Reporting_a_device_connection_publishes_DeviceConnectionChanged()
    {
        var connection = new RecordingHandler<DeviceConnectionChanged>();
        _bus.Subscribe(connection);
        _bus.Register<ReportDeviceConnection>(new DeviceConnectionHandler(_bus));

        _bus.Send(new ReportDeviceConnection(true, _from));
        _bus.Send(new ReportDeviceConnection(false, _from));

        Assert.Equal([new DeviceConnectionChanged(true), new DeviceConnectionChanged(false)], connection.Received);
    }

    [Fact]
    public void A_late_subscriber_is_replayed_the_last_device_connection()
    {
        new DeviceConnectionHandler(_bus).Handle(new ReportDeviceConnection(true, _from));
        var late = new RecordingHandler<DeviceConnectionChanged>();

        _bus.Subscribe(late);

        Assert.Equal([new DeviceConnectionChanged(true)], late.Received);
    }

    // ---- Magnet eligibility ---------------------------------------------------------------------

    [Fact]
    public void The_magnets_eligibility_changes_are_republished_on_the_bus()
    {
        var magnet = new F9FakeMagnetSnap();
        _ = new MagnetEligibilityPublisher(magnet, _bus);
        var eligibility = new RecordingHandler<MagnetEligibilityChanged>();
        _bus.Subscribe(eligibility);

        magnet.Raise(true);
        magnet.Raise(false);

        Assert.Equal([new MagnetEligibilityChanged(true), new MagnetEligibilityChanged(false)], eligibility.Received);
    }

    [Fact]
    public void A_late_subscriber_is_replayed_the_last_magnet_eligibility()
    {
        var magnet = new F9FakeMagnetSnap();
        _ = new MagnetEligibilityPublisher(magnet, _bus);
        magnet.Raise(true);
        var late = new RecordingHandler<MagnetEligibilityChanged>();

        _bus.Subscribe(late);

        Assert.Equal([new MagnetEligibilityChanged(true)], late.Received);
    }

    // ---- Crossfader -----------------------------------------------------------------------------

    private MixerSession NewMixer() =>
        new(new DeckPair(new DeckSessionRig(new TestDeckFactory().Create()).Session,
                new DeckSessionRig(new TestDeckFactory().Create()).Session),
            new EqualPowerCrossfade(), _bus);

    [Fact]
    public void Moving_the_crossfader_publishes_CrossfaderChanged_with_the_position()
    {
        var mixer = NewMixer();
        var moved = new RecordingHandler<Sholto.Data.CrossfaderChanged>();
        _bus.Subscribe(moved);

        mixer.Crossfader = 0.25;

        Assert.Equal(0.25, mixer.Crossfader);
        Assert.Equal(0.25, moved.Received[^1].Position);
    }

    [Theory]
    [InlineData(1.7, 1.0)]
    [InlineData(-0.2, 0.0)]
    public void A_crossfader_position_outside_the_range_is_published_clamped(double set, double expected)
    {
        var mixer = NewMixer();
        var moved = new RecordingHandler<Sholto.Data.CrossfaderChanged>();
        _bus.Subscribe(moved);

        mixer.Crossfader = set;

        Assert.Equal(expected, mixer.Crossfader);
        Assert.Equal(expected, moved.Received[^1].Position);
    }

    [Fact]
    public void A_late_subscriber_is_replayed_the_last_crossfader_position()
    {
        var mixer = NewMixer();
        mixer.Crossfader = 0.75;
        var late = new RecordingHandler<Sholto.Data.CrossfaderChanged>();

        _bus.Subscribe(late);

        Assert.Equal(0.75, Assert.Single(late.Received).Position);
    }

    // ---- Track load failure ---------------------------------------------------------------------

    private async Task<(LibrarySessionRig Rig, TrackLoader Loader)> NewLoaderAsync(FakeAudioFileDecoder decoder)
    {
        var rig = new LibrarySessionRig();
        var clock = new SettableFrameClock();
        await rig.Library.ScanAsync("/music", null);
        var loader = new TrackLoader(
            rig.Library, new DeckPair(rig.Deck1, rig.Deck2), decoder, new RecordingKeyAnalysisStore(),
            new FakeKeyAnalyzer(null), rig.Reporter, new ImmediateAppThread(), rig.Bus, new Sholto.App.Loading.SearchPick(rig.Library),
            new Sholto.App.Loading.LoadGuard(clock, rig.Bus), new Sholto.App.Loading.LoadUndo(clock, rig.Bus));
        return (rig, loader);
    }

    private void Highlight(LibrarySessionRig rig, Track track) =>
        rig.Library.Select(rig.Library.Rows.ToList().FindIndex(r => r.FilePath == track.FilePath));

    [Fact]
    public async Task A_failed_decode_publishes_TrackLoadFailed_with_the_deck_path_and_title()
    {
        var (rig, loader) = await NewLoaderAsync(new FakeAudioFileDecoder(new IOException("corrupt file")));
        Highlight(rig, LibrarySessionRig.Alpha);
        var failed = new F9AwaitableEventHandler<TrackLoadFailed>();
        rig.Bus.Subscribe(failed);

        loader.Handle(new LoadSelectedIntoDeck(1, _from));

        var e = await failed.NextAsync();
        Assert.Equal(new TrackLoadFailed(1, LibrarySessionRig.Alpha.FilePath, "Alpha"), e);
        Assert.Equal(Sholto.Data.DeckLoadState.Failed, rig.Deck2.LoadState);
    }

    [Fact]
    public async Task A_successful_load_publishes_no_TrackLoadFailed()
    {
        var (rig, loader) = await NewLoaderAsync(new FakeAudioFileDecoder());
        Highlight(rig, LibrarySessionRig.Alpha);
        var failed = new F9AwaitableEventHandler<TrackLoadFailed>();
        rig.Bus.Subscribe(failed);

        loader.Handle(new LoadSelectedIntoDeck(0, _from));

        await EventuallyAsync(() => rig.Deck1.LoadState == Sholto.Data.DeckLoadState.Loaded);
        Assert.False(failed.HasEvent);
    }

    [Fact]
    public async Task Loading_with_nothing_highlighted_publishes_no_TrackLoadFailed()
    {
        var (rig, loader) = await NewLoaderAsync(new FakeAudioFileDecoder(new IOException("corrupt file")));
        var failed = new F9AwaitableEventHandler<TrackLoadFailed>();
        rig.Bus.Subscribe(failed);

        loader.Handle(new LoadSelectedIntoDeck(0, _from));
        await Task.Delay(100);

        Assert.False(failed.HasEvent);
    }

    // ---- Marker added ---------------------------------------------------------------------------

    private async Task<(LibrarySessionRig Rig, DeckMarkers Markers)> NewMarkersAsync(bool attach)
    {
        var rig = new LibrarySessionRig();
        await rig.Library.ScanAsync("/music", rig.Stack());   // gives every track a catalog id
        var markers = new DeckMarkers(new DeckPair(rig.Deck1, rig.Deck2), rig.Library, new ImmediateAppThread(), rig.Bus);
        if (attach) markers.Attach(new FakeMarkerService());
        return (rig, markers);
    }

    [Fact]
    public async Task Adding_a_marker_publishes_MarkerAdded_with_the_deck_and_position()
    {
        var (rig, markers) = await NewMarkersAsync(attach: true);
        rig.Deck1.LoadTrack(LibrarySessionRig.Alpha, LibrarySessionRig.Alpha.FilePath, [], bpmMultiplier: 1.0);
        var added = new RecordingHandler<MarkerAdded>();
        rig.Bus.Subscribe(added);

        await markers.AddAsync(0);

        Assert.Equal([new MarkerAdded(0, rig.Deck1.PlaybackSeconds)], added.Received);
    }

    [Fact]
    public async Task Adding_a_marker_on_a_deck_with_no_track_publishes_nothing()
    {
        var (rig, markers) = await NewMarkersAsync(attach: true);
        var added = new RecordingHandler<MarkerAdded>();
        rig.Bus.Subscribe(added);

        await markers.AddAsync(1);

        Assert.Empty(added.Received);
    }

    [Fact]
    public async Task Adding_a_marker_without_the_marker_service_publishes_nothing()
    {
        var (rig, markers) = await NewMarkersAsync(attach: false);
        rig.Deck1.LoadTrack(LibrarySessionRig.Alpha, LibrarySessionRig.Alpha.FilePath, [], bpmMultiplier: 1.0);
        var added = new RecordingHandler<MarkerAdded>();
        rig.Bus.Subscribe(added);

        await markers.AddAsync(0);

        Assert.Empty(added.Received);
    }
}
