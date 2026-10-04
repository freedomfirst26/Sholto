using Sholto.App.Audio;

namespace Sholto.App.Lifecycle;

/// <summary>Builds the <see cref="IAppLifecycle"/> over the core.</summary>
public interface IAppLifecycleFactory
{
    /// <param name="core">The headless core the lifecycle seeds and scans into.</param>
    /// <param name="controllerSoundCard">Tells the controller's own sound card apart from ordinary speakers.</param>
    IAppLifecycle Create(CoreStack core, IControllerSoundCard controllerSoundCard);
}
