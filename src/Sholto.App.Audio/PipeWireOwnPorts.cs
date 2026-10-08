namespace Sholto.App.Audio;

/// <summary>Works out which PipeWire ports belong to this process. Under pipewire-pulse a stream's
/// node (and so its port prefix) is named after the libpulse application name, which defaults to the
/// process binary name (<c>dotnet</c>, <c>Sholto</c>, ...), so the ports cannot be found by name. The
/// process id is stable: <c>pactl list sink-inputs</c> names the node for our PID, and <c>pw-link -o</c>
/// lists that node's ports. Both methods are pure text parsing.</summary>
public sealed class PipeWireOwnPorts
{
    /// <summary>The <c>node.name</c> of the sink-input whose <c>application.process.id</c> is
    /// <paramref name="processId"/> in <c>pactl list sink-inputs</c> text, or null when there is none.</summary>
    public string? NodeNameForProcess(string pactlListSinkInputsText, int processId)
    {
        var blocks = pactlListSinkInputsText.Split(new[] { "Sink Input #" }, StringSplitOptions.None);
        foreach (var block in blocks)
        {
            string? pid = null, node = null;
            foreach (var rawLine in block.Split('\n'))
            {
                var line = rawLine.Trim();
                if (line.StartsWith("application.process.id", StringComparison.Ordinal)) pid = Value(line);
                else if (line.StartsWith("node.name", StringComparison.Ordinal)) node = Value(line);
            }
            if (node is not null && pid == processId.ToString()) return node;
        }
        return null;
    }

    /// <summary>The FL and FR ports of <paramref name="nodeName"/> in <c>pw-link -o</c> text
    /// (lines <c>nodeName:..._FL</c> / <c>..._FR</c>); null for a channel that is not listed.</summary>
    public (string? FL, string? FR) PortsForNode(string pwLinkOutputText, string nodeName)
    {
        string? fl = null, fr = null;
        var prefix = nodeName + ":";
        foreach (var rawLine in pwLinkOutputText.Split('\n'))
        {
            var line = rawLine.Trim();
            if (!line.StartsWith(prefix, StringComparison.Ordinal)) continue;
            if (line.EndsWith("_FL", StringComparison.Ordinal)) fl = line;
            else if (line.EndsWith("_FR", StringComparison.Ordinal)) fr = line;
        }
        return (fl, fr);
    }

    /// <summary>The quoted value of a <c>key = "value"</c> property line.</summary>
    private string? Value(string line)
    {
        var eq = line.IndexOf('=');
        return eq < 0 ? null : line[(eq + 1)..].Trim().Trim('"');
    }
}
