using Sholto.Data;

namespace Sholto.Interface.Controller;

/// <summary>Builds the <see cref="IControllerInput"/> over a control surface.</summary>
public interface IControllerInputFactory
{
    /// <param name="surface">Where the controller's events come from.</param>
    /// <param name="sender">Where its commands go.</param>
    /// <param name="appThread">The app thread; MIDI-thread events are posted onto it before anything else.</param>
    /// <param name="clock">Ticks the recognizer (browse-hold) at an interface order, after the App core.</param>
    IControllerInput Create(IControlSurface surface, ICommandSender sender, IAppThread appThread,
        IFrameClock clock);
}
