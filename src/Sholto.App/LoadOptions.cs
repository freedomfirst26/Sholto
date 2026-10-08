namespace Sholto.App;

/// <summary>Track-load safety windows, supplied via the standard <c>IOptions&lt;LoadOptions&gt;</c> pipeline like
/// <see cref="GlanceOptions"/>. Defaults live here.</summary>
public sealed class LoadOptions
{
    /// <summary>How long, in seconds, a load can be undone after it lands.</summary>
    public int UndoWindowSeconds { get; set; } = 10;

    /// <summary>How long, in seconds, a second load request may follow the first to confirm replacing a playing deck.</summary>
    public int ConfirmWindowSeconds { get; set; } = 3;
}
