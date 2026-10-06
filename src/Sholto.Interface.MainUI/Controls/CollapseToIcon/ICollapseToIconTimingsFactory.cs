namespace Sholto.Interface.MainUI.Controls.CollapseToIcon;

/// <summary>Names the timing recipes.</summary>
public interface ICollapseToIconTimingsFactory
{
    /// <summary>300 ms shrink, 260 ms grow, three 900 ms pulses, 150 ms reduced fade, 4 s reduced hold.</summary>
    CollapseToIconTimings Standard();
}
