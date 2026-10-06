using System.ComponentModel;
using Avalonia.Input;
using Sholto.Data;
using Sholto.Interface.MainUI.Controls.Modal;

namespace Sholto.Interface.MainUI.ViewModels;

/// <inheritdoc />
public sealed class SystemReportViewModel : ISystemReportViewModel
{
    private SystemCheckReported? _report;
    private IReadOnlyList<SystemReportRow> _rows = [];
    private bool _isOpen;

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool IsOpen
    {
        get => _isOpen;
        private set
        {
            if (_isOpen == value) return;
            _isOpen = value;
            Notify(nameof(IsOpen));
        }
    }

    public void Open() => IsOpen = true;

    public void Close() => IsOpen = false;

    public void Report(SystemCheckReported report)
    {
        _report = report;
        _rows = [.. report.Tools.Select(t => new SystemReportRow(t))];
        Notify(nameof(Rows));
        Notify(nameof(Headline));
        Notify(nameof(Title));
    }

    public IReadOnlyList<SystemReportRow> Rows => _rows;

    public string Headline => _report?.Health switch
    {
        SystemHealth.Offline =>
            "Beat detection is unavailable, so tracks get no BPM, beatgrid, waveform or key.",
        SystemHealth.Degraded =>
            "Sholto is running, but some optional analysis features are unavailable.",
        _ => "Everything Sholto needs is installed.",
    };

    public string Eyebrow => "●  SYSTEM REPORT";

    public ModalTone Tone => ModalTone.Attention;

    public string Title => Headline;

    public string? Subtitle => null;

    public string KeyHint => "Tools are looked up once at startup — restart Sholto after installing.";

    public ModalWidth Width => ModalWidth.Narrow;

    public ModalButtons Buttons { get; } = new("Close", null, null);

    public ModalScrimClick ScrimClick => ModalScrimClick.Dismisses;

    public bool CapturesText => false;

    public bool CanGoBack => false;

    public bool CanConfirm => false;

    public void Dismiss() => Close();

    public void Back() { }

    public void Confirm() { }

    /// <summary>The report has no keys of its own; Esc is the router's.</summary>
    public bool HandleKey(Key key, KeyModifiers modifiers) => false;

    private void Notify(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
