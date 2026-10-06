using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.LogicalTree;

namespace Sholto.Interface.MainUI.Controls.Modal;

/// <summary>The chrome every menu modal shares: scrim, raised panel, eyebrow/title/subtitle, one standard
/// button bar and a key-hint line. The content view model is <see cref="Modal"/>; its view is the
/// <see cref="Body"/>. Keys are not handled here: <see cref="IModalKeyRouter"/> does that.</summary>
public partial class ModalShell : UserControl
{
    private const double NarrowWidth = 440;
    private const double RegularWidth = 600;
    private const double WideWidth = 900;
    private const double PanelMargin = 20;

    public static readonly StyledProperty<IModalContent?> ModalProperty =
        AvaloniaProperty.Register<ModalShell, IModalContent?>(nameof(Modal));

    public static readonly StyledProperty<object?> BodyProperty =
        AvaloniaProperty.Register<ModalShell, object?>(nameof(Body));

    public static readonly StyledProperty<object?> HeaderAccessoryProperty =
        AvaloniaProperty.Register<ModalShell, object?>(nameof(HeaderAccessory));

    public static readonly StyledProperty<bool> IsBodyFlushProperty =
        AvaloniaProperty.Register<ModalShell, bool>(nameof(IsBodyFlush));

    public static readonly StyledProperty<bool> ShowsItselfProperty =
        AvaloniaProperty.Register<ModalShell, bool>(nameof(ShowsItself), true);

    /// <summary>Set on one element inside <see cref="Body"/>: the shell focuses it each time the modal opens.</summary>
    public static readonly AttachedProperty<bool> FocusOnOpenProperty =
        AvaloniaProperty.RegisterAttached<ModalShell, Control, bool>("FocusOnOpen");

    private IModalContent? _subscribed;

    public ModalShell()
    {
        InitializeComponent();
        Refresh();
    }

    public static bool GetFocusOnOpen(Control element) => element.GetValue(FocusOnOpenProperty);

    public static void SetFocusOnOpen(Control element, bool value) => element.SetValue(FocusOnOpenProperty, value);

    public IModalContent? Modal
    {
        get => GetValue(ModalProperty);
        set => SetValue(ModalProperty, value);
    }

    public object? Body
    {
        get => GetValue(BodyProperty);
        set => SetValue(BodyProperty, value);
    }

    public object? HeaderAccessory
    {
        get => GetValue(HeaderAccessoryProperty);
        set => SetValue(HeaderAccessoryProperty, value);
    }

    /// <summary>The body spans the panel edge to edge (list pickers' highlight rows). Default false: a 22 px inset.</summary>
    public bool IsBodyFlush
    {
        get => GetValue(IsBodyFlushProperty);
        set => SetValue(IsBodyFlushProperty, value);
    }

    /// <summary>True: visibility follows <c>Modal.IsOpen</c>. False: always visible, an outer host gates it.</summary>
    public bool ShowsItself
    {
        get => GetValue(ShowsItselfProperty);
        set => SetValue(ShowsItselfProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ModalProperty) Subscribe(change.GetNewValue<IModalContent?>());
        else if (change.Property == BodyProperty) Presenter(nameof(BodyPresenter)).SetValue(ContentPresenter.ContentProperty, Body);
        else if (change.Property == HeaderAccessoryProperty) Presenter(nameof(AccessoryPresenter)).SetValue(ContentPresenter.ContentProperty, HeaderAccessory);
        else if (change.Property == IsBodyFlushProperty) ApplyBodyInset();
        else if (change.Property == ShowsItselfProperty) Refresh();
        else if (change.Property == BoundsProperty) ApplyMaxHeight();
    }

    private ContentPresenter Presenter(string name) => this.FindControl<ContentPresenter>(name)!;

    private void Subscribe(IModalContent? modal)
    {
        var wasOpen = _subscribed?.IsOpen ?? false;
        if (_subscribed is not null) _subscribed.PropertyChanged -= OnModalChanged;
        _subscribed = modal;
        if (modal is not null) modal.PropertyChanged += OnModalChanged;
        this.FindControl<Grid>("Inner")!.DataContext = modal;
        Refresh();
        if (modal is { IsOpen: true } && !wasOpen) PostFocus();
    }

    private void OnModalChanged(object? sender, PropertyChangedEventArgs e)
    {
        Refresh();
        if (e.PropertyName == nameof(IModal.IsOpen) && _subscribed is { IsOpen: true }) PostFocus();
    }

    private void Refresh()
    {
        var modal = _subscribed;
        IsVisible = !ShowsItself || modal is { IsOpen: true };
        if (modal is null) return;

        var buttons = modal.Buttons;
        var hasPrimary = buttons.Primary is not null;
        var dismiss = this.FindControl<Button>("DismissButton")!;
        var back = this.FindControl<Button>("BackButton")!;
        var primary = this.FindControl<Button>("PrimaryButton")!;

        dismiss.Content = buttons.Dismiss;
        dismiss.SetValue(Grid.ColumnProperty, hasPrimary ? 0 : 3);
        dismiss.Classes.Set("ghost", hasPrimary);
        dismiss.Classes.Set("secondary", !hasPrimary);

        back.IsVisible = buttons.Back is not null;
        back.Content = buttons.Back;
        back.IsEnabled = modal.CanGoBack;

        primary.IsVisible = hasPrimary;
        primary.Content = buttons.Primary;
        primary.IsEnabled = modal.CanConfirm;

        this.FindControl<TextBlock>("EyebrowText")!.Classes.Set("attention", modal.Tone == ModalTone.Attention);
        this.FindControl<TextBlock>("SubtitleText")!.IsVisible = modal.Subtitle is not null;
        this.FindControl<TextBlock>("HintText")!.IsVisible = !string.IsNullOrEmpty(modal.KeyHint);
        this.FindControl<Border>("Panel")!.Width = modal.Width switch
        {
            ModalWidth.Narrow => NarrowWidth,
            ModalWidth.Regular => RegularWidth,
            _ => WideWidth,
        };
        ApplyBodyInset();
        ApplyMaxHeight();
    }

    private void ApplyBodyInset() =>
        Presenter(nameof(BodyPresenter)).Margin = IsBodyFlush ? new Thickness(-22, 0) : default;

    private void ApplyMaxHeight()
    {
        var panel = this.FindControl<Border>("Panel")!;
        panel.MaxHeight = Bounds.Height > 0 ? Math.Max(0, Bounds.Height - 2 * PanelMargin) : double.PositiveInfinity;
    }

    private void PostFocus() => Dispatcher.UIThread.Post(FocusTarget);

    /// <summary>The first element in the body marked <see cref="FocusOnOpenProperty"/>, or null.</summary>
    internal Control? FindFocusTarget()
    {
        if (Body is not Control body) return null;
        if (GetFocusOnOpen(body)) return body;
        foreach (var descendant in body.GetLogicalDescendants())
            if (descendant is Control c && GetFocusOnOpen(c)) return c;
        return null;
    }

    private void FocusTarget() => FindFocusTarget()?.Focus();

    private void OnScrimPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_subscribed is { ScrimClick: ModalScrimClick.Dismisses } modal) modal.Dismiss();
        e.Handled = true;
    }

    private void OnPanelPressed(object? sender, PointerPressedEventArgs e) => e.Handled = true;

    private void OnDismissClick(object? sender, RoutedEventArgs e) => _subscribed?.Dismiss();

    private void OnBackClick(object? sender, RoutedEventArgs e) => _subscribed?.Back();

    private void OnPrimaryClick(object? sender, RoutedEventArgs e) => _subscribed?.Confirm();
}
