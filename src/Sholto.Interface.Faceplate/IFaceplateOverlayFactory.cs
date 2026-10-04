using Sholto.Interface.Faceplate.Views;

namespace Sholto.Interface.Faceplate;

/// <summary>Builds the controller guide overlay for a device, with its view model,
/// prose builder and brushes, so the overlay constructs none of them itself.</summary>
public interface IFaceplateOverlayFactory
{
    /// <summary>Loads the device's guide and builds an overlay over its drawing.</summary>
    FaceplateOverlay Create(IDeviceFaceplate device);
}
