using Sholto.App.Analysis.Stems;
using Xunit;

namespace Sholto.App.Audio.Tests;

public class StemMixLoopRegionTests
{
    private const int Shifts = 1_000_000;
    private const long Length = 1_000;   // every written region is (n, n + Length)

    [Fact]
    public void A_reader_never_sees_a_loop_start_paired_with_another_regions_end()
    {
        var provider = new StemMixDataProvider(new StemSamples([], [], [], []), 48000);
        provider.SetLoop(new LoopRegion(0, Length));

        bool done = false;
        long reads = 0, torn = 0;
        var reader = new Thread(() =>
        {
            while (!Volatile.Read(ref done))
            {
                if (provider.ActiveLoop is { } r)
                {
                    reads++;
                    if (r.EndSample - r.StartSample != Length) torn++;
                }
            }
        });
        reader.Start();

        // Writer: ShiftLoop-style, every write is a whole (n, n + Length) region.
        for (long n = 2; n < 2L * Shifts; n += 2)
            provider.SetLoop(new LoopRegion(n, n + Length));

        Volatile.Write(ref done, true);
        reader.Join();

        Assert.True(reads > 0, "reader never observed a loop");
        Assert.Equal(0, torn);
    }
}
