namespace Sholto.App.Lifecycle;

/// <inheritdoc />
public sealed class SettingPreference(ILibraryDatabase database, string key) : ISettingPreference
{
    private readonly ILibraryDatabase _database = database;
    private readonly string _key = key;

    public async Task<string?> GetAsync()
    {
        var opened = await _database.Opened;
        if (opened is null) return null;
        return await opened.Settings.GetAsync(_key);
    }

    public async Task SetAsync(string value)
    {
        var opened = await _database.Opened;
        if (opened is null) return;
        await opened.Settings.SetAsync(_key, value);
    }
}
