namespace Sholto.App.Storage;

/// <summary>
/// Single entry point for opening the Sholto library database: migrates it and
/// returns a <see cref="DatabaseStack"/> exposing only ports.
/// </summary>
public interface ISholtoStorage
{
    Task<DatabaseStack> OpenAsync(string? overridePath = null, CancellationToken ct = default);

    string DefaultDbPath();
}
