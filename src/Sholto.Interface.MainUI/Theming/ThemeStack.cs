namespace Sholto.Interface.MainUI.Theming;

/// <summary>The theme catalog and the live theme context built over it.</summary>
public sealed class ThemeStack(
    IThemeCatalog catalog,
    IThemeContext context,
    IThemeDefaults defaults)
{
    public IThemeCatalog Catalog { get; } = catalog;
    public IThemeContext Context { get; } = context;
    public IThemeDefaults Defaults { get; } = defaults;
}
