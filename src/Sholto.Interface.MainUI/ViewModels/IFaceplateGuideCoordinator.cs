using System.ComponentModel;
using Sholto.Interface.MainUI.Controls.CollapseToIcon;

namespace Sholto.Interface.MainUI.ViewModels;

/// <summary>Where the controller guide is mounted, whether it is open, and its collapse into the top-bar icon.</summary>
public interface IFaceplateGuideCoordinator : INotifyPropertyChanged
{
    /// <summary>The guide's own view model; null until <see cref="AttachFaceplate"/> runs.</summary>
    Sholto.Interface.Faceplate.ViewModels.FaceplateViewModel? Faceplate { get; }

    /// <summary>Hand in the guide's own view model once the overlay hosting it exists.</summary>
    void AttachFaceplate(Sholto.Interface.Faceplate.ViewModels.FaceplateViewModel faceplate);

    /// <summary>When the controller guide is open, collapsing into its top-bar icon, hinting or idle.</summary>
    ICollapseToIconSequence FaceplateDock { get; }

    /// <summary>Where the controller guide is shown.</summary>
    Sholto.Interface.Faceplate.FaceplateMount Mount { get; set; }

    /// <summary>Whether the controller guide overlay is showing: a bool view over <see cref="Mount"/>.</summary>
    bool IsFaceplateOpen { get; set; }
}
