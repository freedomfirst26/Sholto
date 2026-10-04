using Sholto.Data;

namespace Sholto.App.Performance;

/// <summary>Publishes the magnet's eligibility flips on the bus as <see cref="MagnetEligibilityChanged"/>.
/// The magnet raises its event from the frame tick, on the app thread. Created and kept alive by its
/// subscription to the magnet.</summary>
public sealed class MagnetEligibilityPublisher
{
    private readonly IEventPublisher _publisher;

    public MagnetEligibilityPublisher(IMagnetSnap magnet, IEventPublisher publisher)
    {
        _publisher = publisher;
        magnet.EligibilityChanged += OnEligibilityChanged;
    }

    private void OnEligibilityChanged(bool eligible) => _publisher.Publish(new MagnetEligibilityChanged(eligible));
}
