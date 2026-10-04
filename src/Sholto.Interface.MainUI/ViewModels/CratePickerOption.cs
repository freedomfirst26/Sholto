namespace Sholto.Interface.MainUI.ViewModels;

/// <summary>One row in the crate picker: either an existing crate, or the
/// "create '<query>'" affordance at the top.</summary>
public sealed record CratePickerOption(bool IsCreate, string Display, int CrateId, int TrackCount);
