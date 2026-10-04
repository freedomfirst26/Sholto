namespace Sholto.App.Library.Crates;

/// <summary>Crate creation, lookup and track membership used by the crate picker.</summary>
public interface ICrateService
{
    Task<int> CreateAsync(string name);

    Task<IReadOnlyList<CrateSummary>> SearchAsync(string query);

    Task<bool> AddTrackAsync(int crateId, Guid trackId);

    Task<IReadOnlyList<Guid>> TrackIdsAsync(int crateId);
}
