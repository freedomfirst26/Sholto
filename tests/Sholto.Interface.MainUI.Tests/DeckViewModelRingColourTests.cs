using Sholto.TestSupport.Allocation;
using Microsoft.Extensions.Options;
using Sholto.App.Analysis.Analyzers;
using Sholto.App.Analysis.Analyzers.Segments;
using Sholto.App.Analysis.Analyzers.Waveform;
using Sholto.App.Audio;
using Sholto.App.Library;
using Sholto.Data;
using Sholto.Interface.MainUI.Theming;
using Sholto.Interface.MainUI.ViewModels;

namespace Sholto.Interface.MainUI.Tests;

/// <summary>The disc ring colour steps with the playhead and is retoned on a theme switch.</summary>
[Collection(DeckViewModelCollection.Name)]
public class DeckViewModelRingColourTests
{
    private readonly DeckViewModelRig rig = new();

    [Fact]
    public void A_playing_frame_allocates_only_when_the_ring_colour_steps()
    {
        var notifications = 0;
        rig.Deck.PropertyChanged += (_, _) => notifications++;
        // The fixture's own rig.Changed list records every notification; size it up front so its doubling
        // growth is not counted as the view model's allocation.
        rig.Changed.Capacity = 1 << 20;

        // One 5-minute track at 60 fps is 18,000 frames; the position advances 1/18000 per frame.
        const int totalFrames = 18_000;
        const int warmUpFrames = 1_000;
        // Measured as one AvaloniaPropertyChangedEventArgs<Color> per SolidColorBrush.Color set.
        const long bytesPerColourStep = 64;

        DeckFrame Frame(int i) =>
            new(0, i / (double)totalFrames, i * 0.01, 1.0 + (i % 7) * 0.001, i % 2 == 0, i % 3 == 0, i % 5);

        var brush = (Avalonia.Media.SolidColorBrush)rig.Deck.DiscRingBrush;

        // A full unmeasured pass so tiered JIT/OSR has finished before measuring.
        for (var i = 0; i < totalFrames; i++) rig.Bus.Publish(Frame(i));

        // Five consecutive windows over the measured frames. A sporadic one-off runtime allocation on a random
        // frame must not fail the budget: it is enough that one window stays within its own colour-step budget.
        const int windows = 5;
        var measuredFrames = totalFrames - warmUpFrames;
        var framesPerWindow = measuredFrames / windows;
        var colourSteps = new int[windows];
        var window = 0;
        var result = new AllocationWindows(windows).Measure(() =>
        {
            var from = warmUpFrames + window * framesPerWindow;
            for (var i = from; i < from + framesPerWindow; i++)
            {
                var colourBefore = brush.Color;
                rig.Bus.Publish(Frame(i));
                if (brush.Color != colourBefore) colourSteps[window]++;
            }
            window++;
        });

        Assert.True(notifications > 0);
        Assert.True(colourSteps.Sum() < measuredFrames * 0.05,
            $"colour changed on {colourSteps.Sum()} of {measuredFrames} frames");
        Assert.True(Enumerable.Range(0, windows).Any(w => result.Bytes[w] <= colourSteps[w] * bytesPerColourStep),
            $"over budget in every window: {result}; colour changes per window: {string.Join(", ", colourSteps)}");
    }

    [Fact]
    public void RefreshThemeBindings_retones_the_ring_and_notifies_the_key_and_ring_brushes()
    {
        var stack = new ThemeStackFactory(new RecordingLogFactory()).Build();
        var deck = rig.NewDeck(0, stack.Context);
        var changed = new List<string?>();
        deck.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        var cur = stack.Context.Current;
        stack.Context.Current = cur with { Ring = cur.Ring with { Start = cur.Ring.End } };

        deck.RefreshThemeBindings();

        Assert.Equal(cur.Ring.End, ((Avalonia.Media.ISolidColorBrush)deck.DiscRingBrush).Color);
        Assert.Contains(nameof(DeckViewModel.KeyBrush), changed);
        Assert.Contains(nameof(DeckViewModel.DiscRingBrush), changed);
    }
}
