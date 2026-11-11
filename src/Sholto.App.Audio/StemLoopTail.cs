namespace Sholto.App.Audio;

/// <summary>
/// Per-stem snapshot of the audio just past <c>loopEnd</c>, captured at
/// loop-engage time. Mixed into the first the loop fade length
/// frames after every wrap with a linear ramp so the wrap is inaudible
/// — at frame f, output = tail[f]·(1 − f/N) + freshAudio[f]·(f/N). At
/// f=0 the listener hears the natural continuation past loopEnd (which
/// IS continuous with the last pre-wrap sample), at f=N they hear pure
/// loop content. Modeled on Ardour's <c>setup_preloop_buffer</c> +
/// <c>maybe_xfade_loop</c> and Mixxx's <c>ReadAheadManager</c>
/// crossfade. Both fields immutable; published as a single reference
/// so the audio thread sees them atomically.
/// </summary>
internal sealed class StemLoopTail(float[][] stems, int frames)
{
    public float[][] Stems { get; } = stems;   // [4][frames*2], interleaved stereo per stem
    public int Frames { get; } = frames;       // actual frames captured (may be < the loop fade length near track end)
}
