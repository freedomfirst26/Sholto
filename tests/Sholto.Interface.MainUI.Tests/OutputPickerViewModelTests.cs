using Sholto.Data;
using Sholto.Interface.MainUI.ViewModels;

namespace Sholto.Interface.MainUI.Tests;

public class OutputPickerViewModelTests
{
    private static readonly OutputDeviceChoice[] Devices =
    [
        new("Built-in", false), new("Speakers", true), new("DDJ-FLX4", false),
    ];

    private readonly OutputPickerViewModel _vm = new();

    [Fact]
    public void No_saved_device_asks_where_the_master_mix_should_play()
    {
        _ = _vm.AskAsync(Devices, null);

        Assert.Equal("Where should the master mix play?", _vm.Headline);
        Assert.Equal("Pick your speakers or audio interface. You can change this later under Settings → Output device…", _vm.Subtext);
    }

    [Fact]
    public void A_saved_device_that_is_missing_is_named_as_not_connected()
    {
        _ = _vm.AskAsync(Devices, "Gone");

        Assert.Equal("“Gone” isn't connected", _vm.Headline);
        Assert.Equal("Pick another output for the master mix.", _vm.Subtext);
    }

    [Fact]
    public void A_saved_device_that_is_present_gets_the_plain_wording()
    {
        _ = _vm.AskAsync(Devices, "DDJ-FLX4");

        Assert.Equal("Output device", _vm.Headline);
        Assert.Equal("Where the master mix plays.", _vm.Subtext);
        Assert.Equal([false, false, true], _vm.Devices.Select(d => d.IsCurrent));
    }

    [Fact]
    public void Preselects_the_current_device_first()
    {
        _ = _vm.AskAsync(Devices, "DDJ-FLX4");
        Assert.Equal(2, _vm.SelectedIndex);
    }

    [Fact]
    public void Preselects_the_system_default_when_there_is_no_current_device()
    {
        _ = _vm.AskAsync(Devices, "Gone");
        Assert.Equal(1, _vm.SelectedIndex);
    }

    [Fact]
    public void Preselects_the_first_row_when_nothing_is_current_or_default()
    {
        _ = _vm.AskAsync([new("A", false), new("B", false)], null);
        Assert.Equal(0, _vm.SelectedIndex);
    }

    [Fact]
    public void Move_stops_at_both_ends()
    {
        _ = _vm.AskAsync(Devices, "Built-in");

        _vm.Move(-1);
        Assert.Equal(0, _vm.SelectedIndex);

        _vm.Move(+1);
        _vm.Move(+1);
        _vm.Move(+1);
        Assert.Equal(2, _vm.SelectedIndex);
    }

    [Fact]
    public async Task Commit_returns_the_selected_name_and_closes()
    {
        bool closed = false;
        _vm.RequestClose += () => closed = true;
        var ask = _vm.AskAsync(Devices, null);
        _vm.Move(+1);

        _vm.Commit();

        Assert.Equal("DDJ-FLX4", await ask);
        Assert.True(closed);
    }

    [Fact]
    public async Task Cancel_returns_null_and_closes()
    {
        bool closed = false;
        _vm.RequestClose += () => closed = true;
        var ask = _vm.AskAsync(Devices, "Speakers");

        _vm.Cancel();

        Assert.Null(await ask);
        Assert.True(closed);
    }

    [Fact]
    public async Task An_empty_list_returns_null_without_opening()
    {
        bool opened = false;
        _vm.Opened += () => opened = true;

        Assert.Null(await _vm.AskAsync([], null));
        Assert.False(opened);
    }
}
