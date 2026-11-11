using Sholto.Data;

namespace Sholto.App.Lifecycle;

/// <summary>Starts the audio output on the saved or chosen device, and handles the menu's device change.</summary>
public interface IOutputDeviceChooser : ICommandHandler<ChangeOutputDevice>, ICommandHandler<ChooseOutputDevice>
{
    /// <summary>Startup: lists the devices (before awaiting any preference), drops the controller's card, uses
    /// the saved device or asks, saves the choice, starts the audio output and remembers where master plays.</summary>
    Task StartAudioAsync();
}
