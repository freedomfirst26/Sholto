namespace Sholto.App.Theming;

/// <summary>
/// The theme currently in effect, and notification when it changes.
///
/// A port rather than the concrete <see cref="ThemeContext"/> for a practical reason:
/// the real implementation resolves its initial theme through Avalonia's asset loader,
/// so constructing one requires a live Avalonia application. Anything that only needs
/// to read colours — a view model under test, a harness rendering offline — can be
/// handed a fixed theme instead of standing up a UI framework.
/// </summary>
public interface IThemeContext
{
    /// <summary>The active theme. Setting it to a different instance raises
    /// <see cref="Changed"/>; setting it to the same one does nothing.</summary>
    SholtoTheme Current { get; set; }

    /// <summary>Raised after <see cref="Current"/> changes, so views can re-read
    /// brushes and repaint.</summary>
    event Action? Changed;
}
