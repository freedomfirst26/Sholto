using Sholto.Data;

namespace Sholto.Interface.MainUI.ViewModels;

/// <summary>A faked, representative track's waveform for the Layout Wizard's preview cards: about two
/// minutes at 128 BPM with intro, groove, build, breakdown, gap, drop and outro, so each style shows how
/// it draws quiet and loud, bassy and bright sections. Same shape the analyzer produces.</summary>
public interface IDemoWaveformFactory
{
    /// <summary>The demo peaks. Deterministic, built on first use and the same instance every call.</summary>
    WaveformPeaks Peaks { get; }

    /// <summary>The demo's tempo.</summary>
    double Bpm { get; }
}
