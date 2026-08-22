namespace Sholto.Controller;

/// <summary>Listens to the feedback bus and drives the controller's LEDs. It is
/// the ONLY place app state turns into light output: each logical
/// <see cref="LightChanged"/> is rendered to device MIDI bytes by the active
/// mapping and written out the controller's port. Producers never touch MIDI, and
/// the mapping owns every device-specific byte — so adding a new light is one
/// event on the producer side plus one line in the mapping.</summary>
public sealed class LightingController : IDisposable
{
    private readonly IFeedbackBus _bus;
    private readonly MidiManager _midi;

    public LightingController(IFeedbackBus bus, MidiManager midi)
    {
        _bus = bus;
        _midi = midi;
        _bus.LightChanged += OnLightChanged;
    }

    private void OnLightChanged(LightChanged e)
    {
        var bytes = _midi.Mapping?.RenderLight(e.Light, e.On);
        if (bytes is { Length: > 0 }) _midi.Send(bytes);
    }

    public void Dispose() => _bus.LightChanged -= OnLightChanged;
}
