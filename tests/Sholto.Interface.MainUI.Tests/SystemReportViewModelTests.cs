using Avalonia.Input;
using Sholto.Data;
using Sholto.Interface.MainUI.Controls.Modal;
using Sholto.Interface.MainUI.ViewModels;

namespace Sholto.Interface.MainUI.Tests;

/// <summary>The system report's own view model: rows and headline from the boot-time probe, and the
/// <see cref="IModalContent"/> shape the modal shell reads.</summary>
public class SystemReportViewModelTests
{
    private readonly SystemReportViewModel _vm = new();

    private static ToolStatus Tool(string name, bool required, bool present) =>
        new(name, "cap-" + name, required, present ? "/bin/" + name : null, null);

    private static SystemCheckReported Check(SystemHealth health, params ToolStatus[] tools) => new(tools, health);

    [Fact]
    public void Starts_closed_and_empty_with_the_healthy_headline()
    {
        Assert.False(_vm.IsOpen);
        Assert.Empty(_vm.Rows);
        Assert.Equal("Everything Sholto needs is installed.", _vm.Headline);
    }

    [Fact]
    public void Open_and_close_raise_IsOpen()
    {
        var changes = new List<string?>();
        _vm.PropertyChanged += (_, e) => changes.Add(e.PropertyName);
        _vm.Open();
        Assert.True(_vm.IsOpen);
        _vm.Close();
        Assert.False(_vm.IsOpen);
        Assert.Equal([nameof(ISystemReportViewModel.IsOpen), nameof(ISystemReportViewModel.IsOpen)], changes);
    }

    [Fact]
    public void Report_builds_one_row_per_tool_and_notifies()
    {
        var changes = new List<string?>();
        _vm.PropertyChanged += (_, e) => changes.Add(e.PropertyName);
        _vm.Report(Check(SystemHealth.Degraded, Tool(ExternalToolNames.Madmom, true, true), Tool(ExternalToolNames.Demucs, false, false)));
        Assert.Equal(2, _vm.Rows.Count);
        Assert.True(_vm.Rows[0].IsPresent);
        Assert.True(_vm.Rows[1].IsMissing);
        Assert.Contains(nameof(ISystemReportViewModel.Rows), changes);
        Assert.Contains(nameof(ISystemReportViewModel.Headline), changes);
        Assert.Contains(nameof(IModalContent.Title), changes);
    }

    [Fact]
    public void A_missing_tool_row_says_what_is_lost_by_capability()
    {
        _vm.Report(Check(SystemHealth.Offline,
            new ToolStatus("a", ToolCapabilities.Beats, true, null, null),
            new ToolStatus("b", ToolCapabilities.Stems, false, null, null),
            new ToolStatus("c", ToolCapabilities.Transcode, false, null, null)));

        Assert.Equal("No BPM, beatgrid, waveform or key for any track.", _vm.Rows[0].Detail);
        Assert.Equal("No stem separation — no stem EQ, stem mutes or vocal regions.", _vm.Rows[1].Detail);
        Assert.Equal("No M4A/AAC playback (MP3, FLAC and WAV still work).", _vm.Rows[2].Detail);
    }

    [Fact]
    public void Install_is_the_status_command_else_the_installer_line_and_a_present_tool_shows_its_path()
    {
        _vm.Report(Check(SystemHealth.Offline,
            new ToolStatus(ExternalToolNames.Madmom, ToolCapabilities.Beats, true, null, "uv tool install madmom-onnx"),
            new ToolStatus(ExternalToolNames.Demucs, ToolCapabilities.Stems, false, null, null),
            new ToolStatus(ExternalToolNames.Ffmpeg, ToolCapabilities.Transcode, false, "/bin/demucs", null)));

        Assert.Equal("uv tool install madmom-onnx", _vm.Rows[0].Install);
        Assert.Equal("Run ./install.sh from the Sholto folder, then restart Sholto.", _vm.Rows[1].Install);
        Assert.Equal("/bin/demucs", _vm.Rows[2].Detail);
        Assert.Equal("", _vm.Rows[2].Install);
    }

    [Fact]
    public void Headline_follows_the_severity_and_is_the_title()
    {
        _vm.Report(Check(SystemHealth.Offline, Tool(ExternalToolNames.Madmom, true, false)));
        Assert.Equal("Beat detection is unavailable, so tracks get no BPM, beatgrid, waveform or key.", _vm.Headline);
        Assert.Equal(_vm.Headline, _vm.Title);

        _vm.Report(Check(SystemHealth.Degraded, Tool(ExternalToolNames.Madmom, true, true), Tool(ExternalToolNames.Demucs, false, false)));
        Assert.Equal("Sholto is running, but some optional analysis features are unavailable.", _vm.Headline);

        _vm.Report(Check(SystemHealth.Healthy, Tool(ExternalToolNames.Madmom, true, true)));
        Assert.Equal("Everything Sholto needs is installed.", _vm.Headline);
    }

    [Fact]
    public void Describes_itself_as_an_attention_toned_close_only_modal()
    {
        Assert.Equal("●  SYSTEM REPORT", _vm.Eyebrow);
        Assert.Equal(ModalTone.Attention, _vm.Tone);
        Assert.Null(_vm.Subtitle);
        Assert.Equal("Tools are looked up once at startup — restart Sholto after installing.", _vm.KeyHint);
        Assert.Equal(ModalWidth.Narrow, _vm.Width);
        Assert.Equal(new ModalButtons("Close", null, null), _vm.Buttons);
        Assert.Equal(ModalScrimClick.Dismisses, _vm.ScrimClick);
        Assert.False(_vm.CapturesText);
        Assert.False(_vm.CanGoBack);
        Assert.False(_vm.CanConfirm);
    }

    [Fact]
    public void Dismiss_closes_and_no_key_is_its_own()
    {
        _vm.Open();
        _vm.Confirm();
        _vm.Back();
        Assert.True(_vm.IsOpen);
        Assert.False(_vm.HandleKey(Key.Escape, KeyModifiers.None));
        Assert.False(_vm.HandleKey(Key.Enter, KeyModifiers.None));
        _vm.Dismiss();
        Assert.False(_vm.IsOpen);
    }
}
