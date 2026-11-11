using System.ComponentModel;
using System.Runtime.CompilerServices;
using Sholto.Interface.MainUI.Controls.CollapseToIcon;

namespace Sholto.Interface.MainUI.ViewModels;

/// <summary>The controller guide's mount, open state and dock. Raises only <c>Faceplate</c>, <c>Mount</c> and
/// <c>IsFaceplateOpen</c>, names the main view model also has, so it relays them verbatim.</summary>
public sealed class FaceplateGuideCoordinator(ICollapseToIconSequence faceplateDock) : IFaceplateGuideCoordinator
{
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>The controller guide's own state. Null until <see cref="AttachFaceplate"/>
    /// runs (MainWindow's code-behind does this once the mounted overlay has built its
    /// own view model from <c>FaceplateDocLoader.Load(IDeviceFaceplate)</c>) — there is
    /// deliberately no second, separately-constructed instance here; see the MainWindow
    /// constructor for why that would silently break the panel.</summary>
    public Sholto.Interface.Faceplate.ViewModels.FaceplateViewModel? Faceplate { get; private set; }

    /// <summary>Wires up the guide's own view model (see <see cref="Faceplate"/>) once
    /// the overlay hosting it exists. State
    /// owned by another part of the app, handed in after construction.</summary>
    public void AttachFaceplate(Sholto.Interface.Faceplate.ViewModels.FaceplateViewModel faceplate)
    {
        if (ReferenceEquals(Faceplate, faceplate)) return;
        Faceplate = faceplate;
        Faceplate.RequestClose += () => IsFaceplateOpen = false;
        Notify(nameof(Faceplate));
    }

    /// <summary>When the controller guide is open, collapsing into its top-bar icon, hinting or idle. The
    /// view reads it to animate; <see cref="Mount"/> drives it.</summary>
    public ICollapseToIconSequence FaceplateDock { get; } = faceplateDock;

    private Sholto.Interface.Faceplate.FaceplateMount _mount = Sholto.Interface.Faceplate.FaceplateMount.Hidden;
    /// <summary>Where the controller guide is shown — see
    /// <see cref="Sholto.Interface.Faceplate.FaceplateMount"/>. <see cref="IsFaceplateOpen"/>
    /// derives from this (anything but <c>Hidden</c> counts as open) rather than being
    /// independent state. Opening sets <c>Full</c>; closing sets <c>Hidden</c>.
    /// <c>Embedded</c> is wired but not built — selecting it behaves exactly like
    /// <c>Full</c> until a real embedded layout exists, so it is never a
    /// half-built layout and never a dead value nothing shows.</summary>
    public Sholto.Interface.Faceplate.FaceplateMount Mount
    {
        get => _mount;
        set
        {
            if (_mount == value) return;
            var wasOpen = _mount != Sholto.Interface.Faceplate.FaceplateMount.Hidden;
            _mount = value;
            var isOpen = _mount != Sholto.Interface.Faceplate.FaceplateMount.Hidden;
            Notify();
            Notify(nameof(IsFaceplateOpen));
            if (wasOpen == isOpen) return;
            // The guide tells the App whether Inspect mode is on (SetInspectMode); the App owns that state.
            Faceplate?.SetMounted(isOpen);
            // Drop any standing selection without re-raising RequestClose — that event
            // is how the overlay's OWN close button tells us to close; looping back into
            // it here would just re-enter this setter (safely, since the equality check
            // above short-circuits it, but needlessly).
            if (!isOpen) Faceplate?.ClearSelection();
            // Inspect is already off and the selection gone: only now does the guide start shrinking into its icon.
            if (isOpen) FaceplateDock.Open();
            else FaceplateDock.Collapse();
        }
    }

    /// <summary>Whether the controller guide overlay is showing. The only way in is the
    /// top-bar button. Three ways set it back to false: Esc, the overlay's own visible
    /// close button, and the same top-bar button — deliberately NOT a backdrop click,
    /// which would dismiss the whole guide (and whatever was selected) on a click that
    /// merely missed a small control by a few pixels. A thin bool view over
    /// <see cref="Mount"/>: true sets <c>Full</c>, false sets <c>Hidden</c>. Mounting the guide
    /// turns Inspect mode on (Mount's setter tells the Faceplate view model, which sends
    /// <c>SetInspectMode</c>): that is what lets pressing PLAY on the physical unit explain PLAY
    /// instead of starting a deck.</summary>
    public bool IsFaceplateOpen
    {
        get => Mount != Sholto.Interface.Faceplate.FaceplateMount.Hidden;
        set
        {
            if (IsFaceplateOpen == value) return;
            Mount = value ? Sholto.Interface.Faceplate.FaceplateMount.Full : Sholto.Interface.Faceplate.FaceplateMount.Hidden;
        }
    }

    private void Notify([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
