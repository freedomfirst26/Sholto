using System.Diagnostics;

namespace Sholto.Audio;

/// <summary>
/// Linux/PipeWire-only helper for the dual-sound-card master routing scenario:
/// the DDJ-FLX4 stays open as the 4ch playback device (master 1-2 + headphone
/// cue 3-4 — see <see cref="AudioEngine"/>), and this class re-links just the
/// master FL/FR ports to a separate speaker sink via the <c>pw-link</c> CLI so
/// master plays on a real speaker card while cue keeps coming out the FLX4.
/// RL/RR are never touched.
///
/// Shells out to <c>pw-link</c>/<c>pactl</c> rather than binding libpipewire —
/// this is a small, occasional, best-effort operation, not a hot path. Every
/// method here is guarded: a missing tool, an absent sink, or a shell failure
/// degrades to "master stays on the FLX4" and is logged, never thrown.
/// </summary>
public static class PipeWireRouter
{
    /// <summary>True if the node/description names look like the DDJ-FLX4.
    /// Matches on "DDJ-FLX4" or "AlphaTheta" (the manufacturer) rather than a
    /// full node name — the node name embeds the unit's serial number, which
    /// differs per controller.</summary>
    public static bool IsFlx4(string? nodeOrDesc) =>
        nodeOrDesc is not null &&
        (nodeOrDesc.Contains("DDJ-FLX4", StringComparison.OrdinalIgnoreCase) ||
         nodeOrDesc.Contains("AlphaTheta", StringComparison.OrdinalIgnoreCase));

    /// <summary>True if <c>pw-link</c> is on PATH. Everything else here no-ops
    /// (returns null/false/empty) when this is false.</summary>
    public static bool IsAvailable()
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo("pw-link", "--version")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            });
            if (p is null) return false;
            p.WaitForExit(2000);
            return p.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Parses the <c>Name:</c>/<c>Description:</c> lines out of
    /// `pactl list sinks` text. Pure and side-effect-free so it's unit
    /// testable without a live PipeWire session — pass it captured output.</summary>
    public static IReadOnlyList<(string Node, string Desc)> ParseSinks(string pactlListSinksOutput)
    {
        var sinks = new List<(string Node, string Desc)>();
        string? pendingNode = null;
        foreach (var rawLine in pactlListSinksOutput.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.StartsWith("Name:", StringComparison.Ordinal))
            {
                pendingNode = line["Name:".Length..].Trim();
            }
            else if (line.StartsWith("Description:", StringComparison.Ordinal) && pendingNode is not null)
            {
                sinks.Add((pendingNode, line["Description:".Length..].Trim()));
                pendingNode = null; // one Name→Description pair consumed per sink block
            }
        }
        return sinks;
    }

    /// <summary>All non-FLX4 playback sinks — i.e. the master-speaker choices
    /// the user is allowed to pick from. Returns empty (never throws) if
    /// <c>pactl</c> isn't available.</summary>
    public static IReadOnlyList<(string Node, string Desc)> EnumerateSpeakerSinks()
    {
        var text = RunCapture("pactl", "list sinks");
        if (text is null) return Array.Empty<(string, string)>();
        return ParseSinks(text).Where(s => !IsFlx4(s.Node) && !IsFlx4(s.Desc)).ToList();
    }

    /// <summary>The FLX4's PipeWire sink node name, or null if it isn't
    /// currently connected/enumerated by PipeWire.</summary>
    public static string? FindFlx4Sink()
    {
        var text = RunCapture("pactl", "list sinks");
        if (text is null) return null;
        var match = ParseSinks(text).FirstOrDefault(s => IsFlx4(s.Node) || IsFlx4(s.Desc));
        return match.Node;
    }

    /// <summary>Re-link Sholto's master FL/FR output ports from the FLX4 to
    /// <paramref name="speakerNode"/>. RL/RR (cue) are left connected to the
    /// FLX4. Polls for Sholto's ports for ~3s since they only exist once the
    /// device is actively streaming (call this after <c>Start()</c>).
    /// Never throws; returns false + a log line on any failure.</summary>
    public static bool ApplyMasterRoute(string flx4Node, string speakerNode, out string log)
    {
        var logLines = new List<string>();
        void Log(string s) { Console.WriteLine($"[PipeWire] {s}"); logLines.Add(s); }

        if (!IsAvailable())
        {
            Log("pw-link not on PATH — master stays on FLX4");
            log = string.Join("; ", logLines);
            return false;
        }

        var (fl, fr) = FindSholtoOutputPorts();
        if (fl is null || fr is null)
        {
            Log("Sholto's PipeWire output ports never appeared — master stays on FLX4");
            log = string.Join("; ", logLines);
            return false;
        }

        bool ok = true;
        foreach (var (port, chan) in new[] { (fl, "FL"), (fr, "FR") })
        {
            // Disconnect from the FLX4 first. Ignore its exit code: it's a
            // (harmless) non-zero no-op if the port wasn't linked there.
            Run("pw-link", $"-d \"{port}\" \"{flx4Node}:playback_{chan}\"");
            if (Run("pw-link", $"\"{port}\" \"{speakerNode}:playback_{chan}\""))
            {
                Log($"{port} -> {speakerNode}:playback_{chan}");
            }
            else
            {
                Log($"failed to link {port} -> {speakerNode}:playback_{chan}");
                ok = false;
            }
        }
        log = string.Join("; ", logLines);
        return ok;
    }

    /// <summary>Relink master FL/FR back onto the FLX4 (undo
    /// <see cref="ApplyMasterRoute"/>). We don't track which speaker sink
    /// master was last routed to, so this best-effort disconnects from every
    /// currently-enumerated speaker sink before relinking to the FLX4.</summary>
    public static void ResetMasterRoute(string flx4Node)
    {
        if (!IsAvailable()) return;
        var (fl, fr) = FindSholtoOutputPorts();
        if (fl is null || fr is null)
        {
            Console.WriteLine("[PipeWire] reset: Sholto's output ports not found — nothing to relink");
            return;
        }

        var speakers = EnumerateSpeakerSinks();
        foreach (var (port, chan) in new[] { (fl, "FL"), (fr, "FR") })
        {
            foreach (var sink in speakers)
                Run("pw-link", $"-d \"{port}\" \"{sink.Node}:playback_{chan}\"");
            if (Run("pw-link", $"\"{port}\" \"{flx4Node}:playback_{chan}\""))
                Console.WriteLine($"[PipeWire] {port} -> {flx4Node}:playback_{chan} (reset to FLX4)");
        }
    }

    /// <summary>Finds Sholto's own output FL/FR ports via <c>pw-link -o</c>.
    /// These only exist while the playback device is actively streaming, so
    /// this polls for up to ~3s. Tolerant of the port suffix being
    /// "output_FL"/"output_FR" or "playback_FL"/"playback_FR" — only the
    /// trailing "_FL"/"_FR" and a case-insensitive "sholto" in the client name
    /// are required.</summary>
    private static (string? FL, string? FR) FindSholtoOutputPorts()
    {
        const int attempts = 10;
        const int delayMs = 300; // 10 × 300ms ≈ 3s total
        for (int attempt = 0; attempt < attempts; attempt++)
        {
            var text = RunCapture("pw-link", "-o");
            if (text is not null)
            {
                string? fl = null, fr = null;
                foreach (var rawLine in text.Split('\n'))
                {
                    var line = rawLine.Trim();
                    if (!line.Contains("sholto", StringComparison.OrdinalIgnoreCase)) continue;
                    if (line.EndsWith("_FL", StringComparison.Ordinal)) fl = line;
                    else if (line.EndsWith("_FR", StringComparison.Ordinal)) fr = line;
                }
                if (fl is not null && fr is not null) return (fl, fr);
            }
            if (attempt < attempts - 1) Thread.Sleep(delayMs);
        }
        return (null, null);
    }

    /// <summary>Runs a command, ignoring stdout, returning whether it exited 0.</summary>
    private static bool Run(string exe, string args)
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo(exe, args)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            });
            if (p is null) return false;
            p.WaitForExit(3000);
            return p.ExitCode == 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PipeWire] '{exe} {args}' failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>Runs a command and returns its captured stdout, or null on failure.</summary>
    private static string? RunCapture(string exe, string args)
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo(exe, args)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            });
            if (p is null) return null;
            string stdout = p.StandardOutput.ReadToEnd();
            p.WaitForExit(5000);
            return stdout;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PipeWire] '{exe} {args}' failed: {ex.Message}");
            return null;
        }
    }
}
