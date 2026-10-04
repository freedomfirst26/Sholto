using Avalonia.Media;

namespace Sholto.Interface.MainUI.Theming;

/// <summary>Non-derivable default colours (<c>defaults.json</c>), a partial theme looked up
/// by dotted key such as <c>waveform.background</c> after the theme itself and before derivation.</summary>
public interface IThemeDefaults
{
    bool TryGetColor(string key, out Color color);
}
