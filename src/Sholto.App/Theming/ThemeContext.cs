namespace Sholto.App.Theming;

/// <summary>
/// Process-wide "what's the active theme" hook. View models read
/// <see cref="Current"/> when they need to compute a colour that depends on
/// the theme (e.g. a Camelot chip background) and can't easily participate
/// in Avalonia's <c>DynamicResource</c> system.
///
/// MainViewModel pushes the current theme in here whenever it changes.
/// Consumers don't subscribe to <see cref="Changed"/> directly — to avoid
/// the static-event-keeps-objects-alive footgun, MainViewModel iterates its
/// children and refreshes them explicitly. Changed is still exposed for the
/// rare case where a long-lived control needs to react.
/// </summary>
public static class ThemeContext
{
    private static SholtoTheme _current = Themes.Classic;

    public static SholtoTheme Current
    {
        get => _current;
        set
        {
            if (_current == value) return;
            _current = value;
            Changed?.Invoke();
        }
    }

    public static event Action? Changed;
}
