using System.ComponentModel;
using Avalonia.Media;
using Sholto.Interface.MainUI.Theming;

namespace Sholto.Interface.MainUI.ViewModels;

/// <summary>One card in the Layout Wizard's theme step: a theme, whether it is selected or was in use when
/// the wizard opened, and the key-chip colours its miniature shows (drawn from that theme's own palette).</summary>
public sealed class ThemeOption(SholtoTheme theme, bool isCurrent, IReadOnlyList<IBrush> keyChips) : INotifyPropertyChanged
{
    private bool _isSelected;

    public event PropertyChangedEventHandler? PropertyChanged;

    public SholtoTheme Theme { get; } = theme;
    public string Name => Theme.Name;
    public bool IsUser => Theme.IsUser;

    /// <summary>True for the theme in use when the wizard opened (styles the CURRENT tag).</summary>
    public bool IsCurrent { get; } = isCurrent;

    /// <summary>Three Camelot key-chip colours in this theme's key palette.</summary>
    public IReadOnlyList<IBrush> KeyChips { get; } = keyChips;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }
}
