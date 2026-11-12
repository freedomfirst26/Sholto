namespace Sholto.App.Audio.Golden;

/// <summary>Any exception fails an xunit test — this one just carries a
/// message diagnosable enough to act on without re-running anything.</summary>
internal sealed class GoldenAssertionException(string message) : Exception(message);

/// <summary>
/// Compares a freshly-rendered WAV against a checked-in reference and turns a
/// mismatch into a diagnosable message — sample index, time, peak deviation,
/// mismatch count — instead of a bare pass/fail. A hash-only comparison was
/// explicitly ruled out (see the test class docs): it tells you THAT audio
/// changed, never WHERE or by HOW MUCH.
/// </summary>
internal static class GoldenReference
{
    /// <summary>Resolves to "Fixtures" next to whichever source file calls
    /// this (via <see cref="System.Runtime.CompilerServices.CallerFilePathAttribute"/>)
    /// — i.e. the checked-in source tree, not wherever the test binaries were
    /// copied to. So SHOLTO_GOLDEN_REGEN writes land where `git status` will
    /// see them, regardless of build output layout.</summary>
    public static string FixturesDir([System.Runtime.CompilerServices.CallerFilePath] string here = "") =>
        Path.Combine(Path.GetDirectoryName(here)!, "Fixtures");


    /// <summary>Set <c>SHOLTO_GOLDEN_REGEN=1</c> to overwrite every reference
    /// this run touches with the freshly-rendered audio, instead of comparing.
    /// This is NOT a "fix the test" escape hatch — regenerating a reference is
    /// an assertion that the audio changed ON PURPOSE. Only ever do it after
    /// listening to (or otherwise independently verifying) the new render, and
    /// say so in the commit that updates the fixture.</summary>
    private static bool RegenMode =>
        Environment.GetEnvironmentVariable("SHOLTO_GOLDEN_REGEN") == "1";

    /// <summary>
    /// Largest |reference − rendered| per sample that <see cref="AssertMatchesWithin"/>
    /// accepts for the DjFilter render. DjFilter is the only golden path that calls
    /// libm (MathF.Tanh per sample, MathF.Tan/Exp/Log for coefficients), and libm
    /// results may move by an ulp between glibc versions, so filter.wav cannot stay
    /// bit-exact across machines. Measured on glibc 2.39 by temporarily rewriting
    /// DjFilter's libm calls (max over filter.wav and DeckEffectOutputPinTests' filter
    /// case): correctly rounded results (Math.* in double, cast to float) move the
    /// output by at most 1.2e-7, and every result nudged by 1 ulp (up, down or
    /// alternating) by at most 9.3e-7. This bound is about 100× that. The cutoff
    /// coefficient off by 1% moves it by at least 1.3e-3, over 10× this bound, so a
    /// real filter change still fails. 1e-4 is −80 dBFS.
    /// </summary>
    public const float LibmFilterTolerance = 1e-4f;

    /// <summary>Render <paramref name="renderedPath"/> against
    /// <paramref name="fixturesDir"/>/<paramref name="referenceName"/>.
    /// Exact float32 equality — see the test class docs for why exactness is
    /// the right default here.</summary>
    public static void AssertMatches(string fixturesDir, string referenceName, string renderedPath) =>
        Compare(fixturesDir, referenceName, renderedPath, tolerance: 0f);

    /// <summary>As <see cref="AssertMatches"/>, but a sample passes when
    /// |reference − rendered| ≤ <paramref name="tolerance"/>. Only for a render
    /// whose maths depends on libm (see <see cref="LibmFilterTolerance"/>);
    /// everything else stays exact.</summary>
    public static void AssertMatchesWithin(string fixturesDir, string referenceName, string renderedPath, float tolerance) =>
        Compare(fixturesDir, referenceName, renderedPath, tolerance);

    private static void Compare(string fixturesDir, string referenceName, string renderedPath, float tolerance)
    {
        string referencePath = Path.Combine(fixturesDir, referenceName);

        if (RegenMode)
        {
            Directory.CreateDirectory(fixturesDir);
            File.Copy(renderedPath, referencePath, overwrite: true);
            Console.WriteLine($"[SHOLTO_GOLDEN_REGEN] wrote {referencePath} ({new FileInfo(referencePath).Length} bytes) — " +
                               "this asserts the audio changed intentionally.");
            return;
        }

        if (!File.Exists(referencePath))
            throw new GoldenAssertionException(
                $"no reference at {referencePath}. If this is a new test, run once with " +
                "SHOLTO_GOLDEN_REGEN=1 to create it, listen to the render, and check it in.");

        var (refRate, refChannels, refSamples) = WavFloatReader.Read(referencePath);
        var (gotRate, gotChannels, gotSamples) = WavFloatReader.Read(renderedPath);

        if (refRate != gotRate || refChannels != gotChannels)
            throw new GoldenAssertionException(
                $"{referenceName}: format mismatch — reference {refRate}Hz/{refChannels}ch, " +
                $"rendered {gotRate}Hz/{gotChannels}ch");

        if (refSamples.Length != gotSamples.Length)
            throw new GoldenAssertionException(
                $"{referenceName}: length mismatch — reference {refSamples.Length} samples " +
                $"({FramesOf(refSamples.Length, refChannels, refRate):F3}s), rendered {gotSamples.Length} samples " +
                $"({FramesOf(gotSamples.Length, gotChannels, gotRate):F3}s)");

        int firstDiff = -1;
        int diffCount = 0;
        float peakDeviation = 0f;
        int peakDiffIndex = -1;

        for (int i = 0; i < refSamples.Length; i++)
        {
            if (refSamples[i] == gotSamples[i]) continue;
            float dev = Math.Abs(refSamples[i] - gotSamples[i]);
            if (dev <= tolerance) continue;
            if (firstDiff < 0) firstDiff = i;
            diffCount++;
            if (!(dev <= peakDeviation)) { peakDeviation = dev; peakDiffIndex = i; }
        }

        if (diffCount == 0) return;

        int firstDiffFrame = firstDiff / refChannels;
        double firstDiffSeconds = firstDiffFrame / (double)refRate;
        int peakDiffFrame = peakDiffIndex / refChannels;
        double peakDiffSeconds = peakDiffFrame / (double)refRate;
        double percent = 100.0 * diffCount / refSamples.Length;

        throw new GoldenAssertionException(
            $"{referenceName}: {diffCount} of {refSamples.Length} samples differ by more than {tolerance:G9} ({percent:F4}%). " +
            $"First difference at sample {firstDiff} (frame {firstDiffFrame}, t={firstDiffSeconds:F6}s): " +
            $"reference={refSamples[firstDiff]:G9} rendered={gotSamples[firstDiff]:G9}. " +
            $"Peak |deviation|={peakDeviation:G9} at sample {peakDiffIndex} (frame {peakDiffFrame}, t={peakDiffSeconds:F6}s): " +
            $"reference={refSamples[peakDiffIndex]:G9} rendered={gotSamples[peakDiffIndex]:G9}.");
    }

    private static double FramesOf(int sampleCount, int channels, int sampleRate) =>
        channels == 0 || sampleRate == 0 ? 0 : sampleCount / (double)channels / sampleRate;
}
