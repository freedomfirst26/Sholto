using Sholto.App.Lifecycle.Folders;
using Sholto.Data;

namespace Sholto.App.Lifecycle;

/// <summary>A lifecycle that does nothing, for a host with no database, no audio device and no pickers
/// (Bench): every command is accepted and ignored.</summary>
public sealed class NullAppLifecycle : IAppLifecycle
{
    public ILibraryFolders Folders { get; } = new NullLibraryFolders();

    public void Start() { }

    public void Stop() { }

    public void Handle(in ChooseOutputDevice command) { }

    public void Handle(in ChangeOutputDevice command) { }

    public void Handle(in ChooseTheme command) { }

    public void Handle(in ChooseWaveformStyle command) { }
}
