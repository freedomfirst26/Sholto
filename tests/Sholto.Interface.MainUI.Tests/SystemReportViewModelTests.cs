using Avalonia.Input;
using Sholto.Data;
using Sholto.Interface.MainUI.Controls.Modal;
using Sholto.Interface.MainUI.ViewModels;

namespace Sholto.Interface.MainUI.Tests;

/// <summary>The system report's own view model: capability cards and headline from the boot-time probe, and the
/// <see cref="IModalContent"/> shape the modal shell reads.</summary>
public class SystemReportViewModelTests
{
    private readonly SystemReportViewModel _vm = new();

    private static ToolStatus Tool(string name, bool required, bool present) =>
        new(name, "cap-" + name, required, present ? "/bin/" + name : null, null);

    private static SystemCheckReported Check(SystemHealth health, params ToolStatus[] tools) => new(tools, health);

    private ToolStatus BeatThis(bool present) =>
        new(ExternalToolNames.BeatThis, ToolCapabilities.Beats, true, present ? "/bin/beat_this" : null, BeatThisBeatAnalysisStep.InstallCommand);

    private ToolStatus Madmom(bool present) =>
        new(ExternalToolNames.Madmom, ToolCapabilities.Beats, false, present ? "/bin/madmom" : null, MadmomBeatAnalysisStep.InstallCommand);

    private ToolStatus Demucs(bool present) =>
        new(ExternalToolNames.Demucs, ToolCapabilities.Stems, false, present ? "/bin/demucs" : null, null);

    private ToolStatus Ffmpeg(bool present) =>
        new(ExternalToolNames.Ffmpeg, ToolCapabilities.Transcode, false, present ? "/bin/ffmpeg" : null, null);

    [Fact]
    public void Starts_closed_and_empty_with_the_healthy_headline()
    {
        Assert.False(_vm.IsOpen);
        Assert.Empty(_vm.Capabilities);
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
    public void Report_groups_tools_into_one_card_per_capability_and_notifies()
    {
        var changes = new List<string?>();
        _vm.PropertyChanged += (_, e) => changes.Add(e.PropertyName);
        _vm.Report(Check(SystemHealth.Degraded, BeatThis(true), Madmom(false), Demucs(false), Ffmpeg(true)));

        Assert.Equal(3, _vm.Capabilities.Count);
        Assert.Equal(SystemReportCapabilityState.Ready, _vm.Capabilities[0].State);
        Assert.Equal(SystemReportCapabilityState.Missing, _vm.Capabilities[1].State);
        Assert.Equal(SystemReportCapabilityState.Ready, _vm.Capabilities[2].State);
        foreach (var name in new[]
                 {
                     nameof(ISystemReportViewModel.Capabilities), nameof(ISystemReportViewModel.Headline),
                     nameof(ISystemReportViewModel.VerdictTitle), nameof(ISystemReportViewModel.ReadyCount),
                     nameof(ISystemReportViewModel.IsHealthy), nameof(ISystemReportViewModel.NeedsAttention),
                     nameof(IModalContent.KeyHint),
                 })
            Assert.Contains(name, changes);
    }

    [Fact]
    public void Beat_this_missing_with_madmom_present_is_ready_on_the_fallback_not_broken()
    {
        _vm.Report(Check(SystemHealth.Healthy, BeatThis(false), Madmom(true)));

        var beats = _vm.Capabilities.Single();
        Assert.Equal(SystemReportCapabilityState.ReadyWithFallback, beats.State);
        Assert.True(beats.IsReady);
        Assert.True(beats.IsFallback);
        Assert.False(beats.IsMissing);
        Assert.Equal("Ready", beats.StatusText);
        Assert.DoesNotContain("No BPM", beats.Detail);
        Assert.Equal($"Working, using the {ExternalToolNames.Madmom} fallback. {ExternalToolNames.BeatThis} is not installed.", beats.Detail);
        Assert.Equal($"{ExternalToolNames.Madmom} · /bin/madmom", beats.ToolLine);
        Assert.Equal($"Optional: install {ExternalToolNames.BeatThis} for more accurate beats.", beats.FixLabel);
        Assert.Equal(BeatThisBeatAnalysisStep.InstallCommand, beats.FixCommand);
        Assert.True(beats.HasFix);
    }

    [Fact]
    public void Both_beat_tools_missing_is_missing_with_the_cost_and_the_preferred_tools_command()
    {
        _vm.Report(Check(SystemHealth.Offline, BeatThis(false), Madmom(false)));

        var beats = _vm.Capabilities.Single();
        Assert.Equal(SystemReportCapabilityState.Missing, beats.State);
        Assert.Equal("Not available", beats.StatusText);
        Assert.Equal("No BPM, beatgrid, waveform or key for any track.", beats.Detail);
        Assert.Equal($"{ExternalToolNames.BeatThis} · not found", beats.ToolLine);
        Assert.Equal("To fix:", beats.FixLabel);
        Assert.Equal(BeatThisBeatAnalysisStep.InstallCommand, beats.FixCommand);
    }

    [Fact]
    public void Demucs_missing_points_at_the_installer_and_says_what_is_lost()
    {
        _vm.Report(Check(SystemHealth.Degraded, Demucs(false), Ffmpeg(false)));

        var stems = _vm.Capabilities[0];
        Assert.Equal(SystemReportCapabilityState.Missing, stems.State);
        Assert.Equal("Stem separation", stems.Name);
        Assert.Equal("No stem separation — no stem EQ, stem mutes or vocal regions.", stems.Detail);
        Assert.Equal("./install.sh", stems.FixCommand);
        Assert.Equal("To fix: run the installer from the Sholto folder.", stems.FixLabel);
        Assert.Equal("No M4A/AAC playback (MP3, FLAC and WAV still work).", _vm.Capabilities[1].Detail);
    }

    [Fact]
    public void A_ready_card_names_what_it_gives_shows_its_path_and_has_no_fix()
    {
        _vm.Report(Check(SystemHealth.Healthy, BeatThis(true), Madmom(false), Demucs(true), Ffmpeg(true)));

        var beats = _vm.Capabilities[0];
        Assert.Equal("Beats, BPM & key", beats.Name);
        Assert.Equal("Beatgrid, BPM, waveform and key for every track.", beats.Detail);
        Assert.Equal($"{ExternalToolNames.BeatThis} · /bin/beat_this", beats.ToolLine);
        Assert.Equal("", beats.FixCommand);
        Assert.Equal("", beats.FixLabel);
        Assert.False(beats.HasFix);
        Assert.False(beats.IsFallback);
        Assert.Equal("M4A / AAC playback", _vm.Capabilities[2].Name);
    }

    [Fact]
    public void An_unknown_capability_shows_its_raw_name()
    {
        _vm.Report(Check(SystemHealth.Healthy, Tool("x", false, true)));

        Assert.Equal("cap-x", _vm.Capabilities.Single().Name);
        Assert.Equal("Available.", _vm.Capabilities.Single().Detail);
    }

    [Fact]
    public void Verdict_headline_and_ready_count_follow_the_health()
    {
        _vm.Report(Check(SystemHealth.Offline, BeatThis(false), Madmom(false), Demucs(true), Ffmpeg(true)));
        Assert.Equal("Beat detection is off", _vm.VerdictTitle);
        Assert.Equal("Beat detection is unavailable, so tracks get no BPM, beatgrid, waveform or key.", _vm.Headline);
        Assert.Equal("2 of 3 ready", _vm.ReadyCount);
        Assert.True(_vm.NeedsAttention);
        Assert.False(_vm.IsHealthy);

        _vm.Report(Check(SystemHealth.Degraded, BeatThis(false), Madmom(true), Demucs(false), Ffmpeg(true)));
        Assert.Equal("Running with gaps", _vm.VerdictTitle);
        Assert.Equal("Sholto is running, but some optional analysis features are unavailable.", _vm.Headline);
        Assert.Equal("2 of 3 ready", _vm.ReadyCount);
        Assert.True(_vm.NeedsAttention);

        _vm.Report(Check(SystemHealth.Healthy, BeatThis(true), Madmom(false), Demucs(true), Ffmpeg(true)));
        Assert.Equal("All set", _vm.VerdictTitle);
        Assert.Equal("Everything Sholto needs is installed.", _vm.Headline);
        Assert.Equal("3 of 3 ready", _vm.ReadyCount);
        Assert.True(_vm.IsHealthy);
        Assert.False(_vm.NeedsAttention);
    }

    [Fact]
    public void Describes_itself_as_an_accent_toned_close_only_modal_whose_title_is_the_banner()
    {
        Assert.Equal("SYSTEM REPORT", _vm.Eyebrow);
        Assert.Equal(ModalTone.Accent, _vm.Tone);
        Assert.Equal("", _vm.Title);
        Assert.Null(_vm.Subtitle);
        Assert.Equal(ModalWidth.Regular, _vm.Width);
        Assert.Equal(new ModalButtons("Close", null, null), _vm.Buttons);
        Assert.Equal(ModalScrimClick.Dismisses, _vm.ScrimClick);
        Assert.False(_vm.CapturesText);
        Assert.False(_vm.CanGoBack);
        Assert.False(_vm.CanConfirm);
    }

    [Fact]
    public void The_hint_asks_for_a_restart_only_when_something_needs_attention()
    {
        _vm.Report(Check(SystemHealth.Degraded, Demucs(false)));
        Assert.Equal("Restart Sholto after installing. Tools are checked once at startup.", _vm.KeyHint);

        _vm.Report(Check(SystemHealth.Healthy, Demucs(true)));
        Assert.Equal("Tools are checked once at startup.", _vm.KeyHint);
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
