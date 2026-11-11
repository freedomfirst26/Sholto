using Sholto.Interface.MainUI.ViewModels.Glance;

namespace Sholto.Interface.MainUI.Views;

/// <summary>Keeps the rail list in step with the Glance view model: its items, its highlight, its count ticks, and a click on it.</summary>
internal interface ISearchOverlayRailSync
{
    /// <summary>Shows the view model's rail items and highlight; crates and tags whose count changed tick.</summary>
    void Sync(IGlanceViewModel glance);

    /// <summary>Puts the list's selection where the view model has it.</summary>
    void SyncHighlight(IGlanceViewModel glance);

    /// <summary>A selection the person made in the list: moves the view model there, or puts the selection back on a header.
    /// False when the change was the sync's own, so nothing was done.</summary>
    bool Pick(IGlanceViewModel glance);
}
