namespace Sholto.Controller;

/// <summary>Broadcasts controller feedback (currently light state) from app-side
/// producers to listeners such as <see cref="LightingController"/>. Producers
/// publish freely on state change; the bus de-dupes so only genuine changes are
/// broadcast (no redundant MIDI writes to the controller).</summary>
public interface IFeedbackBus
{
    /// <summary>Producer entry point: set a deck's light on/off.</summary>
    void SetLight(int deck, LightFunction function, bool on);

    /// <summary>Raised when a light actually changes state.</summary>
    event Action<LightChanged>? LightChanged;
}

public sealed class FeedbackBus : IFeedbackBus
{
    private readonly Dictionary<ControllerLight, bool> _state = new();

    public event Action<LightChanged>? LightChanged;

    public void SetLight(int deck, LightFunction function, bool on)
    {
        var light = new ControllerLight(deck, function);
        if (_state.TryGetValue(light, out var current) && current == on) return;
        _state[light] = on;
        LightChanged?.Invoke(new LightChanged(light, on));
    }
}
