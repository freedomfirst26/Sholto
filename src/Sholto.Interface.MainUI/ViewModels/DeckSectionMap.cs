using Sholto.Data;

namespace Sholto.Interface.MainUI.ViewModels;

public sealed class DeckSectionMap(bool enabled) : IDeckSectionMap
{
    private DeckSectionsChanged _last = new(0, [], new DeckPhraseGrid(0, 8), 0, 0, 0);

    public bool Enabled { get; } = enabled;

    public void Apply(in DeckSectionsChanged e) => _last = e;

    public IReadOnlyList<DeckSection> Sections => _last.Sections;
    public DeckPhraseGrid PhraseGrid => _last.PhraseGrid;
    public double FirstDownbeatSec => _last.FirstDownbeatSec;
    public double BarPeriodSec => _last.BarPeriodSec;
    public int TotalBars => _last.TotalBars;
    public bool HasSections => _last.Sections.Count > 0 && _last.BarPeriodSec > 0;
    public bool Visible => Enabled && HasSections;
}
