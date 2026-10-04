using Microsoft.Extensions.Options;
using Sholto.App.Analysis.Analyzers.Waveform;
using Sholto.Data;
using Sholto.Interface.MainUI.Theming;

namespace Sholto.Interface.MainUI.ViewModels;

/// <summary>Holds the collaborators every <see cref="DeckViewModel"/> shares (the bus, the theme, the
/// display options) and creates one per call for the deck it is handed.</summary>
public sealed class DeckViewModelFactory(
    IEventSubscriber subscriber,
    ICommandSender sender,
    IThemeContext theme,
    IOptions<FeatureOptions> features,
    IWaveformPeaksFactory peaksFactory) : IDeckViewModelFactory
{
    private readonly IEventSubscriber _subscriber = subscriber;
    private readonly ICommandSender _sender = sender;
    private readonly IThemeContext _theme = theme;
    private readonly IOptions<FeatureOptions> _features = features;
    private readonly IWaveformPeaksFactory _peaksFactory = peaksFactory;

    public DeckViewModel Create(int deck) =>
        new(deck, _subscriber, _sender, _theme, _peaksFactory)
        {
            SectionMapEnabled = _features.Value.ShowSectionMap
        };
}
