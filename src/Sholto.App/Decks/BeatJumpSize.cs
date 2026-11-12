using Microsoft.Extensions.Options;

namespace Sholto.App.Decks;

/// <summary>The App's per-deck BEAT JUMP scale. A halving or doubling that would leave
/// [<see cref="BeatJumpOptions.MinScale"/>, <see cref="BeatJumpOptions.MaxScale"/>] is ignored, so the
/// scale stays where it was at a bound. Not persisted: every launch starts at 1.</summary>
public sealed class BeatJumpSize(IOptions<BeatJumpOptions> options) : IBeatJumpSize
{
    private readonly double _min = options.Value.MinScale;
    private readonly double _max = options.Value.MaxScale;
    private readonly double[] _scale = [1, 1];

    public double ScaleFor(int deck) => IsDeck(deck) ? _scale[deck] : 1;

    public void Halve(int deck) => SetIfWithin(deck, ScaleFor(deck) / 2);

    public void Double(int deck) => SetIfWithin(deck, ScaleFor(deck) * 2);

    private void SetIfWithin(int deck, double next)
    {
        if (IsDeck(deck) && next >= _min && next <= _max) _scale[deck] = next;
    }

    private bool IsDeck(int deck) => (uint)deck < _scale.Length;
}
