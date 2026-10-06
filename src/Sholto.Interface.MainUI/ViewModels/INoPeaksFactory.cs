using Sholto.Data;

namespace Sholto.Interface.MainUI.ViewModels;

/// <summary>Supplies the placeholder peaks a deck shows before any analysis has landed.</summary>
public interface INoPeaksFactory
{
    /// <summary>Peaks that describe nothing renderable. The same instance every call.</summary>
    WaveformPeaks None();
}
