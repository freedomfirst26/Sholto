using System.ComponentModel;
using System.Runtime.CompilerServices;
using Sholto.Data;

namespace Sholto.Interface.MainUI.ViewModels;

/// <summary>The brief confirmation text: set by <see cref="Show"/>, cleared on the app thread a moment later.</summary>
public sealed class ToastViewModel(IAppThread appThread) : IToastViewModel
{
    private readonly IAppThread _appThread = appThread;

    public event PropertyChangedEventHandler? PropertyChanged;

    private string? _toast;
    /// <summary>Brief confirmation text (e.g. "Added to Warmup Set"); null = hidden.</summary>
    public string? Toast
    {
        get => _toast;
        private set { _toast = value; Notify(); Notify(nameof(HasToast)); }
    }
    public bool HasToast => !string.IsNullOrEmpty(_toast);

    public void Show(string text)
    {
        Toast = text;
        _ = HideLaterAsync();
    }

    /// <summary>The wait runs off the app thread; clearing the toast comes back onto it.</summary>
    private async Task HideLaterAsync()
    {
        await Task.Delay(1800).ConfigureAwait(false);
        _appThread.Post(() => Toast = null);
    }

    private void Notify([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
