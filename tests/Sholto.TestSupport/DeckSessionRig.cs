using Sholto.App.Analysis.Analyzers.Segments;
using Sholto.App.Audio;
using Sholto.Data;
using Sholto.App.Decks;

namespace Sholto.TestSupport;

/// <summary>A deck session over the given ports, on a settable clock, an immediate app thread and a real
/// bus, built through the production <see cref="DeckSessionFactory"/>.</summary>
internal sealed class DeckSessionRig
{
    public DeckSessionRig(IDeckPorts ports)
    {
        Ports = ports;
        Session = new DeckSessionFactory(
            new FixedDeckFactory(ports), new PhraseSectionAnalyzer(new BarFeatureExtractor(new PhraseSectionOptions()), new PhraseSectionLabeler(new PhraseSectionOptions()), new PhraseSectionOptions()), Clock, new ImmediateAppThread(), Bus).Create(0);
    }

    public IDeckPorts Ports { get; }

    public SettableFrameClock Clock { get; } = new();

    public DataBus Bus { get; } = new(new ThrowingFailureSink());

    public IDeckSession Session { get; }
}
