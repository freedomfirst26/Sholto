namespace Sholto.App;

/// <summary>The crossfader. One of the four real roles extracted from the old
/// <c>IDeckHost</c> — see the split plan in <c>~/Projects/sholto.md</c>.
/// <see cref="ViewModels.MainViewModel"/> implements this directly.</summary>
public interface IMixer
{
    double Crossfader { get; set; }
}
