namespace Sholto.Interface.Controller;

/// <summary>The controller's output side: turns App state events into LED writes.</summary>
public interface IControllerFeedback : IDisposable
{
    /// <summary>Start listening. The bus replays the current state of every event this subscribes to,
    /// so the lights come up matching the App. Calling it again drops the old subscriptions first.</summary>
    void Subscribe();

    /// <summary>Drop and re-take the subscriptions, so the bus replays current state: for a controller
    /// that dropped out and came back dark.</summary>
    void Resubscribe();

    /// <summary>Write every cached light (and pad page) to the surface again, without waiting for the bus. For after
    /// an Inspect session ended or anything else may have left the LEDs and the hardware pad mode out of step.</summary>
    void Reapply();
}
