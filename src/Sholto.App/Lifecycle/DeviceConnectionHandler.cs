using Sholto.Data;

namespace Sholto.App.Lifecycle;

/// <summary>Handles <see cref="ReportDeviceConnection"/>: the controller's USB connection state becomes App
/// state, published as <see cref="DeviceConnectionChanged"/> for any interface to show.</summary>
public sealed class DeviceConnectionHandler(IEventPublisher publisher) : ICommandHandler<ReportDeviceConnection>
{
    private readonly IEventPublisher _publisher = publisher;

    public void Handle(in ReportDeviceConnection command) =>
        _publisher.Publish(new DeviceConnectionChanged(command.Connected));
}
