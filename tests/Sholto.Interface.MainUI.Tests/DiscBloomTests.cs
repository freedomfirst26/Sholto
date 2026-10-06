using Sholto.App.Analysis.Analyzers;
using Sholto.App.Analysis.Analyzers.Segments;
using Sholto.App.Analysis.Analyzers.Waveform;
using Sholto.App.Audio;
using Sholto.App.Library;
using Sholto.Data;
using Sholto.Interface.MainUI.Theming;
using Sholto.Interface.MainUI.ViewModels;

namespace Sholto.Interface.MainUI.Tests;

/// <summary>The disc bloom: the band energies at the playhead become three glow opacities, with a fast
/// attack and a slower release, and an empty deck is dark.</summary>
public class DiscBloomTests
{
    private const double Frame = 0.016;

    private readonly DataBus _bus = new(new ThrowingFailureSink());
    private readonly FakeFrameClock _clock = new();
    private readonly DeckViewModel _deck;

    public DiscBloomTests()
    {
        AvaloniaTestApp.EnsureStarted();
        _deck = new DeckViewModel(0, _bus, _bus, new ThemeStackFactory().Build().Context, new NoPeaksFactory(),
            new DiscBloomFactory(_clock));
    }

    private void Load(float[] low, float[] mid, float[] high)
    {
        var analysis = new DeckAnalysis(
            new WaveformPeaks(Min: new float[low.Length], Max: new float[low.Length], low, mid, high, 1024, 48000),
            128.0, [], [], null, false, null);
        _bus.Publish(new DeckContentChanged(
            0, new DeckTrack("/music/a.mp3", "Alpha", "Zed", TimeSpan.FromMinutes(3)),
            DeckLoadState.Loaded, true, analysis));
    }

    private void Unload() =>
        _bus.Publish(new DeckContentChanged(0, null, DeckLoadState.Idle, false, null));

    private void Play(double position, int frames, double dt = Frame)
    {
        for (var i = 0; i < frames; i++)
        {
            _clock.Advance(dt);
            _bus.Publish(new DeckFrame(0, position, position * 180, 1.0, false, false, -1));
        }
    }

    [Fact]
    public void Each_glow_follows_its_own_band_with_the_mockup_scaling()
    {
        Load([0.5f], [0.25f], [1.0f]);

        Play(0.0, frames: 40, dt: 0.1);

        Assert.Equal(0.575, _deck.Bloom.Low, 2);    // low x1.15
        Assert.Equal(0.2, _deck.Bloom.Mid, 2);      // mid x0.8
        Assert.Equal(0.9, _deck.Bloom.High, 2);     // high x0.9
    }

    [Fact]
    public void The_low_glow_is_clamped_to_one()
    {
        Load([1.0f], [0f], [0f]);

        Play(0.0, frames: 40, dt: 0.1);

        Assert.Equal(1.0, _deck.Bloom.Low);
    }

    [Fact]
    public void The_glows_read_the_column_under_the_playhead()
    {
        Load([0f, 1f], [0f, 0f], [0f, 0f]);

        Play(0.0, frames: 40, dt: 0.1);
        Assert.Equal(0.0, _deck.Bloom.Low, 2);

        Play(0.75, frames: 40, dt: 0.1);
        Assert.Equal(1.0, _deck.Bloom.Low, 2);
    }

    [Fact]
    public void Attack_is_fast_and_release_is_slow()
    {
        Load([1f, 0f], [0f, 0f], [0f, 0f]);

        Play(0.0, frames: 1, dt: 0.03);
        var afterAttack = _deck.Bloom.Low;   // one attack time constant: ~63 % of the way (x1.15)
        Assert.InRange(afterAttack, 0.70, 0.76);

        Play(0.0, frames: 40, dt: 0.1);
        Play(0.75, frames: 1, dt: 0.03);     // the column drops to silence; same 30 ms
        var afterRelease = _deck.Bloom.Low;  // 30 ms of a 250 ms release: ~11 % down
        Assert.InRange(afterRelease, 0.99, 1.0);
        Play(0.75, frames: 5, dt: 0.05);     // 280 ms of release in all: ~33 % left (x1.15)
        Assert.InRange(_deck.Bloom.Low, 0.30, 0.46);
    }

    [Fact]
    public void An_empty_deck_is_dark()
    {
        Play(0.5, frames: 10, dt: 0.1);

        Assert.Equal(0.0, _deck.Bloom.Low);
        Assert.Equal(0.0, _deck.Bloom.Mid);
        Assert.Equal(0.0, _deck.Bloom.High);
    }

    [Fact]
    public void Emptying_the_deck_fades_the_glows_out()
    {
        Load([1f], [1f], [1f]);
        Play(0.0, frames: 40, dt: 0.1);
        Assert.True(_deck.Bloom.Low > 0.9);

        Unload();
        Play(0.0, frames: 80, dt: 0.1);

        Assert.True(_deck.Bloom.Low < 0.001);
        Assert.True(_deck.Bloom.Mid < 0.001);
        Assert.True(_deck.Bloom.High < 0.001);
    }

    [Fact]
    public void A_paused_deck_holds_the_value_at_the_playhead()
    {
        Load([0.6f], [0.6f], [0.6f]);
        Play(0.0, frames: 40, dt: 0.1);
        var held = _deck.Bloom.Low;
        var changes = 0;
        _deck.Bloom.Changed += () => changes++;

        Play(0.0, frames: 100);

        Assert.Equal(held, _deck.Bloom.Low, 3);
        Assert.Equal(0, changes);
    }

    [Fact]
    public void Changed_fires_when_a_glow_steps_and_not_when_it_holds()
    {
        Load([1f], [0f], [0f]);
        var changes = 0;
        _deck.Bloom.Changed += () => changes++;

        Play(0.0, frames: 1);
        Assert.True(changes > 0);

        Play(0.0, frames: 100, dt: 0.1);
        var settled = changes;
        Play(0.0, frames: 50, dt: 0.1);
        Assert.Equal(settled, changes);
    }

    [Fact]
    public void A_playing_frame_allocates_nothing_for_the_bloom()
    {
        // 2,000 columns of moving band energy, so the glows keep stepping during the measured frames.
        const int columns = 2_000;
        var low = new float[columns];
        var mid = new float[columns];
        var high = new float[columns];
        for (var i = 0; i < columns; i++)
        {
            low[i] = (float)(0.5 + 0.5 * Math.Sin(i * 0.05));
            mid[i] = (float)(0.5 + 0.5 * Math.Sin(i * 0.07));
            high[i] = (float)(0.5 + 0.5 * Math.Sin(i * 0.11));
        }
        Load(low, mid, high);

        var steps = 0;
        _deck.Bloom.Changed += () => steps++;
        var ring = (Avalonia.Media.SolidColorBrush)_deck.DiscRingBrush;
        const int totalFrames = 10_000;
        const int warmUpFrames = 1_000;

        void Run(int from, int to, ref int ringSteps)
        {
            for (var i = from; i < to; i++)
            {
                _clock.Advance(Frame);
                var colour = ring.Color;
                _bus.Publish(new DeckFrame(0, i / (double)totalFrames, i * 0.01, 1.0, false, false, -1));
                if (ring.Color != colour) ringSteps++;
            }
        }

        var ignored = 0;
        // A full unmeasured pass so tiered JIT/OSR has finished before measuring.
        Run(0, totalFrames, ref ignored);
        // Best of 3: a sporadic one-off runtime allocation on a random frame must not fail the budget.
        const int attempts = 3;
        var results = new List<(long Allocated, int RingSteps, int BloomSteps)>();
        var passed = false;
        for (var attempt = 0; attempt < attempts && !passed; attempt++)
        {
            steps = 0;
            var ringSteps = 0;
            var before = GC.GetAllocatedBytesForCurrentThread();
            Run(warmUpFrames, totalFrames, ref ringSteps);
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            results.Add((allocated, ringSteps, steps));
            // The glows moving on nearly every frame is part of what is being measured.
            Assert.True(steps > (totalFrames - warmUpFrames) / 2, $"the glows barely moved ({steps} steps)");
            // The only allocation a frame may make is the ring colour stepping (the existing ring test's
            // budget); the glows moving on nearly every frame add nothing.
            passed = allocated <= ringSteps * 64L;
        }

        Assert.True(passed,
            "allocated over budget in every attempt: "
            + string.Join("; ", results.Select(r => $"{r.Allocated} bytes over {r.RingSteps} ring colour steps ({r.BloomSteps} bloom steps)")));
    }
}
