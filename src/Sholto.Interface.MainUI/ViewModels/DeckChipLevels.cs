namespace Sholto.Interface.MainUI.ViewModels;

public sealed class DeckChipLevels(double minOpacity) : IDeckChipLevels
{
    private bool _drumsActive = true, _vocalsActive = true, _instrumentalActive = true;
    private double _drumsLevel = 1.0, _vocalsLevel = 1.0, _instrumentalLevel = 1.0;

    public double MinOpacity { get; } = minOpacity;

    public bool DrumsActive => _drumsActive;
    public bool VocalsActive => _vocalsActive;
    public bool InstrumentalActive => _instrumentalActive;

    public double DrumsChipOpacity => Opacity(_drumsLevel, _drumsActive);
    public double VocalsChipOpacity => Opacity(_vocalsLevel, _vocalsActive);
    public double InstrumentalChipOpacity => Opacity(_instrumentalLevel, _instrumentalActive);

    public bool SetActive(int stem, bool active)
    {
        switch (stem)
        {
            case 0: _drumsActive = active; return true;
            case 1: _vocalsActive = active; return true;
            case 2: _instrumentalActive = active; return true;
            default: return false;
        }
    }

    public bool SetLevel(int stem, double level)
    {
        switch (stem)
        {
            case 0: return Set(ref _drumsLevel, _drumsActive, level);
            case 1: return Set(ref _vocalsLevel, _vocalsActive, level);
            case 2: return Set(ref _instrumentalLevel, _instrumentalActive, level);
            default: return false;
        }
    }

    private bool Set(ref double field, bool active, double level)
    {
        var before = Opacity(field, active);
        field = level;
        return Opacity(field, active) != before;
    }

    private double Opacity(double level, bool active) =>
        !active ? 1.0 : MinOpacity + (1.0 - MinOpacity) * Math.Clamp(level, 0.0, 1.0);
}
