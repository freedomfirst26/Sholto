using Sholto.App.Settings;
using Sholto.Data;

namespace Sholto.App.Lifecycle;

/// <inheritdoc />
/// <remarks>Each count is stored under <see cref="SettingsKeys.HintShownPrefix"/> plus the hint key. Writes run in
/// the order they were recorded, and a read waits behind them, so a count asked for after a record includes it.
/// When the database is unavailable nothing is saved and every count is 0.</remarks>
public sealed class HintCounter(ILibraryDatabase database) : IHintCounter
{
    private readonly ILibraryDatabase _database = database;
    private readonly SemaphoreSlim _turn = new(1, 1);

    public void Handle(in RecordHintShown command)
    {
        if (!IsValid(command.HintKey)) return;
        _ = IncrementAsync(command.HintKey);
    }

    public Task<int> Handle(in GetHintShownCount query) =>
        IsValid(query.HintKey) ? ReadAsync(query.HintKey) : Task.FromResult(0);

    private bool IsValid(string? key) =>
        !string.IsNullOrEmpty(key) && key.All(c => c is >= 'a' and <= 'z' or '_');

    private async Task IncrementAsync(string key)
    {
        await _turn.WaitAsync();
        try
        {
            var opened = await _database.Opened;
            if (opened is null) return;
            var stored = SettingsKeys.HintShownPrefix + key;
            var count = await ParseAsync(opened.Settings.GetAsync(stored));
            await opened.Settings.SetAsync(stored, (count + 1).ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        catch (Exception ex) { Console.WriteLine($"[HintCounter] persist failed: {ex.Message}"); }
        finally { _turn.Release(); }
    }

    private async Task<int> ReadAsync(string key)
    {
        await _turn.WaitAsync();
        try
        {
            var opened = await _database.Opened;
            if (opened is null) return 0;
            return await ParseAsync(opened.Settings.GetAsync(SettingsKeys.HintShownPrefix + key));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[HintCounter] read failed: {ex.Message}");
            return 0;
        }
        finally { _turn.Release(); }
    }

    private async Task<int> ParseAsync(Task<string?> stored) =>
        int.TryParse(await stored, System.Globalization.NumberStyles.None,
            System.Globalization.CultureInfo.InvariantCulture, out var count) ? count : 0;
}
