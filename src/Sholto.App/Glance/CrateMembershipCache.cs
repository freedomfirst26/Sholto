using Sholto.App.Library;
using Sholto.App.Library.Crates;

namespace Sholto.App.Glance;

/// <inheritdoc cref="ICrateMembershipCache"/>
public sealed class CrateMembershipCache(ILibrarySession library) : ICrateMembershipCache
{
    private readonly CrateMembership _none = new(new Dictionary<int, IReadOnlySet<Guid>>(), null);

    private readonly ILibrarySession _library = library;
    private Task<CrateMembership>? _current;
    private ICrateService? _currentCrates;

    public Task<CrateMembership> CurrentAsync()
    {
        var crates = _library.Crates;
        if (crates is null) return Task.FromResult(_none);
        // A new crate service (the database came up) or a failed fetch is not reusable.
        if (_current is { IsFaulted: false, IsCanceled: false } && ReferenceEquals(_currentCrates, crates)) return _current;
        _currentCrates = crates;
        return _current = Task.Run(() => crates.MembershipAsync());
    }

    public void Invalidate() => _current = null;
}
