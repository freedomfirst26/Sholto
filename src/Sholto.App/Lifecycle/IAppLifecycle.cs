using Sholto.App.Lifecycle.Folders;
using Sholto.Data;

namespace Sholto.App.Lifecycle;

/// <summary>The app's startup sequence and the questions it must put to the user. Where it needs the
/// user (the folders, the output device) it publishes <c>FoldersNeeded</c> /
/// <c>OutputDeviceNeeded</c> and waits for the answer command; the interface shows its picker and replies
/// with <c>ChooseFolders</c> / <c>ChooseOutputDevice</c> (a null answer means the picker was
/// cancelled). The folders question is asked first and the output device's after it, never both at once.
/// It also handles the menu's change requests and persists the chosen theme and waveform style.</summary>
public interface IAppLifecycle :
    ICommandHandler<ChooseOutputDevice>,
    ICommandHandler<ChangeOutputDevice>,
    ICommandHandler<ChooseTheme>,
    ICommandHandler<ChooseWaveformStyle>
{
    /// <summary>The music and analysis folders: their commands and the folder check query are registered on
    /// this, not on the lifecycle.</summary>
    ILibraryFolders Folders { get; }

    /// <summary>Begin the sequence. Call once, after the first paint, on the app thread. Opens the database
    /// off the app thread, restores the saved theme, resolves and scans the music folder, and starts audio
    /// (listing the output devices before it waits for the database).</summary>
    void Start();

    /// <summary>Stop the audio engine (the app is exiting).</summary>
    void Stop();
}
