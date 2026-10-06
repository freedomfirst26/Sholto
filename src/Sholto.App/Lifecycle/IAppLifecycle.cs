using Sholto.Data;

namespace Sholto.App.Lifecycle;

/// <summary>The app's startup sequence and the questions it must put to the user. Where it needs the
/// user (the music folder, the output device) it publishes <c>MusicFolderNeeded</c> /
/// <c>OutputDeviceNeeded</c> and waits for the answer command; the interface shows its picker and replies
/// with <c>ChooseMusicFolder</c> / <c>ChooseOutputDevice</c> (a null answer means the picker was
/// cancelled). It also handles the menu's change requests and persists the chosen theme and waveform style.</summary>
public interface IAppLifecycle :
    ICommandHandler<ChooseMusicFolder>,
    ICommandHandler<ChooseOutputDevice>,
    ICommandHandler<ChangeMusicFolder>,
    ICommandHandler<ChangeOutputDevice>,
    ICommandHandler<ChooseTheme>,
    ICommandHandler<ChooseWaveformStyle>
{
    /// <summary>Begin the sequence. Call once, after the first paint, on the app thread. Opens the database
    /// off the app thread, restores the saved theme, resolves and scans the music folder, and starts audio
    /// (listing the output devices before it waits for the database).</summary>
    void Start();

    /// <summary>Stop the audio engine (the app is exiting).</summary>
    void Stop();
}
