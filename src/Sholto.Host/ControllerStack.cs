using Sholto.App.Audio;
using Sholto.Interface.Controller;

namespace Sholto.Host;

/// <summary>The controller-side collaborators: mapping registry, the controller's own
/// sound card, and the MIDI connection over the registry.</summary>
public sealed class ControllerStack(
    IControllerMappings mappingRegistry,
    IControllerSoundCard soundCard,
    IMidiConnection midi)
{
    public IControllerMappings MappingRegistry { get; } = mappingRegistry;
    public IControllerSoundCard SoundCard { get; } = soundCard;
    public IMidiConnection Midi { get; } = midi;
}
