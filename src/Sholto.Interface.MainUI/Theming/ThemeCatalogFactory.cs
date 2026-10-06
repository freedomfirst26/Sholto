namespace Sholto.Interface.MainUI.Theming;

/// <summary>One-time build of the <see cref="ThemeCatalog"/>. Must run in
/// <c>App.OnFrameworkInitializationCompleted</c> or later: loading bundled themes
/// goes through Avalonia's <c>AssetLoader</c> (<c>avares://</c>), which needs a live
/// application.</summary>
public sealed class ThemeCatalogFactory(ISholtoThemeJson themeJson)
{
    public ThemeCatalog Build() => new(themeJson.LoadAll(), themeJson.UserThemesDir());
}
