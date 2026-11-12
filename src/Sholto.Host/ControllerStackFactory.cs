using Sholto.App.Audio;
using Sholto.Interface.Controller;
using Sholto.Interface.Controller.Mappings;
using Sholto.Data;

namespace Sholto.Host;

/// <summary>Constructs the mapping registry, the controller sound card and the MIDI
/// manager, in that order. The MIDI manager gets the opener for this OS from <paramref name="openers"/>.</summary>
public sealed class ControllerStackFactory(ILogFactory logs, IMidiPortOpenerFactory openers) : IControllerStackFactory
{
    private readonly ILogFactory _logs = logs;
    private readonly IMidiPortOpenerFactory _openers = openers;

    public ControllerStack Build(ControllerMappingsOptions options)
    {
        var mappingRegistry = new MappingRegistry(new ControllerMappingCatalog(options).Mappings);
        // Built before audio starts: the audio picker and engine both need it to
        // tell the controller's own sound card apart from ordinary speakers.
        var soundCard = new ControllerSoundCard(mappingRegistry.Mappings.SelectMany(m => m.AudioDeviceNameMatches).ToList());

        var midi = new MidiManager(mappingRegistry, _openers.Create(), _logs)
        {
            LogAllMessages = Environment.GetEnvironmentVariable("SHOLTO_MIDI_LOG") == "1",
        };
        return new ControllerStack(mappingRegistry, soundCard, midi);
    }
}
