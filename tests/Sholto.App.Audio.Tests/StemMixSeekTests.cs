using Sholto.App.Analysis.Stems;
using Xunit;

namespace Sholto.App.Audio.Tests;

public class StemMixSeekTests
{
    private const int Seeks = 10_000;
    private const int BufferFrames = 64;
    private const int BufferSamples = BufferFrames * 2;
    private const int TrackFrames = 100_000;
    private const int FreeReads = 2;   // reads granted to run concurrently with each Seek

    // The reader parks once it has done `_limit` reads, so the seeker knows
    // exactly how many reads happened after each Seek returned.
    private long _reads;
    private long _limit;
    private bool _done;

    [Fact]
    public void A_seek_issued_while_the_reader_runs_is_never_lost()
    {
        var zeros = new float[TrackFrames * 2];
        var provider = new StemMixDataProvider(new StemSamples(zeros, zeros, zeros, zeros), 48000);

        var reader = new Thread(() =>
        {
            var buffer = new float[BufferSamples];
            var spin = new SpinWait();
            while (!Volatile.Read(ref _done))
            {
                if (Volatile.Read(ref _reads) >= Volatile.Read(ref _limit))
                {
                    spin.SpinOnce();
                    continue;
                }
                provider.ReadBytes(buffer);
                Volatile.Write(ref _reads, Volatile.Read(ref _reads) + 1);
            }
        });
        reader.Start();

        int wrong = 0;
        int firstWrong = -1;
        for (int i = 0; i < Seeks; i++)
        {
            // Frame-aligned targets, far apart, with room for every read below.
            int target = (i * 977 % 1500) * BufferSamples;

            // Let FreeReads reads run while Seek lands (possibly mid-buffer),
            // then grant one more so the seek is guaranteed to be consumed.
            long start = Volatile.Read(ref _reads);
            Volatile.Write(ref _limit, start + FreeReads);
            provider.Seek(target);
            Volatile.Write(ref _limit, start + FreeReads + 1);
            Wait(start + FreeReads + 1);

            // The seek was applied by one of the last 1..FreeReads+1 reads; every
            // read after it advanced exactly one buffer. A lost seek leaves the
            // position near the previous target, outside this window.
            int advanced = provider.Position - target;
            bool ok = advanced > 0 && advanced <= (FreeReads + 1) * BufferSamples && advanced % BufferSamples == 0;
            if (!ok && wrong++ == 0) firstWrong = i;
        }

        Volatile.Write(ref _done, true);
        reader.Join();

        Assert.True(wrong == 0, $"{wrong} of {Seeks} seeks lost or misplaced; first at seek {firstWrong}");
    }

    private void Wait(long reads)
    {
        var spin = new SpinWait();
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (Volatile.Read(ref _reads) < reads)
        {
            Assert.True(DateTime.UtcNow < deadline, "reader stalled");
            spin.SpinOnce();
        }
    }
}
