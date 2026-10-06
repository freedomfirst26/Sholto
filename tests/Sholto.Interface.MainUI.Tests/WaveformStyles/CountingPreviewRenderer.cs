using Avalonia.Media.Imaging;
using Sholto.App.Analysis.Analyzers.Waveform;
using Sholto.Interface.MainUI.Controls.WaveformStyles;
using Sholto.Interface.MainUI.Theming;
using Sholto.Interface.MainUI.ViewModels;
using Sholto.Data;

namespace Sholto.Interface.MainUI.Tests.WaveformStyles;

/// <summary>Records which styles were asked for a preview; draws nothing.</summary>
internal sealed class CountingPreviewRenderer : IWaveformPreviewRenderer
{
    private int _calls;
    public int Calls => Volatile.Read(ref _calls);

    public Bitmap? Render(IWaveformStyleStrategy style, WaveformPeaks peaks, WaveformPalette palette)
    {
        Interlocked.Increment(ref _calls);
        return null;
    }
}
