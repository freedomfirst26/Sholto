using System.ComponentModel;

namespace Sholto.Interface.MainUI.ViewModels;

/// <summary>The brief confirmation text on the main window.</summary>
public interface IToastViewModel : INotifyPropertyChanged
{
    /// <summary>Brief confirmation text (e.g. "Added to Warmup Set"); null = hidden.</summary>
    string? Toast { get; }

    bool HasToast { get; }

    /// <summary>Show <paramref name="text"/>; it clears itself a moment later.</summary>
    void Show(string text);
}
