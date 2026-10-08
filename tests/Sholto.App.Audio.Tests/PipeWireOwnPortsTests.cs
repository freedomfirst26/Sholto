using Xunit;

namespace Sholto.App.Audio.Tests;

public sealed class PipeWireOwnPortsTests
{
    private const string SinkInputs = """
        Sink Input #101
        	Driver: protocol-native.c
        	Properties:
        		application.name = "Firefox"
        		application.process.id = "2211"
        		application.process.binary = "firefox"
        		node.name = "Firefox"
        Sink Input #202
        	Driver: protocol-native.c
        	Properties:
        		application.process.id = "153438"
        		application.process.binary = "dotnet"
        		client.api = "pipewire-pulse"
        		node.name = "dotnet"
        """;

    private const string PwLinkOut = """
        alsa_output.pci-0000_2d_00.4.analog-stereo:monitor_FL
        alsa_output.pci-0000_2d_00.4.analog-stereo:monitor_FR
        dotnet:output_FL
        dotnet:output_FR
        dotnet:output_RL
        dotnet:output_RR
        """;

    private readonly PipeWireOwnPorts _ports = new();

    [Fact]
    public void The_node_is_found_by_process_id_not_by_name()
    {
        Assert.Equal("dotnet", _ports.NodeNameForProcess(SinkInputs, 153438));
    }

    [Fact]
    public void An_unknown_process_id_has_no_node()
    {
        Assert.Null(_ports.NodeNameForProcess(SinkInputs, 999));
    }

    [Fact]
    public void Only_the_front_ports_of_the_node_are_returned()
    {
        var (fl, fr) = _ports.PortsForNode(PwLinkOut, "dotnet");
        Assert.Equal("dotnet:output_FL", fl);
        Assert.Equal("dotnet:output_FR", fr);
    }

    [Fact]
    public void A_node_with_no_ports_listed_gives_nulls()
    {
        Assert.Equal((null, null), _ports.PortsForNode(PwLinkOut, "Sholto"));
    }
}
