using Sholto.Data;

namespace Sholto.Interface.MainUI.ViewModels;

/// <summary>One deck's song sections as of the last <see cref="DeckSectionsChanged"/>, and whether the
/// section-map strip shows.</summary>
public interface IDeckSectionMap
{
    void Apply(in DeckSectionsChanged e);

    /// <summary>Phrase-aware sections in bars; empty until basic analysis lands. Turn a bar into seconds
    /// with <see cref="FirstDownbeatSec"/> + bar * <see cref="BarPeriodSec"/>.</summary>
    IReadOnlyList<DeckSection> Sections { get; }

    /// <summary>Phrase lines every <c>PhraseBars</c> bars from <c>PhaseBar</c>.</summary>
    DeckPhraseGrid PhraseGrid { get; }

    /// <summary>Time of bar 0.</summary>
    double FirstDownbeatSec { get; }

    /// <summary>Seconds per bar; 0 when the track has no grid.</summary>
    double BarPeriodSec { get; }

    /// <summary>Whole bars in the track.</summary>
    int TotalBars { get; }

    /// <summary>Sections exist and there is a bar grid to place them on.</summary>
    bool HasSections { get; }

    /// <summary>Feature-flag gate for the section map (from <c>FeatureOptions</c>).</summary>
    bool Enabled { get; }

    /// <summary>The section-map strip is shown only when the feature flag is on AND sections have been
    /// analysed.</summary>
    bool Visible { get; }
}
