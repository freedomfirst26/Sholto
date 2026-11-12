using Sholto.Data;

namespace Sholto.App.Library;

/// <summary>An <see cref="IProgress{T}"/> that runs <paramref name="onReport"/> on the app thread, whatever
/// thread reports.</summary>
public sealed class AppThreadProgress<T>(IAppThread appThread, Action<T> onReport) : IProgress<T>
{
    private readonly IAppThread _appThread = appThread;
    private readonly Action<T> _onReport = onReport;

    public void Report(T value) => _appThread.Post(() => _onReport(value));
}
