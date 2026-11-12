using Sholto.Data;

namespace Sholto.Interface.MainUI.Tests;

/// <summary>The Folders panel and the output picker are never open together: a question arriving while the other
/// one is open waits until it closes, and the menu refuses to open one over the other. (The App's half, the
/// startup device question waiting for the folders question, is in <c>FolderStartupTests</c>.)</summary>
[Collection(MainViewModelCollection.Name)]
public class FoldersAndOutputPickerTests
{
    private readonly MainViewModelRig _rig = new();
    // The app thread the prompts post to: drained by the test, as the UI thread drains the dispatcher.
    private readonly QueuedAppThread _appThread = new();
    private readonly F9RecordingCommandHandler<ChooseFolders> _folderAnswers;
    private readonly F9RecordingCommandHandler<ChooseOutputDevice> _deviceAnswers;
    private readonly F9RecordingCommandHandler<ChangeFolders> _folderChanges;
    private readonly F9RecordingCommandHandler<ChangeOutputDevice> _deviceChanges;

    public FoldersAndOutputPickerTests()
    {
        _folderAnswers = _rig.Record<ChooseFolders>();
        _deviceAnswers = _rig.Record<ChooseOutputDevice>();
        _folderChanges = _rig.Record<ChangeFolders>();
        _deviceChanges = _rig.Record<ChangeOutputDevice>();
        new LifecyclePromptsFactory(_rig.Bus, _rig.Bus, _appThread, new RecordingLogFactory()).Create(_rig.Vm).Start();
    }

    private static FoldersNeeded FoldersQuestion(FoldersReason reason = FoldersReason.FirstRun) => new(reason,
        new FolderRow(null, FolderRowState.NotChosen, null),
        new FolderRow(null, FolderRowState.NotChosen, null),
        0);

    private static OutputDeviceNeeded DeviceQuestion() =>
        new([new OutputDeviceChoice("Speakers", true), new OutputDeviceChoice("Headphones", false)], null);

    private bool PickerOpen => _rig.Vm.IsOutputPickerOpen;

    private bool FoldersOpen => _rig.Vm.Folders.IsOpen;

    /// <summary>Publish on the app thread, then run what it posted (a question that waited its turn).</summary>
    private void Publish<T>(T e) where T : struct, IEvent
    {
        _rig.Bus.Publish(e);
        _appThread.Drain();
    }

    /// <summary>Do <paramref name="action"/> on the app thread, then run what it posted.</summary>
    private void Do(Action action)
    {
        action();
        _appThread.Drain();
    }

    /// <summary>The picker's answer is posted from a pool thread once its task completes.</summary>
    private static async Task Eventually(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException("The condition never became true.");
            await Task.Delay(10);
        }
    }

    // ---- Questions wait their turn --------------------------------------------------------------

    [Fact]
    public void Each_question_alone_opens_at_once()
    {
        Publish(FoldersQuestion());
        Assert.True(FoldersOpen);
        Do(() => _rig.Vm.Folders.Dismiss());

        Publish(DeviceQuestion());
        Assert.True(PickerOpen);
    }

    [Fact]
    public async Task The_device_question_arriving_while_the_folders_panel_is_open_waits_until_the_panel_closes()
    {
        Publish(FoldersQuestion());

        Publish(DeviceQuestion());

        Assert.True(FoldersOpen);
        Assert.False(PickerOpen);

        Do(() => _rig.Vm.Folders.Dismiss());

        Assert.False(FoldersOpen);
        Assert.True(PickerOpen);
        Assert.Single(_folderAnswers.Received);
        Do(() => _rig.Vm.OutputPicker.Commit());
        await Eventually(() => _appThread.Pending > 0);
        _appThread.Drain();
        Assert.Equal("Speakers", _deviceAnswers.Received[0].Name);
    }

    [Fact]
    public void The_device_question_waits_while_the_folders_panel_applies_an_answer_and_opens_once_the_app_says_done()
    {
        Publish(FoldersQuestion());
        _rig.Vm.Folders.Pick(_rig.Vm.Folders.Music, "/music");
        _rig.Vm.Folders.Pick(_rig.Vm.Folders.Analysis, "/analysis");
        Publish(DeviceQuestion());

        // Continue keeps the panel open, working, until the App publishes the folder in use.
        Do(() => _rig.Vm.Folders.Confirm());
        Assert.True(FoldersOpen);
        Assert.False(PickerOpen);

        Publish(new AnalysisFolderChanged("/analysis", false));

        Assert.False(FoldersOpen);
        Assert.True(PickerOpen);
    }

    [Fact]
    public void The_folders_question_arriving_while_the_output_picker_is_open_waits_until_the_picker_closes()
    {
        Publish(DeviceQuestion());

        Publish(FoldersQuestion(FoldersReason.DriveNotFound));

        Assert.True(PickerOpen);
        Assert.False(FoldersOpen);

        Do(() => _rig.Vm.OutputPicker.Cancel());

        Assert.False(PickerOpen);
        Assert.True(FoldersOpen);
        Assert.Equal("A folder can't be reached — reconnect the drive or choose another", _rig.Vm.Folders.Title);
    }

    [Fact]
    public void Neither_is_ever_open_together_however_the_questions_interleave()
    {
        var both = false;
        _rig.Vm.PropertyChanged += (_, _) => both |= PickerOpen && FoldersOpen;
        _rig.Vm.Folders.PropertyChanged += (_, _) => both |= PickerOpen && FoldersOpen;

        Publish(FoldersQuestion());
        Publish(DeviceQuestion());
        Do(() => _rig.Vm.Folders.Dismiss());
        Publish(FoldersQuestion(FoldersReason.Change));
        Do(() => _rig.Vm.OutputPicker.Commit());
        Publish(DeviceQuestion());
        Do(() => _rig.Vm.Folders.Dismiss());

        Assert.False(both);
        Assert.True(PickerOpen);
        Assert.False(FoldersOpen);
    }

    // ---- The menu refuses -----------------------------------------------------------------------

    [Fact]
    public void The_menu_asks_for_either_when_neither_is_open()
    {
        _rig.Vm.RequestChangeFolders();
        _rig.Vm.RequestChangeOutputDevice();

        Assert.Single(_folderChanges.Received);
        Assert.Single(_deviceChanges.Received);
    }

    [Fact]
    public void The_menu_refuses_to_ask_for_the_folders_while_the_output_picker_is_open()
    {
        Publish(DeviceQuestion());

        _rig.Vm.RequestChangeFolders();

        Assert.Empty(_folderChanges.Received);
        Assert.False(FoldersOpen);
    }

    [Fact]
    public void The_menu_refuses_to_ask_for_the_output_device_while_the_folders_panel_is_open()
    {
        Publish(FoldersQuestion());

        _rig.Vm.RequestChangeOutputDevice();

        Assert.Empty(_deviceChanges.Received);
        Assert.False(PickerOpen);
    }
}
