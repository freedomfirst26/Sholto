using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Sholto.Bench.Sound;

/// <summary>
/// Turns a WAV file into numbers via ffmpeg's analysis filters, so an agent can
/// assert on values instead of reading a log. One ffmpeg invocation runs
/// <c>astats</c> (per-channel RMS/peak), <c>ebur128</c> (integrated loudness +
/// true peak) and <c>silencedetect</c> (gaps) together with <c>-f null -</c>;
/// ffmpeg writes all filter output to stderr, which is parsed here.
/// </summary>
public static partial class FfmpegMeasurer
{
    public static SoundMeasurement Measure(string wavPath, double silenceThresholdDb = -50, double silenceMinDuration = 0.3)
    {
        string args = $"-hide_banner -nostats -i \"{wavPath}\" " +
                      $"-af \"astats=metadata=0,ebur128=peak=true,silencedetect=noise={silenceThresholdDb}dB:d={silenceMinDuration}\" " +
                      "-f null -";

        string stderr = RunFfmpeg(args);
        return ParseStderr(wavPath, stderr);
    }

    /// <summary>Parsing split out from process execution so
    /// <c>Sholto.Bench.Tests</c> can exercise the regexes against a captured
    /// fixture without shelling out to ffmpeg — no audio hardware or ffmpeg
    /// binary needed to run that test.</summary>
    public static SoundMeasurement ParseStderr(string wavPath, string stderr) => new()
    {
        WavPath = wavPath,
        DurationSeconds = ParseDuration(stderr),
        Loudness = ParseLoudness(stderr),
        Channels = ParseChannels(stderr),
        Silences = ParseSilences(stderr),
    };

    private static string RunFfmpeg(string args)
    {
        var psi = new ProcessStartInfo("ffmpeg", args)
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start ffmpeg — is it installed and on PATH?");
        var stderr = new StringBuilder();
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) stderr.AppendLine(e.Data); };
        process.BeginErrorReadLine();
        process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return stderr.ToString();
    }

    [GeneratedRegex(@"Duration:\s*(\d+):(\d+):(\d+(?:\.\d+)?)")]
    private static partial Regex DurationRegex();

    private static double? ParseDuration(string stderr)
    {
        var m = DurationRegex().Match(stderr);
        if (!m.Success) return null;
        double h = double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
        double min = double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
        double s = double.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture);
        return h * 3600 + min * 60 + s;
    }

    [GeneratedRegex(@"Integrated loudness:\s*\n\s*I:\s*(-?[\d.]+|-?inf)\s*LUFS", RegexOptions.Singleline)]
    private static partial Regex IntegratedRegex();

    [GeneratedRegex(@"Loudness range:\s*\n\s*LRA:\s*(-?[\d.]+|-?inf)\s*LU", RegexOptions.Singleline)]
    private static partial Regex LraRegex();

    [GeneratedRegex(@"True peak:\s*\n\s*Peak:\s*(-?[\d.]+|-?inf)\s*dBFS", RegexOptions.Singleline)]
    private static partial Regex TruePeakRegex();

    private static LoudnessResult ParseLoudness(string stderr) => new()
    {
        IntegratedLufs = ParseDb(IntegratedRegex().Match(stderr)),
        LoudnessRangeLu = ParseDb(LraRegex().Match(stderr)),
        TruePeakDbfs = ParseDb(TruePeakRegex().Match(stderr)),
    };

    private static double? ParseDb(Match m)
    {
        if (!m.Success) return null;
        string v = m.Groups[1].Value;
        if (v.Contains("inf", StringComparison.OrdinalIgnoreCase))
            return v.StartsWith('-') ? double.NegativeInfinity : double.PositiveInfinity;
        return double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : null;
    }

    // astats prints one "Channel: N" block per channel, each followed by its
    // metrics, then repeats the same field names once more under "Overall" —
    // so each block is bounded by the next "Channel:"/"Overall" header.
    [GeneratedRegex(@"Channel:\s*(\d+)\s*\n(.*?)(?=\nChannel:|\nOverall|\z)", RegexOptions.Singleline)]
    private static partial Regex ChannelBlockRegex();

    [GeneratedRegex(@"Peak level dB:\s*(-?[\d.]+|-?inf)")]
    private static partial Regex PeakLevelRegex();

    [GeneratedRegex(@"RMS level dB:\s*(-?[\d.]+|-?inf)")]
    private static partial Regex RmsLevelRegex();

    private static IReadOnlyList<ChannelStats> ParseChannels(string stderr)
    {
        // Strip the per-line ffmpeg log prefix so the block regex's "\n" boundaries
        // line up with content rather than "[Parsed_astats_0 @ 0x...] ".
        string cleaned = LinePrefixRegex().Replace(stderr, "");
        var results = new List<ChannelStats>();
        foreach (Match m in ChannelBlockRegex().Matches(cleaned))
        {
            int channel = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
            string block = m.Groups[2].Value;
            results.Add(new ChannelStats
            {
                Channel = channel,
                PeakLevelDb = ParseDb(PeakLevelRegex().Match(block)),
                RmsLevelDb = ParseDb(RmsLevelRegex().Match(block)),
            });
        }
        return results;
    }

    [GeneratedRegex(@"^\[[^\]]+\]\s?", RegexOptions.Multiline)]
    private static partial Regex LinePrefixRegex();

    [GeneratedRegex(@"silence_start:\s*(-?[\d.]+)")]
    private static partial Regex SilenceStartRegex();

    [GeneratedRegex(@"silence_end:\s*(-?[\d.]+)\s*\|\s*silence_duration:\s*(-?[\d.]+)")]
    private static partial Regex SilenceEndRegex();

    private static IReadOnlyList<SilenceInterval> ParseSilences(string stderr)
    {
        var starts = new Queue<double>(
            SilenceStartRegex().Matches(stderr)
                .Select(m => double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)));
        var ends = SilenceEndRegex().Matches(stderr)
            .Select(m => (
                End: double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture),
                Duration: double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture)))
            .ToList();

        var results = new List<SilenceInterval>();
        int startCount = starts.Count;
        for (int i = 0; i < startCount; i++)
        {
            double start = starts.Dequeue();
            if (i < ends.Count)
                results.Add(new SilenceInterval { StartSeconds = start, EndSeconds = ends[i].End, DurationSeconds = ends[i].Duration });
            else
                // Trailing silence to end-of-file never gets a silence_end line.
                results.Add(new SilenceInterval { StartSeconds = start, EndSeconds = null, DurationSeconds = null });
        }
        return results;
    }
}
