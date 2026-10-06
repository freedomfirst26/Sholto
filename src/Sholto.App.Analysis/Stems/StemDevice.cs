namespace Sholto.App.Analysis.Stems;

/// <summary>The device demucs separates stems on.</summary>
public enum StemDevice
{
    /// <summary>CPU: saturates every core for 30-180 s, so it must not overlap other analysis.</summary>
    Cpu,

    /// <summary>CUDA: ~8 s for a 60 s clip and almost no CPU, so it can overlap other analysis.</summary>
    Cuda,
}
