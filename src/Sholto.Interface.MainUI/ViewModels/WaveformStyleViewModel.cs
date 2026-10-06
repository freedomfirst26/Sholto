using System.ComponentModel;
using Sholto.Data;
using Sholto.Interface.MainUI.Controls.WaveformStyles;

namespace Sholto.Interface.MainUI.ViewModels;

/// <inheritdoc />
public sealed class WaveformStyleViewModel : IWaveformStyleViewModel
{
    private readonly IWaveformStyles _styles;
    private readonly ICommandSender _sender;
    private IWaveformStyleStrategy _shown;
    private IWaveformStyleStrategy _chosen;

    public WaveformStyleViewModel(IWaveformStyles styles, ICommandSender sender)
    {
        _styles = styles;
        _sender = sender;
        _shown = styles.Default;
        _chosen = styles.Default;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public IReadOnlyList<IWaveformStyleStrategy> All => _styles.All;

    public IWaveformStyleStrategy Shown => _shown;

    public IWaveformStyleStrategy Chosen => _chosen;

    public void Preview(IWaveformStyleStrategy style)
    {
        if (ReferenceEquals(_shown, style)) return;
        _shown = style;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Shown)));
    }

    public void Choose(IWaveformStyleStrategy style)
    {
        Preview(style);
        if (ReferenceEquals(_chosen, style)) return;
        _chosen = style;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Chosen)));
        _sender.Send(new ChooseWaveformStyle(style.Id, new Origin(InterfaceIds.MainUI, "waveform-style", "choose")));
    }

    public void Restore(string savedId)
    {
        var style = _styles.ById(savedId);
        if (!ReferenceEquals(_chosen, style))
        {
            _chosen = style;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Chosen)));
        }
        Preview(style);
    }
}
