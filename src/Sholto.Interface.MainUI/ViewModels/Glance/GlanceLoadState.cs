namespace Sholto.Interface.MainUI.ViewModels.Glance;

/// <summary>What a LOAD TO key offers for the highlighted item.</summary>
public enum GlanceLoadState
{
    /// <summary>Nothing to load here: the key is dimmed.</summary>
    Off,

    /// <summary>Pressing it loads the item.</summary>
    Ready,

    /// <summary>Already in the Track List: the key reads "In Track List" and does nothing.</summary>
    InList,
}
