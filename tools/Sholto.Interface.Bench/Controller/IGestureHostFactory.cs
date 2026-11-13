namespace Sholto.Interface.Bench.Controller;

/// <summary>Builds a <see cref="GestureHost"/> over a fresh scripted surface and manual clock.</summary>
public interface IGestureHostFactory
{
    /// <summary>A host whose clock reads wall time, for runs that pass real time between steps.</summary>
    GestureHost Create();

    /// <summary>A host whose clock starts at a fixed instant and moves only when the caller advances it,
    /// for an offline render that ticks it by the audio each step renders.</summary>
    GestureHost CreateForRender();
}
