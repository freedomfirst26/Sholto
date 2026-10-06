using System.ComponentModel;
using Sholto.Data;
using Sholto.Interface.MainUI.Theming;

namespace Sholto.Interface.MainUI.ViewModels;

/// <inheritdoc />
public sealed class ThemeViewModel : IThemeViewModel
{
    private readonly IThemeContext _context;
    private readonly IThemeCatalog _catalog;
    private readonly ICommandSender _sender;
    private SholtoTheme _shown;
    private SholtoTheme _chosen;

    public ThemeViewModel(IThemeContext context, IThemeCatalog catalog, ICommandSender sender)
    {
        _context = context;
        _catalog = catalog;
        _sender = sender;
        _shown = catalog.ByName("Silence Groove");
        _chosen = _shown;
        // Make the initial theme visible to anything that reads the context before the user picks another.
        _context.Current = _shown;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public IReadOnlyList<SholtoTheme> All => _catalog.All;

    public string UserThemesDirectory => _catalog.UserThemesDirectory;

    public SholtoTheme Shown => _shown;

    public SholtoTheme Chosen => _chosen;

    public void Preview(SholtoTheme theme)
    {
        if (_shown == theme) return;
        _shown = theme;
        // Publish to the process-wide hook so anything not in our visual tree (e.g. value converters) sees it.
        _context.Current = theme;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Shown)));
    }

    public void Choose(SholtoTheme theme)
    {
        Preview(theme);
        if (_chosen == theme) return;
        _chosen = theme;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Chosen)));
        _sender.Send(new ChooseTheme(theme.Name, new Origin(InterfaceIds.MainUI, "theme", "choose")));
    }

    public void Restore(string savedName)
    {
        var match = _catalog.All.FirstOrDefault(t => t.Name == savedName);
        if (match is null)
        {
            Console.WriteLine($"[Theme] saved name '{savedName}' no longer exists — keeping default");
            return;
        }
        if (_chosen != match)
        {
            _chosen = match;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Chosen)));
        }
        Preview(match);
    }
}
