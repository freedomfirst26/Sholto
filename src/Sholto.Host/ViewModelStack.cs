using Microsoft.Extensions.Options;
using Sholto.App.Analysis.Analyzers.Segments;
using Sholto.App.Audio;
using Sholto.App.Library;
using Sholto.App;
using Sholto.Interface.MainUI.Theming;
using Sholto.Interface.MainUI.ViewModels;

using Sholto.Interface.MainUI;

namespace Sholto.Host;

/// <summary>The leaf collaborators <see cref="LiveEntities"/> needs to assemble the
/// real application entities — every one of them built by the composition root
/// (<c>App</c>), never here. Grouped into one class so handing them over is a
/// single readable call rather than a long argument list, and so adding a
/// <c>MainViewModel</c> dependency is a change to this class, not to
/// <see cref="ISholtoEntities"/>.</summary>
public sealed class ViewModelStack(
    IOptions<FeatureOptions> features,
    IDeckFactory decks,
    IThemeContext theme,
    IPhraseSectionAnalyzer songSegments,
    ITagRecency tagRecency,
    ILibrarySearch librarySearch,
    ICoreFactory coreFactory)
{
    public IOptions<FeatureOptions> Features { get; } = features;
    public IDeckFactory Decks { get; } = decks;
    public IThemeContext Theme { get; } = theme;
    public IPhraseSectionAnalyzer SongSegments { get; } = songSegments;
    public ITagRecency TagRecency { get; } = tagRecency;
    public ILibrarySearch LibrarySearch { get; } = librarySearch;
    /// <summary>Builds the headless core over the two deck sessions.</summary>
    public ICoreFactory CoreFactory { get; } = coreFactory;
}
