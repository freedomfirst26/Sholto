using Sholto.Interface.MainUI.ViewModels.Glance;

namespace Sholto.Interface.MainUI.Views;

/// <summary>Keeps the results table in step with the Glance view model: its rows, the empty hints, its highlight,
/// the slow indicator, the settle-in and Track List cues, and a click on a row.</summary>
internal interface ISearchOverlayResultsSync
{
    /// <summary>Shows the view model's rows and highlight.</summary>
    void Sync(IGlanceViewModel glance);

    /// <summary>With no rows: the no-match hint, or the centred scope line with the chip names in bright text.</summary>
    void SyncEmpty(IGlanceViewModel glance);

    /// <summary>Puts the list's selection where the view model has it.</summary>
    void SyncHighlight(IGlanceViewModel glance);

    /// <summary>The slow indicator: rows dim and the fit column's neutral bars carry the light.</summary>
    void SyncReassessing(IGlanceViewModel glance);

    /// <summary>A result replaced the rows: the table fades in for a moment (never under reduced motion).</summary>
    void Settle(IGlanceViewModel glance);

    /// <summary>A song just joined the Track List: its row, if on screen, glows for a moment (never under reduced motion).</summary>
    void Flash(IGlanceViewModel glance, string path);

    /// <summary>A row the person clicked: moves the view model's highlight there. False when the change was the
    /// sync's own, so nothing was done.</summary>
    bool Pick(IGlanceViewModel glance);
}
