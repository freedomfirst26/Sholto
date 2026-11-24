using System.ComponentModel;
using Avalonia.Input;
using Sholto.Data;
using Sholto.Interface.MainUI.Controls.Modal;

namespace Sholto.Interface.MainUI.ViewModels;

/// <inheritdoc />
public sealed class SystemReportViewModel : ISystemReportViewModel
{
    private SystemCheckReported? _report;
    private IReadOnlyList<SystemReportCapability> _capabilities = [];
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
        _capabilities = [.. report.Tools.GroupBy(t => t.Capability).Select(g => new SystemReportCapability([.. g]))];
        Notify(nameof(Capabilities));
        Notify(nameof(Headline));
        Notify(nameof(VerdictTitle));
        Notify(nameof(ReadyCount));
        Notify(nameof(IsHealthy));
        Notify(nameof(NeedsAttention));
        Notify(nameof(KeyHint));
    }

    public IReadOnlyList<SystemReportCapability> Capabilities => _capabilities;

    public string VerdictTitle => _report?.Health switch
    {
        SystemHealth.Offline => "Beat detection is off",
        SystemHealth.Degraded => "Running with gaps",
        _ => "All set",
    };

    public string ReadyCount => $"{_capabilities.Count(c => c.IsReady)} of {_capabilities.Count} ready";

    public bool IsHealthy => !NeedsAttention;

    public bool NeedsAttention => _report?.Health is SystemHealth.Degraded or SystemHealth.Offline;

    public string Headline => _report?.Health switch
    {
        SystemHealth.Offline =>
            "Beat detection is unavailable, so tracks get no BPM, beatgrid, waveform or key.",
        SystemHealth.Degraded =>
            "Sholto is running, but some optional analysis features are unavailable.",
        _ => "Everything Sholto needs is installed.",
    };

    public string Eyebrow => "SYSTEM REPORT";

    public ModalTone Tone => ModalTone.Accent;

    public string Title => "";

    public string? Subtitle => null;

    public string KeyHint => NeedsAttention
        ? "Restart Sholto after installing. Tools are checked once at startup."
        : "Tools are checked once at startup.";

    public ModalWidth Width => ModalWidth.Regular;

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
