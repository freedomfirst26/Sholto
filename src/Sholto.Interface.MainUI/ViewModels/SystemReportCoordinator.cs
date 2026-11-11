using System.ComponentModel;
using Sholto.Data;

namespace Sholto.Interface.MainUI.ViewModels;

/// <summary>The system report modal and the boot-time tool health. Raises only
/// <see cref="IsSystemReportOpen"/>, a name the main view model also has.</summary>
public sealed class SystemReportCoordinator : ISystemReportCoordinator
{
    private SystemHealth? _systemHealth;

    public event PropertyChangedEventHandler? PropertyChanged;

    public SystemReportCoordinator(ISystemReportViewModel systemReport)
    {
        SystemReportModal = systemReport;
        SystemReportModal.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ISystemReportViewModel.IsOpen))
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSystemReportOpen)));
        };
    }

    public ISystemReportViewModel SystemReportModal { get; }

    public bool IsSystemReportOpen => SystemReportModal.IsOpen;

    public bool ToolsMissing => _systemHealth is { } h && h != SystemHealth.Healthy;

    /// <summary>The boot-time tool probe's result, published once at startup by the App side. Nothing here
    /// re-probes the filesystem, and availability deliberately never goes live (see ExternalToolFinder: a
    /// tool installed mid-session needs a restart).</summary>
    public void Report(in SystemCheckReported e)
    {
        _systemHealth = e.Health;
        SystemReportModal.Report(e);
    }
}
