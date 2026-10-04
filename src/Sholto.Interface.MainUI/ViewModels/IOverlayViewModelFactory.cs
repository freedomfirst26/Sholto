namespace Sholto.Interface.MainUI.ViewModels;

/// <summary>Builds the overlay view models that only exist once the database is attached.</summary>
public interface IOverlayViewModelFactory
{
    TagEditorViewModel TagEditor();
    CratePickerViewModel CratePicker();
}
