using System.ComponentModel;
using Avalonia.Media.Imaging;
using Sholto.Interface.MainUI.Controls.WaveformStyles;

namespace Sholto.Interface.MainUI.ViewModels;

/// <summary>One card in the Layout Wizard's waveform step: a style, its copy, whether it is selected,
/// and a preview of the demo track drawn in that style (null until rendered).</summary>
public sealed class WaveformStyleOption(
    IWaveformStyleStrategy strategy,
    string key,
    string? tag,
    bool isCurrent,
    string description,
    IReadOnlyList<WaveformLegendEntry> legend) : INotifyPropertyChanged
{
    private bool _isSelected;
    private IReadOnlyList<WaveformLegendEntry> _legend = legend;
    private Bitmap? _preview;

    public event PropertyChangedEventHandler? PropertyChanged;

    public IWaveformStyleStrategy Strategy { get; } = strategy;
    public string Name => Strategy.DisplayName;
    /// <summary>The number key that selects this card ("1", "2").</summary>
    public string Key { get; } = key;
    /// <summary>"CURRENT", "NEW", or null for no tag.</summary>
    public string? Tag { get; } = tag;
    public bool HasTag => Tag is not null;
    /// <summary>True for the style in use when the wizard opened (styles the CURRENT tag).</summary>
    public bool IsCurrent { get; } = isCurrent;
    public string Description { get; } = description;
    /// <summary>The swatches under the card; follows the theme being tried on.</summary>
    public IReadOnlyList<WaveformLegendEntry> Legend
    {
        get => _legend;
        set
        {
            if (ReferenceEquals(_legend, value)) return;
            _legend = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Legend)));
        }
    }

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

    public Bitmap? Preview
    {
        get => _preview;
        set
        {
            if (ReferenceEquals(_preview, value)) return;
            _preview = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Preview)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasPreview)));
        }
    }

    public bool HasPreview => _preview is not null;

    /// <summary>Where the preview is in the demo track; set by the wizard when it opens.</summary>
    public IWaveformPreviewScroll? Scroll { get; set; }
}
