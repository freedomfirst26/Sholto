using Sholto.Data;

namespace Sholto.Interface.MainUI.Controls.CollapseToIcon;

public sealed class CollapseToIconSequenceFactory(IFrameClock clock, IMotionPreference motion) : ICollapseToIconSequenceFactory
{
    /// <summary>Interfaces tick after the App core (0..99).</summary>
    private const int InterfaceOrder = 100;

    private readonly IFrameClock _clock = clock;
    private readonly IMotionPreference _motion = motion;

    public ICollapseToIconSequence Create(CollapseToIconOptions options, IHintPolicy hintPolicy)
    {
        var sequence = new CollapseToIconSequence(_clock, _motion, options, hintPolicy);
        _clock.Subscribe(sequence, InterfaceOrder);
        return sequence;
    }
}
