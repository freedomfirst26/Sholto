namespace Sholto.Interface.MainUI.Theming;

/// <summary>An already-loaded list of themes. Built once at startup by
/// <see cref="ThemeCatalogFactory"/>, after Avalonia is up.</summary>
public sealed class ThemeCatalog(IReadOnlyList<SholtoTheme> themes) : IThemeCatalog
{
    public IReadOnlyList<SholtoTheme> All { get; } = themes;

    public SholtoTheme ByName(string name)
    {
        foreach (var t in All)
            if (string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase))
                return t;
        // Saved settings from before "Birthday Massacre" became "The Birthday Massacre"
        // still resolve: retry with a leading "The " ignored on both sides.
        var bare = WithoutThe(name);
        foreach (var t in All)
            if (string.Equals(WithoutThe(t.Name), bare, StringComparison.OrdinalIgnoreCase))
                return t;
        // Missing theme — fall back so the UI doesn't NRE during startup.
        return All.Count > 0 ? All[0] : throw new InvalidOperationException("No themes loaded");
    }

    private string WithoutThe(string name) =>
        name.StartsWith("The ", StringComparison.OrdinalIgnoreCase) ? name[4..] : name;
}
