using Sholto.App.Lifecycle;
using Sholto.App.Audio;
using Sholto.App.Settings;
using Sholto.Data;

namespace Sholto.App.Tests;

/// <summary>The output-device choices of the startup sequence: saved device, picker, fallback and the menu.</summary>
public class OutputDeviceChooserTests
{
    private static readonly Origin Answer = AppLifecycleRig.Origin;

    private static string NewMusicDir() => Directory.CreateTempSubdirectory("sholto-music-").FullName;

    /// <summary>The sequence runs on the thread pool, so a test waits for its effect.</summary>
    private static async Task Eventually(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException("The condition never became true.");
            await Task.Delay(10);
        }
    }

    // ---- Output device --------------------------------------------------------------------------

    private static readonly AudioDevice Speakers = new("Speakers", IsDefault: true);
    private static readonly AudioDevice Headphones = new("Headphones", IsDefault: false);
    private static readonly AudioDevice ControllerCard = new(AppLifecycleRig.ControllerCard, IsDefault: false);

    [Fact]
    public async Task A_saved_output_device_that_is_still_there_starts_audio_without_asking()
    {
        var rig = new AppLifecycleRig(devices: [Speakers, Headphones, ControllerCard], musicDirOverride: NewMusicDir());
        await rig.Settings.SetAsync(SettingsKeys.OutputDevice, "Headphones");

        rig.Lifecycle.Start();

        await Eventually(() => rig.Audio.Started);
        Assert.Equal("Headphones", rig.Audio.StartedOn);
        Assert.Empty(rig.DeviceAsked.Received);
    }

    [Fact]
    public async Task With_no_saved_device_the_picker_lists_the_controllers_card_first_and_the_choice_is_saved()
    {
        var rig = new AppLifecycleRig(devices: [Speakers, Headphones, ControllerCard], musicDirOverride: NewMusicDir());
        rig.Library.Bus.Subscribe(new ActionEventHandler<OutputDeviceNeeded>(
            _ => rig.Lifecycle.Handle(new ChooseOutputDevice("Speakers", Answer))));

        rig.Lifecycle.Start();

        await Eventually(() => rig.Audio.Started);
        var asked = Assert.Single(rig.DeviceAsked.Received);
        Assert.Equal(new[] { AppLifecycleRig.ControllerCard, "Speakers", "Headphones" }, asked.Devices.Select(d => d.Name));
        Assert.Equal(new[] { true, false, false }, asked.Devices.Select(d => d.IsController));
        Assert.Null(asked.CurrentName);
        Assert.Equal("Speakers", rig.Audio.StartedOn);
        Assert.Equal("Speakers", rig.Settings.Peek(SettingsKeys.OutputDevice));
    }

    [Fact]
    public async Task With_no_controller_connected_the_picker_offers_no_controller_row()
    {
        var rig = new AppLifecycleRig(devices: [Speakers, Headphones], musicDirOverride: NewMusicDir());
        rig.Library.Bus.Subscribe(new ActionEventHandler<OutputDeviceNeeded>(
            _ => rig.Lifecycle.Handle(new ChooseOutputDevice("Headphones", Answer))));

        rig.Lifecycle.Start();

        await Eventually(() => rig.Audio.Started);
        var asked = Assert.Single(rig.DeviceAsked.Received);
        Assert.Equal(new[] { "Speakers", "Headphones" }, asked.Devices.Select(d => d.Name));
        Assert.All(asked.Devices, d => Assert.False(d.IsController));
        Assert.Equal("Headphones", rig.Audio.StartedOn);
        Assert.Equal("Headphones", rig.Settings.Peek(SettingsKeys.OutputDevice));
    }

    [Fact]
    public async Task Choosing_the_controllers_card_at_startup_starts_on_it_and_is_saved()
    {
        var rig = new AppLifecycleRig(devices: [Speakers, ControllerCard], musicDirOverride: NewMusicDir());
        rig.Library.Bus.Subscribe(new ActionEventHandler<OutputDeviceNeeded>(
            _ => rig.Lifecycle.Handle(new ChooseOutputDevice(AppLifecycleRig.ControllerCard, Answer))));

        rig.Lifecycle.Start();

        await Eventually(() => rig.Audio.Started);
        Assert.Equal(AppLifecycleRig.ControllerCard, rig.Audio.StartedOn);
        Assert.Equal(AppLifecycleRig.ControllerCard, rig.Settings.Peek(SettingsKeys.OutputDevice));
    }

    [Fact]
    public async Task A_saved_controllers_card_that_is_connected_starts_audio_on_it_without_asking()
    {
        var rig = new AppLifecycleRig(devices: [Speakers, Headphones, ControllerCard], musicDirOverride: NewMusicDir());
        await rig.Settings.SetAsync(SettingsKeys.OutputDevice, AppLifecycleRig.ControllerCard);

        rig.Lifecycle.Start();

        await Eventually(() => rig.Audio.Started);
        Assert.Equal(AppLifecycleRig.ControllerCard, rig.Audio.StartedOn);
        Assert.Empty(rig.DeviceAsked.Received);
    }

    [Fact]
    public async Task With_only_the_controllers_card_connected_nothing_is_asked_and_master_stays_on_it()
    {
        var rig = new AppLifecycleRig(devices: [ControllerCard], musicDirOverride: NewMusicDir());

        rig.Lifecycle.Start();

        await Eventually(() => rig.Audio.Started);
        Assert.Null(rig.Audio.StartedOn);
        Assert.Empty(rig.DeviceAsked.Received);
    }

    [Fact]
    public async Task A_cancelled_device_picker_falls_back_on_the_controllers_card_when_it_is_connected()
    {
        var rig = new AppLifecycleRig(devices: [Speakers, ControllerCard], musicDirOverride: NewMusicDir());
        rig.Library.Bus.Subscribe(new ActionEventHandler<OutputDeviceNeeded>(
            _ => rig.Lifecycle.Handle(new ChooseOutputDevice(null, Answer))));

        rig.Lifecycle.Start();

        await Eventually(() => rig.Audio.Started);
        Assert.Null(rig.Audio.StartedOn);
        Assert.Null(rig.Settings.Peek(SettingsKeys.OutputDevice));
    }

    [Fact]
    public async Task A_cancelled_device_picker_with_no_controller_card_starts_no_audio()
    {
        var rig = new AppLifecycleRig(devices: [Speakers], musicDirOverride: NewMusicDir());
        rig.Library.Bus.Subscribe(new ActionEventHandler<OutputDeviceNeeded>(
            _ => rig.Lifecycle.Handle(new ChooseOutputDevice(null, Answer))));

        rig.Lifecycle.Start();

        await Eventually(() => rig.DeviceAsked.Received.Count == 1);
        await Task.Delay(100);
        Assert.False(rig.Audio.Started);
    }

    [Fact]
    public async Task With_no_output_devices_at_all_nothing_is_asked_and_no_audio_starts()
    {
        var rig = new AppLifecycleRig(devices: [], musicDirOverride: NewMusicDir());

        rig.Lifecycle.Start();

        await Eventually(() => rig.Enumerator.Calls == 1);
        await Task.Delay(100);
        Assert.Empty(rig.DeviceAsked.Received);
        Assert.False(rig.Audio.Started);
    }

    [Fact]
    public async Task The_output_devices_are_listed_before_the_database_is_awaited()
    {
        var rig = new AppLifecycleRig(gated: true, devices: [Speakers], musicDirOverride: NewMusicDir());

        rig.Lifecycle.Start();

        await Eventually(() => rig.Enumerator.Calls == 1);
        await Task.Delay(100);
        Assert.False(rig.Database.Opened.IsCompleted);
        Assert.Empty(rig.DeviceAsked.Received);

        rig.Database.Release();
        await Eventually(() => rig.DeviceAsked.Received.Count == 1);
    }

    [Fact]
    public async Task Without_the_database_the_device_choice_is_not_saved()
    {
        var rig = new AppLifecycleRig(databaseAvailable: false, devices: [Speakers], musicDirOverride: NewMusicDir());
        rig.Library.Bus.Subscribe(new ActionEventHandler<OutputDeviceNeeded>(
            _ => rig.Lifecycle.Handle(new ChooseOutputDevice("Speakers", Answer))));

        rig.Lifecycle.Start();

        await Eventually(() => rig.Audio.Started);
        Assert.Equal("Speakers", rig.Audio.StartedOn);
        Assert.Null(await rig.OutputDevicePreference.GetAsync());
    }

    [Fact]
    public async Task The_menu_switches_to_another_device_but_not_to_the_one_already_in_use()
    {
        var rig = new AppLifecycleRig(devices: [Speakers, Headphones, ControllerCard], musicDirOverride: NewMusicDir());
        await rig.Settings.SetAsync(SettingsKeys.OutputDevice, "Speakers");
        var reply = "Speakers";
        rig.Library.Bus.Subscribe(new ActionEventHandler<OutputDeviceNeeded>(
            _ => rig.Lifecycle.Handle(new ChooseOutputDevice(reply, Answer))));
        // The preference reads wait for the database, which opens when the lifecycle starts.
        rig.Lifecycle.Start();
        await Eventually(() => rig.Audio.StartedOn is not null);
        await Task.Delay(100);  // the route is recorded just after the engine starts
        rig.DeviceAsked.Received.Clear();

        rig.Lifecycle.Handle(new ChangeOutputDevice(Answer));
        await Eventually(() => rig.DeviceAsked.Received.Count == 1);
        await Task.Delay(100);
        Assert.Equal("Speakers", rig.DeviceAsked.Received[0].CurrentName);
        Assert.Null(rig.Audio.SwitchedTo);

        reply = "Headphones";
        rig.Lifecycle.Handle(new ChangeOutputDevice(Answer));

        await Eventually(() => rig.Audio.SwitchedTo is not null);
        Assert.Equal("Headphones", rig.Audio.SwitchedTo);
        Assert.Equal("Headphones", rig.Settings.Peek(SettingsKeys.OutputDevice));
    }

    [Fact]
    public async Task The_menu_switches_master_back_onto_the_controllers_card_and_saves_it()
    {
        var rig = new AppLifecycleRig(devices: [Speakers, Headphones, ControllerCard], musicDirOverride: NewMusicDir());
        await rig.Settings.SetAsync(SettingsKeys.OutputDevice, "Speakers");
        rig.Library.Bus.Subscribe(new ActionEventHandler<OutputDeviceNeeded>(
            _ => rig.Lifecycle.Handle(new ChooseOutputDevice(AppLifecycleRig.ControllerCard, Answer))));
        rig.Lifecycle.Start();
        await Eventually(() => rig.Audio.StartedOn is not null);
        await Task.Delay(100);  // the route is recorded just after the engine starts

        rig.Lifecycle.Handle(new ChangeOutputDevice(Answer));

        await Eventually(() => rig.Audio.SwitchedTo is not null);
        var asked = Assert.Single(rig.DeviceAsked.Received);
        Assert.Equal(new[] { AppLifecycleRig.ControllerCard, "Speakers", "Headphones" }, asked.Devices.Select(d => d.Name));
        Assert.Equal("Speakers", asked.CurrentName);
        Assert.Equal(AppLifecycleRig.ControllerCard, rig.Audio.SwitchedTo);
        Assert.Equal(AppLifecycleRig.ControllerCard, rig.Settings.Peek(SettingsKeys.OutputDevice));
    }

    [Fact]
    public async Task A_failed_route_marks_the_controller_in_use_and_the_speakers_can_be_picked_again()
    {
        var rig = new AppLifecycleRig(devices: [Speakers, Headphones, ControllerCard], musicDirOverride: NewMusicDir());
        await rig.Settings.SetAsync(SettingsKeys.OutputDevice, "Speakers");
        rig.Audio.NextRoute = FailedRoute;
        rig.Library.Bus.Subscribe(new ActionEventHandler<OutputDeviceNeeded>(
            _ => rig.Lifecycle.Handle(new ChooseOutputDevice("Speakers", Answer))));
        rig.Lifecycle.Start();
        await Eventually(() => rig.Audio.StartedOn is not null);
        await Task.Delay(100);  // the route is recorded just after the engine starts
        rig.DeviceAsked.Received.Clear();

        rig.Lifecycle.Handle(new ChangeOutputDevice(Answer));

        await Eventually(() => rig.Audio.SwitchedTo is not null);
        Assert.Equal(AppLifecycleRig.ControllerCard, rig.DeviceAsked.Received[0].CurrentName);
        Assert.Equal("Speakers", rig.Audio.SwitchedTo);
    }

    [Fact]
    public async Task With_master_on_the_controller_and_no_speaker_chosen_the_controller_is_marked_in_use()
    {
        var rig = new AppLifecycleRig(devices: [Speakers, ControllerCard], musicDirOverride: NewMusicDir());
        rig.Library.Bus.Subscribe(new ActionEventHandler<OutputDeviceNeeded>(
            _ => rig.Lifecycle.Handle(new ChooseOutputDevice(null, Answer))));
        rig.Lifecycle.Start();
        await Eventually(() => rig.DeviceAsked.Received.Count == 1);
        await Task.Delay(100);  // the route is recorded just after the engine starts
        rig.DeviceAsked.Received.Clear();

        rig.Lifecycle.Handle(new ChangeOutputDevice(Answer));

        await Eventually(() => rig.DeviceAsked.Received.Count == 1);
        Assert.Equal(AppLifecycleRig.ControllerCard, rig.DeviceAsked.Received[0].CurrentName);
    }

    private static readonly MasterRoute FailedRoute =
        new(AppLifecycleRig.ControllerCard, "route to 'Speakers' failed");
}
