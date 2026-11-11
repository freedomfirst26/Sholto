using Avalonia.Media;
using Sholto.Interface.MainUI.Controls.WaveformStyles;
using Sholto.Interface.MainUI.Theming;
using SkiaSharp;

namespace Sholto.Interface.MainUI.Controls;

internal sealed class WaveformPaletteSync : IWaveformPaletteSync
{
    private WaveformPalette? _baked;
    private WaveformPalette? _penFor;
    private IPen? _pen;

    public void MarkBaked(WaveformPalette palette) => _baked = palette;

    public bool NeedsRebake(WaveformPalette next, IWaveformStyleStrategy? style) =>
        _baked is null || style is null || !style.BakesSameColours(_baked, next);

    public SKColor ToSk(Color c) => new(c.R, c.G, c.B, c.A);

    public SKColor LoopColour(WaveformPalette p, bool gridNudged) =>
        gridNudged ? ToSk(p.GridEdit).WithAlpha(0xA0) : ToSk(p.Loop);   // red once the grid was nudged

    public WaveformLiveColours LiveColours(WaveformPalette p) =>
        new(ToSk(p.Playhead), ToSk(p.BeatTick), ToSk(p.Marker),
            ToSk(p.Vocal).WithAlpha(0xD0), ToSk(p.VocalInactive).WithAlpha(0xB0), ToSk(p.SnapGlow).WithAlpha(0xF0), ToSk(p.Edge).WithAlpha(0x99));

    public IPen GridEditPen(WaveformPalette p)
    {
        if (_pen is null || !ReferenceEquals(_penFor, p))
        {
            _pen = new Pen(new SolidColorBrush(Color.FromArgb(0xE0, p.GridEdit.R, p.GridEdit.G, p.GridEdit.B)), 2);
            _penFor = p;
        }
        return _pen;
    }
}
