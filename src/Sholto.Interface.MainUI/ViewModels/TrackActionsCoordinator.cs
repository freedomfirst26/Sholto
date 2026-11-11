using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Sholto.Interface.MainUI.ViewModels;

/// <summary>The track action menu, the tag editor and the crate picker. Raises only names the main view
/// model also has, so it relays them verbatim.</summary>
public sealed class TrackActionsCoordinator : ITrackActionsCoordinator
{
    private readonly IOverlayViewModelFactory _overlays;

    public event PropertyChangedEventHandler? PropertyChanged;

    public TrackActionsCoordinator(IOverlayViewModelFactory overlays, TrackActionsViewModel trackActions)
    {
        _overlays = overlays;
        TrackActions = trackActions;
        TrackActions.RequestClose += () => IsTrackActionsOpen = false;
        TrackActions.Invoked += kind =>
        {
            var row = TrackActions.Row;
            IsTrackActionsOpen = false;
            if (row is null) return;
            switch (kind)
            {
                case TrackActionKind.Tag:        _ = OpenTagEditorAsync(row); break;
                case TrackActionKind.AddToCrate: _ = OpenCratePickerAsync(row); break;
            }
        };
    }

    public TagEditorViewModel? TagEditor { get; private set; }

    private bool _isTagEditorOpen;
    public bool IsTagEditorOpen
    {
        get => _isTagEditorOpen;
        private set { if (_isTagEditorOpen == value) return; _isTagEditorOpen = value; Notify(); }
    }

    public CratePickerViewModel? CratePicker { get; private set; }

    public bool IsCratePickerOpen => CratePicker?.IsOpen ?? false;

    public TrackActionsViewModel TrackActions { get; }

    private bool _isTrackActionsOpen;
    public bool IsTrackActionsOpen
    {
        get => _isTrackActionsOpen;
        private set { if (_isTrackActionsOpen == value) return; _isTrackActionsOpen = value; Notify(); }
    }

    /// <summary>The database is up (the App announces it): build the tag editor and the crate picker, which
    /// read and edit through queries and commands. (The search overlay follows the same event itself.)</summary>
    public void AttachLibrary(bool available)
    {
        if (!available || TagEditor is not null) return;

        TagEditor = _overlays.TagEditor();
        TagEditor.RequestClose += () => IsTagEditorOpen = false;
        Notify(nameof(TagEditor));

        CratePicker = _overlays.CratePicker();
        CratePicker.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(CratePickerViewModel.IsOpen)) Notify(nameof(IsCratePickerOpen));
        };
        Notify(nameof(CratePicker));
    }

    public async Task OpenTagEditorAsync(TrackRow row)
    {
        if (TagEditor is null) return;
        await TagEditor.OpenForAsync(row.TrackId, row.Artist, row.Title);
        IsTagEditorOpen = true;
    }

    public void OpenTrackActions(TrackRow row)
    {
        TrackActions.Open(row);
        IsTrackActionsOpen = true;
    }

    public async Task OpenCratePickerAsync(TrackRow row)
    {
        if (CratePicker is null) return;
        await CratePicker.OpenAsync(row);
    }

    private void Notify([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
