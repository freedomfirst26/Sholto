using Avalonia.Input;
using Sholto.Data;
using Sholto.Interface.MainUI.Controls.Knob;
using Sholto.Interface.MainUI.Controls.Modal;
using Sholto.Interface.MainUI.ViewModels;

namespace Sholto.Interface.MainUI.Tests;

/// <summary>The Settings overlay's view model mirrors the App's backspin time and distance and sends the user's
/// changes, snapped and clamped, as <see cref="SetBackspinTime"/> and <see cref="SetBackspinDistance"/>.</summary>
public class SettingsViewModelTests
{
    private readonly DataBus _bus = new(new ThrowingFailureSink());
    private readonly List<SetBackspinTime> _times = [];
    private readonly List<SetBackspinDistance> _distances = [];
    private readonly SettingsViewModel _vm;

    public SettingsViewModelTests()
    {
        _bus.Register(new TimeRecorder(_times));
        _bus.Register(new DistanceRecorder(_distances));
        _vm = new SettingsViewModel(_bus, _bus, new KnobScaleFactory());
    }

    private sealed class TimeRecorder(List<SetBackspinTime> sent) : ICommandHandler<SetBackspinTime>
    {
        private readonly List<SetBackspinTime> _sent = sent;
        public void Handle(in SetBackspinTime command) => _sent.Add(command);
    }

    private sealed class DistanceRecorder(List<SetBackspinDistance> sent) : ICommandHandler<SetBackspinDistance>
    {
        private readonly List<SetBackspinDistance> _sent = sent;
        public void Handle(in SetBackspinDistance command) => _sent.Add(command);
    }

    [Fact]
    public void Starts_closed_at_the_defaults_with_time_active()
    {
        Assert.False(_vm.IsOpen);
        Assert.Equal(0.6, _vm.Time.Value);
        Assert.Equal("0.60 s", _vm.Time.Text);
        Assert.Equal(0.6, _vm.Time.Default);
        Assert.Equal(2.0, _vm.Distance.Value);
        Assert.Equal("2.0 beats", _vm.Distance.Text);
        Assert.Equal(2.0, _vm.Distance.Default);
        Assert.Same(_vm.Time, _vm.ActiveKnob);
        Assert.True(_vm.Time.IsActive);
        Assert.False(_vm.Distance.IsActive);
    }

    [Fact]
    public void The_scales_have_the_named_ticks_and_steps()
    {
        Assert.Equal([0, 0.5, 1, 2, 3], _vm.Time.Scale.Ticks);
        Assert.Equal(0.05, _vm.Time.Scale.Step);
        Assert.Equal([0, 1, 2, 4, 8, 16], _vm.Distance.Scale.Ticks);
        Assert.Equal(0.25, _vm.Distance.Scale.Step);
    }

    [Fact]
    public void Opens_and_closes()
    {
        var changes = new List<string?>();
        _vm.PropertyChanged += (_, e) => changes.Add(e.PropertyName);
        _vm.Open();
        Assert.True(_vm.IsOpen);
        _vm.Close();
        Assert.False(_vm.IsOpen);
        Assert.Equal([nameof(ISettingsViewModel.IsOpen), nameof(ISettingsViewModel.IsOpen)], changes);
    }

    [Fact]
    public void A_time_change_is_snapped_shown_and_sent_as_its_own_command()
    {
        _vm.Time.Value = 1.234;
        Assert.Equal(1.25, _vm.Time.Value, 9);
        Assert.Equal("1.25 s", _vm.Time.Text);
        Assert.Equal(1.25, Assert.Single(_times).Seconds, 9);
        Assert.Equal(InterfaceIds.MainUI, _times[0].Origin.InterfaceId);
        Assert.Empty(_distances);
        Assert.Equal(2.0, _vm.Distance.Value);
    }

    [Fact]
    public void A_distance_change_is_snapped_shown_and_sent_as_its_own_command()
    {
        _vm.Distance.Value = 5.1;
        Assert.Equal(5.0, _vm.Distance.Value);
        Assert.Equal("5.0 beats", _vm.Distance.Text);
        Assert.Equal(5.0, Assert.Single(_distances).Beats);
        Assert.Equal(InterfaceIds.MainUI, _distances[0].Origin.InterfaceId);
        Assert.Empty(_times);
    }

    [Fact]
    public void Out_of_range_values_are_clamped_before_sending()
    {
        _vm.Time.Value = 9;
        _vm.Time.Value = -1;
        _vm.Distance.Value = 99;
        _vm.Distance.Value = -1;
        Assert.Equal([3.0, 0.0], _times.Select(c => c.Seconds));
        Assert.Equal([16.0, 0.0], _distances.Select(c => c.Beats));
        Assert.Equal("0.00 s", _vm.Time.Text);
    }

    [Fact]
    public void The_apps_values_are_shown_on_the_right_knob_and_not_sent_back()
    {
        _bus.Publish(new BackspinTimeChanged(1.5));
        _bus.Publish(new BackspinDistanceChanged(8));
        Assert.Equal(1.5, _vm.Time.Value);
        Assert.Equal("1.50 s", _vm.Time.Text);
        Assert.Equal(8.0, _vm.Distance.Value);
        Assert.Equal("8.0 beats", _vm.Distance.Text);
        Assert.Empty(_times);
        Assert.Empty(_distances);
    }

    [Fact]
    public void Values_the_app_announced_before_the_view_model_existed_are_replayed()
    {
        var bus = new DataBus(new ThrowingFailureSink());
        bus.Publish(new BackspinTimeChanged(2));
        bus.Publish(new BackspinDistanceChanged(6));
        var vm = new SettingsViewModel(bus, bus, new KnobScaleFactory());
        Assert.Equal(2.0, vm.Time.Value);
        Assert.Equal(6.0, vm.Distance.Value);
    }

    [Fact]
    public void Describes_itself_as_a_one_button_modal()
    {
        Assert.Equal("SETTINGS", _vm.Eyebrow);
        Assert.Equal(ModalTone.Accent, _vm.Tone);
        Assert.Equal("Platter feel", _vm.Title);
        Assert.Equal("Changes apply straight away and are remembered.", _vm.Subtitle);
        Assert.Equal(ModalWidth.Regular, _vm.Width);
        Assert.Equal(new ModalButtons("Close", null, null), _vm.Buttons);
        Assert.Equal(ModalScrimClick.Dismisses, _vm.ScrimClick);
        Assert.False(_vm.CapturesText);
        Assert.False(_vm.CanGoBack);
        Assert.False(_vm.CanConfirm);
    }

    [Fact]
    public void Arrows_step_the_active_knob_by_its_own_step()
    {
        Assert.True(_vm.HandleKey(Key.Right, KeyModifiers.None));
        Assert.Equal(0.65, _vm.Time.Value, 9);
        Assert.True(_vm.HandleKey(Key.Left, KeyModifiers.None));
        Assert.True(_vm.HandleKey(Key.Left, KeyModifiers.None));
        Assert.Equal(0.55, _vm.Time.Value, 9);
        Assert.True(_vm.HandleKey(Key.Up, KeyModifiers.None));
        Assert.True(_vm.HandleKey(Key.Down, KeyModifiers.None));
        Assert.Equal(0.55, _vm.Time.Value, 9);
        Assert.Equal(2.0, _vm.Distance.Value);
        Assert.Empty(_distances);
    }

    [Fact]
    public void Tab_switches_which_knob_the_keys_move_and_shift_tab_does_too()
    {
        Assert.True(_vm.HandleKey(Key.Tab, KeyModifiers.None));
        Assert.Same(_vm.Distance, _vm.ActiveKnob);
        Assert.True(_vm.Distance.IsActive);
        Assert.False(_vm.Time.IsActive);
        _vm.HandleKey(Key.Right, KeyModifiers.None);
        Assert.Equal(2.25, _vm.Distance.Value);
        Assert.Equal(0.6, _vm.Time.Value);
        Assert.Equal(2.25, Assert.Single(_distances).Beats);
        Assert.Empty(_times);
        Assert.True(_vm.HandleKey(Key.Tab, KeyModifiers.Shift));
        Assert.Same(_vm.Time, _vm.ActiveKnob);
        Assert.True(_vm.Time.IsActive);
        Assert.False(_vm.Distance.IsActive);
    }

    [Fact]
    public void Home_and_end_jump_the_active_knob_to_its_ends()
    {
        _vm.HandleKey(Key.End, KeyModifiers.None);
        Assert.Equal(3.0, _vm.Time.Value);
        _vm.HandleKey(Key.Home, KeyModifiers.None);
        Assert.Equal(0.0, _vm.Time.Value);
        _vm.HandleKey(Key.Tab, KeyModifiers.None);
        _vm.HandleKey(Key.End, KeyModifiers.None);
        Assert.Equal(16.0, _vm.Distance.Value);
        _vm.HandleKey(Key.Home, KeyModifiers.None);
        Assert.Equal(0.0, _vm.Distance.Value);
        Assert.Equal([3.0, 0.0], _times.Select(c => c.Seconds));
        Assert.Equal([16.0, 0.0], _distances.Select(c => c.Beats));
    }

    [Fact]
    public void Resetting_to_the_default_sends_it()
    {
        _vm.Time.Value = 2;
        _vm.Time.Value = _vm.Time.Default;
        _vm.Distance.Value = 8;
        _vm.Distance.Value = _vm.Distance.Default;
        Assert.Equal(0.6, _vm.Time.Value, 9);
        Assert.Equal(2.0, _vm.Distance.Value);
        Assert.Equal(0.6, _times[^1].Seconds, 9);
        Assert.Equal(2.0, _distances[^1].Beats);
    }

    [Theory]
    [InlineData(Key.Escape)]
    [InlineData(Key.Enter)]
    [InlineData(Key.Back)]
    [InlineData(Key.A)]
    public void Other_keys_are_left_to_the_router(Key key)
    {
        Assert.False(_vm.HandleKey(key, KeyModifiers.None));
        Assert.Equal(0.6, _vm.Time.Value);
        Assert.Empty(_times);
        Assert.Empty(_distances);
    }

    [Fact]
    public void Dismiss_closes_and_confirm_and_back_do_nothing()
    {
        _vm.Open();
        _vm.Confirm();
        _vm.Back();
        Assert.True(_vm.IsOpen);
        _vm.Dismiss();
        Assert.False(_vm.IsOpen);
    }
}
