using Avalonia.Threading;
using Sholto.Data;

namespace Sholto.Interface.MainUI;

/// <summary>The app thread is Avalonia's UI thread.</summary>
public sealed class AvaloniaAppThread : IAppThread
{
    public void Post(Action action) => Dispatcher.UIThread.Post(action);

    public bool IsCurrent => Dispatcher.UIThread.CheckAccess();
}
