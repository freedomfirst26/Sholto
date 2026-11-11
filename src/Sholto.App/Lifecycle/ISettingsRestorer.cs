using Sholto.Data;

namespace Sholto.App.Lifecycle;

/// <summary>Restores and saves the theme, the waveform style and the backspin time and distance.</summary>
public interface ISettingsRestorer : ICommandHandler<ChooseTheme>, ICommandHandler<ChooseWaveformStyle>
{
    /// <summary>Starts (each on its own task, fire-and-forget) the theme restore, then the waveform style
    /// restore, then subscribes to the backspin time and distance choices and starts their restores.</summary>
    void Start();
}
