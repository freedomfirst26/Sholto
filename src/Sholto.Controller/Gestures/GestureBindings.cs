namespace Sholto.Controller.Gestures;

/// <summary>One consumer's answer to "what does each gesture mean?".
/// <para>A gesture is an abstract thing. The App binds `play.press` to starting a deck;
/// the controller guide binds the same id to blinking a shape on a diagram. Same
/// vocabulary, different delegates, and both can be live at once.</para></summary>
public sealed class GestureBindings
{
    private readonly IReadOnlyDictionary<string, Action<Gesture>> _map;

    public GestureBindings(string name, IReadOnlyDictionary<string, Action<Gesture>> map)
    {
        Name = name;
        _map = map;
    }

    /// <summary>For logging and for tests. Not used to look anything up.</summary>
    public string Name { get; }

    /// <summary>False suspends this consumer without unregistering it. Inspect mode
    /// disables the App's table so pressing PLAY explains PLAY instead of starting it.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>The ids this table answers to. A test asserts the App's table covers
    /// every id the recognizer can emit.</summary>
    public IReadOnlySet<string> BoundIds => _map.Keys.ToHashSet();

    public void Invoke(Gesture gesture)
    {
        if (!Enabled) return;
        if (_map.TryGetValue(gesture.Id, out var action)) action(gesture);
    }
}
