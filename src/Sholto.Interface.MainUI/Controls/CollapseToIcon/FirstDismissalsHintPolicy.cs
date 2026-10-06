namespace Sholto.Interface.MainUI.Controls.CollapseToIcon;

/// <summary>Hints for the first <c>limit</c> dismissals ever, counted across launches. The saved count is
/// loaded once; until it arrives the policy hints, and a hint shown meanwhile still counts.</summary>
public sealed class FirstDismissalsHintPolicy : IHintPolicy
{
    private readonly int _limit;
    private readonly IHintCounter _counter;
    private int _loaded;
    private int _shownHere;

    public FirstDismissalsHintPolicy(int limit, IHintCounter counter)
    {
        _limit = limit;
        _counter = counter;
        _ = LoadAsync();
    }

    public bool ShouldHint() => Volatile.Read(ref _loaded) + Volatile.Read(ref _shownHere) < _limit;

    public void HintShown()
    {
        Interlocked.Increment(ref _shownHere);
        _counter.Record();
    }

    private async Task LoadAsync()
    {
        try { Volatile.Write(ref _loaded, await _counter.CountAsync()); }
        catch (Exception ex) { Console.WriteLine($"[FirstDismissalsHintPolicy] load failed: {ex.Message}"); }
    }
}
