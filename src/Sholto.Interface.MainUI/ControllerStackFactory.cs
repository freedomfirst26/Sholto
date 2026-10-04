using Sholto.App.Audio;
using Sholto.Interface.Controller;
using Sholto.Interface.Controller.Mappings;

namespace Sholto.Interface.MainUI;

/// <summary>Constructs the mapping registry, the controller sound card and the MIDI
/// manager, in that order.</summary>
public sealed class ControllerStackFactory : IControllerStackFactory
{
    public ControllerStack Build(ControllerMappingsOptions options)
    {
        var mappingRegistry = new MappingRegistry(new ControllerMappingCatalog(options).Mappings);
        // Built before audio starts: the audio picker and engine both need it to
        // tell the controller's own sound card apart from ordinary speakers.
        var soundCard = new ControllerSoundCard(mappingRegistry.Mappings.SelectMany(m => m.AudioDeviceNameMatches).ToList());

        var midi = new MidiManager(mappingRegistry, new AlsaRawMidiOpener())
        {
            LogAllMessages = Environment.GetEnvironmentVariable("SHOLTO_MIDI_LOG") == "1",
        };
        return new ControllerStack(mappingRegistry, soundCard, midi);
    }
}
