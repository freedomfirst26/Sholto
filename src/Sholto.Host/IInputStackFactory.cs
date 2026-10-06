using Sholto.Data;
using Sholto.Interface.Controller;
using Sholto.Interface.Keyboard;
using Sholto.App;
using Sholto.App.Lifecycle;

namespace Sholto.Host;

/// <summary>Builds the <see cref="InputStack"/> over a control surface, the headless core and a
/// keyboard.</summary>
public interface IInputStackFactory
{
    /// <param name="clock">The frame clock the performance tick (order 0) and the controller input subscribe to.</param>
    /// <param name="masterCueOutput">Where MASTER CUE takes effect (the audio engine, once it exists).</param>
    /// <param name="lifecycle">Handles the startup questions' answers and the menu's change requests.</param>
    InputStack Build(IControlSurface surface, CoreStack core, IKeyboard keyboard,
        SholtoOptions options, IFrameClock clock, IAppThread appThread, IMasterCueOutput masterCueOutput,
        IAppLifecycle lifecycle);
}
