using Sholto.App.Analysis.Reporting;
using Sholto.App.Analysis.Stems;
using Sholto.App.Analysis.ToolBoundary;

namespace Sholto.App.ExternalTools.Tests;

public class DemucsDeviceTests
{
    [Theory]
    [InlineData(StemDevice.Cuda, "cuda")]
    [InlineData(StemDevice.Cpu, "cpu")]
    public async Task Demucs_is_always_told_its_device_explicitly(StemDevice device, string expected)
    {
        var tool = new ArgsRecordingTool();
        var step = new DemucsStemAnalysisStep(tool, _ => "/work", new FixedStemDevice(device));

        await Assert.ThrowsAsync<InvalidOperationException>(() => step.AnalyzeAsync("/music/a.mp3", new NullAnalysisReporter()));

        var args = tool.Args!;
        var at = args.ToList().IndexOf("-d");
        Assert.True(at >= 0, "no -d flag was passed");
        Assert.Equal(expected, args[at + 1]);
    }

    [Fact]
    public async Task Probe_reports_cuda_when_torch_in_the_demucs_interpreter_says_true()
    {
        using var rig = new ProbeRig("True");

        Assert.Equal(StemDevice.Cuda, await rig.Device.ResolveAsync());
    }

    [Fact]
    public async Task Probe_reports_cpu_when_torch_says_false()
    {
        using var rig = new ProbeRig("False");

        Assert.Equal(StemDevice.Cpu, await rig.Device.ResolveAsync());
    }

    [Fact]
    public async Task Probe_reports_cpu_when_demucs_cannot_be_found()
    {
        var device = new CudaProbingStemDevice("/no/such/demucs", TimeSpan.FromSeconds(5));

        Assert.Equal(StemDevice.Cpu, await device.ResolveAsync());
    }

    [Fact]
    public async Task Probe_runs_once_however_many_callers_ask()
    {
        using var rig = new ProbeRig("True");

        await Task.WhenAll(rig.Device.ResolveAsync(), rig.Device.ResolveAsync(), rig.Device.ResolveAsync());
        await rig.Device.ResolveAsync();

        Assert.Equal(1, rig.Runs);
    }

    private sealed class ArgsRecordingTool : IExternalTool
    {
        public IReadOnlyList<string>? Args { get; private set; }
        public bool IsAvailable => true;

        public Task<ToolOutcome<T>> RunAsync<T>(
            IToolDefinition<T> tool, ToolInput toolInput, IAnalysisReporter reporter, CancellationToken ct)
        {
            Args = tool.BuildArgs(toolInput);
            return Task.FromResult(tool.Verify(toolInput, "", exitCode: 1));
        }
    }
}
