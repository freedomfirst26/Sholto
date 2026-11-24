namespace Sholto.Interface.MainUI.ViewModels;

/// <summary>How one capability on the system report stands: its preferred tool is there, only a fallback is, or
/// nothing is.</summary>
public enum SystemReportCapabilityState
{
    Ready,
    ReadyWithFallback,
    Missing,
}
