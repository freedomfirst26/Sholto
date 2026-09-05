using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Sholto.Audio;

/// <summary>M4A/AAC via an external ffmpeg process. No in-process decoder in the
/// current dependency set handles AAC on Linux: NAudio's AAC path needs
/// MediaFoundation (Windows-only), and SoundFlow/miniaudio has no AAC decoder either.
/// ffmpeg is already a required runtime dependency (installed by install.sh for the
/// beat detector), so this shells out to it rather than pulling in a new library —
/// the same subprocess pattern <c>MadmomBeatAnalyzer</c> uses for its external tool.
/// ffmpeg decodes and resamples straight to the target format, so its stdout is
/// already 48 kHz stereo float32 with no further conversion needed.</summary>
public sealed class FfmpegDecodeStrategy : IAudioDecodeStrategy
{
    public bool CanDecode(string extension) =>
        extension is ".m4a" or ".aac" or ".mp4";

    public float[] Decode(string filePath)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        psi.ArgumentList.Add("-v");
        psi.ArgumentList.Add("error");
        psi.ArgumentList.Add("-nostdin");
        psi.ArgumentList.Add("-i");
        psi.ArgumentList.Add(filePath);
        psi.ArgumentList.Add("-vn");
        psi.ArgumentList.Add("-f");
        psi.ArgumentList.Add("f32le");
        psi.ArgumentList.Add("-ac");
        psi.ArgumentList.Add(AudioFileDecoder.TargetChannels.ToString());
        psi.ArgumentList.Add("-ar");
        psi.ArgumentList.Add(AudioFileDecoder.TargetSampleRate.ToString());
        psi.ArgumentList.Add("-");

        Process proc;
        try
        {
            proc = Process.Start(psi)!;
        }
        catch (Win32Exception ex)
        {
            throw new InvalidOperationException(
                "Couldn't launch ffmpeg to decode this M4A/AAC file. Install ffmpeg " +
                "(run install.sh, or `sudo apt install ffmpeg`) and try again.", ex);
        }

        using (proc)
        {
            // Read stderr on its own task so a chatty stderr pipe can't fill up and
            // deadlock against stdout while we're still reading it.
            var stderrTask = proc.StandardError.ReadToEndAsync();

            using var stdout = proc.StandardOutput.BaseStream;
            using var buffer = new MemoryStream();
            stdout.CopyTo(buffer);

            string stderr = stderrTask.GetAwaiter().GetResult();
            proc.WaitForExit();

            if (proc.ExitCode != 0)
                throw new InvalidOperationException(
                    $"ffmpeg failed to decode '{filePath}' (exit {proc.ExitCode}): {stderr}");

            byte[] bytes = buffer.GetBuffer();
            int byteCount = (int)buffer.Length;
            // Trim to a whole number of floats before reinterpreting.
            int floatCount = byteCount / sizeof(float);
            return MemoryMarshal.Cast<byte, float>(bytes.AsSpan(0, floatCount * sizeof(float))).ToArray();
        }
    }
}
