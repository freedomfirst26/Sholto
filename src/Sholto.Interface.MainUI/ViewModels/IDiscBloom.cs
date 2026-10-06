namespace Sholto.Interface.MainUI.ViewModels;

/// <summary>The disc bloom's smoothing state: band energies at the playhead go in once per frame, glow
/// opacities come out.</summary>
public interface IDiscBloom : IDiscBloomLevels
{
    /// <summary>Move the glows toward the given band energies (0 to 1), by the time since the last call
    /// (fast attack, slower release). Allocates nothing.</summary>
    void Advance(float low, float mid, float high);
}
