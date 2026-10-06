using Sholto.App.Library;
using Sholto.Data;

namespace Sholto.App.Loading;

/// <summary>See <see cref="ISearchPick"/>. App thread only.</summary>
public sealed class SearchPick(ILibrarySession library) : ISearchPick
{
    private readonly ILibrarySession _library = library;
    private string? _filePath;

    public bool Active { get; private set; }

    public Track? PickedTrack =>
        Active && _filePath is { } path ? _library.SummaryFor(path)?.ToTrack() : null;

    public void Handle(in SetSearchPick command)
    {
        Active = command.Active;
        _filePath = command.FilePath;
    }
}
