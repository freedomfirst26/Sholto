using Sholto.Data;
using Sholto.Interface.Faceplate.Model;
using Sholto.Interface.Faceplate.ViewModels;
using Sholto.Interface.Faceplate.Views;

namespace Sholto.Interface.Faceplate;

/// <summary>Loads a device's guide and assembles the overlay around it. The guide's view model is
/// subscribed to the bus here: it follows the commands the App echoes while Inspect is on and the Inspect
/// mode itself, and it tells the App when it is mounted.</summary>
public sealed class FaceplateOverlayFactory(
    FaceplateDocLoader docLoader, ICommandSender sender, IEventSubscriber subscriber) : IFaceplateOverlayFactory
{
    private readonly FaceplateDocLoader _docLoader = docLoader;
    private readonly ICommandSender _sender = sender;
    private readonly IEventSubscriber _subscriber = subscriber;

    public FaceplateOverlay Create(IDeviceFaceplate device)
    {
        var viewModel = new FaceplateViewModel(_docLoader.Load(device), _sender);
        // The guide lives as long as the app, so the subscriptions are never disposed.
        _subscriber.Subscribe<CommandReceived>(viewModel);
        _subscriber.Subscribe<InspectModeChanged>(viewModel);
        return new FaceplateOverlay(viewModel, new ProseInlinesFactory(), new ComboMarkerFormatter(), new FaceplateBrushes(), device.CreateLayout());
    }
}
