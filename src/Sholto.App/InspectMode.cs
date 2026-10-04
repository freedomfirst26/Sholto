using Sholto.Data;

namespace Sholto.App;

/// <summary>Owns Inspect mode. Setting it to the value it already has does nothing; a real change is
/// announced as <see cref="InspectModeChanged"/> for the controller adapter, the scratch engine and the
/// Faceplate to follow.</summary>
public sealed class InspectMode(IEventPublisher publisher) : IInspectMode
{
    private readonly IEventPublisher _publisher = publisher;

    public bool IsOn { get; private set; }

    public void Handle(in SetInspectMode command)
    {
        if (IsOn == command.On) return;
        IsOn = command.On;
        _publisher.Publish(new InspectModeChanged(IsOn));
    }
}
