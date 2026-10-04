namespace Sholto.Interface.MainUI.ViewModels;

/// <summary>Session-only record of which tags were last selected, used to float
/// recently used tags to the top of suggestion and search lists.</summary>
public interface ITagRecency
{
    /// <summary>Stamps <paramref name="name"/> as used now.</summary>
    void MarkUsed(string? name);

    /// <summary>Every remembered tag, most recently selected first.</summary>
    IReadOnlyList<string> RecentNames(int limit);

    /// <summary>Reorders <paramref name="items"/> so remembered tags come first, newest first.</summary>
    IReadOnlyList<T> OrderRecentFirst<T>(IEnumerable<T> items, Func<T, string> nameOf);
}
