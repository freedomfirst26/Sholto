using Avalonia.Controls.ApplicationLifetimes;

namespace Sholto.Interface.MainUI;

/// <summary>Startup that does nothing: what the parameterless <see cref="App"/> constructor uses.</summary>
public sealed class NullApplicationStartup : IApplicationStartup
{
    public void Start(IClassicDesktopStyleApplicationLifetime desktop) { }
}
