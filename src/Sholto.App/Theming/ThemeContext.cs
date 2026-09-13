namespace Sholto.App.Theming;

/// <summary>
/// Process-wide "what's the active theme" hook. View models read
/// <see cref="Current"/> when they need to compute a colour that depends on
/// the theme (e.g. a Camelot chip background) and can't easily participate
/// in Avalonia's <c>DynamicResource</c> system.
///
/// Composed once at bootstrap (<c>App.axaml.cs</c>) and passed down to
/// whatever needs it — <see cref="ViewModels.MainViewModel"/> pushes the
/// current theme in here whenever it changes.
///
/// Consumers don't subscribe to <see cref="Changed"/> directly — to avoid
/// the static-event-keeps-objects-alive footgun, <c>MainViewModel</c>
/// iterates its children and refreshes them explicitly. <see cref="Changed"/>
/// is still exposed for the rare case where a long-lived object needs to
/// react; because this is now an instance (not a static), any such
/// subscription releases normally when the subscriber and this instance
/// both become unreachable — no leak.
/// </summary>
public sealed class ThemeContext : IThemeContext
{
    private SholtoTheme _current = Themes.Classic;

    public SholtoTheme Current
    {
        get => _current;
        set
        {
            if (_current == value) return;
            _current = value;
            Changed?.Invoke();
        }
    }

    public event Action? Changed;
}
