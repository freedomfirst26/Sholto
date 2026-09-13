namespace Sholto.App.Theming;

/// <summary>
/// Theme registry. Themes are no longer hard-coded — they're loaded from JSON
/// files at startup via <see cref="SholtoThemeJson.LoadAll"/>: bundled themes
/// from <c>avares://Sholto.App/Themes/</c> plus optional user themes from
/// <c>~/.config/sholto/themes/</c>. The named static accessors (Classic,
/// Serato, …) survive for compile-time callers and just look up by name in
/// <see cref="All"/>. If a theme is missing (e.g. user deleted its JSON), the
/// accessor falls back to the first available theme so the UI never crashes.
/// </summary>
public static class Themes
{
    public static IReadOnlyList<SholtoTheme> All { get; } = SholtoThemeJson.LoadAll();

    private static SholtoTheme ByName(string name)
    {
        foreach (var t in All)
            if (string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase))
                return t;
        // Missing theme — fall back so the UI doesn't NRE during startup.
        return All.Count > 0 ? All[0] : throw new InvalidOperationException("No themes loaded");
    }

    public static SholtoTheme Classic           => ByName("Classic");
    public static SholtoTheme Serato            => ByName("Serato");
    public static SholtoTheme FrontLineAssembly => ByName("Front Line Assembly");
    public static SholtoTheme SilenceGroove     => ByName("Silence Groove");
    public static SholtoTheme JeremySoule       => ByName("Jeremy Soule");
    public static SholtoTheme TypeONegative     => ByName("Type O Negative");
    public static SholtoTheme BirthdayMassacre  => ByName("Birthday Massacre");
    public static SholtoTheme Pantera           => ByName("Pantera");
    public static SholtoTheme DimmuBorgir       => ByName("Dimmu Borgir");
    public static SholtoTheme AphexTwin         => ByName("Aphex Twin");
    public static SholtoTheme Prodigy           => ByName("The Prodigy");
}
