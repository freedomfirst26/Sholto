namespace Sholto.Interface.MainUI.Theming;

/// <summary>Builds the <see cref="ThemeStack"/>. Must be called from
/// <c>App.OnFrameworkInitializationCompleted</c> or later: loading bundled themes
/// goes through Avalonia's <c>AssetLoader</c> (<c>avares://</c>), which needs a live
/// application.</summary>
public interface IThemeStackFactory
{
    ThemeStack Build();
}
