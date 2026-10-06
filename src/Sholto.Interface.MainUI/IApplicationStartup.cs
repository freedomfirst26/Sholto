using Avalonia.Controls.ApplicationLifetimes;

namespace Sholto.Interface.MainUI;

/// <summary>What the host runs once Avalonia's framework is initialised: builds the window and wires
/// the world to it. App calls it from <c>OnFrameworkInitializationCompleted</c>.</summary>
public interface IApplicationStartup
{
    void Start(IClassicDesktopStyleApplicationLifetime desktop);
}
