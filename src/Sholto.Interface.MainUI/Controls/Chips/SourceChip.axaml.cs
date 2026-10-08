using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Sholto.Interface.MainUI.Controls.Chips;

/// <summary>A crate, tag or songs chip with a × button. Search's query box and the Track List strip both show
/// it, so the two cannot drift apart. The × raises <see cref="RemovedEvent"/> with the chip as the sender; the
/// chip's DataContext tells the handler which item to remove.</summary>
public partial class SourceChip : UserControl
{
    public static readonly StyledProperty<SourceChipKind> KindProperty =
        AvaloniaProperty.Register<SourceChip, SourceChipKind>(nameof(Kind));

    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<SourceChip, string?>(nameof(Text));

    public static readonly RoutedEvent<RoutedEventArgs> RemovedEvent =
        RoutedEvent.Register<SourceChip, RoutedEventArgs>(nameof(Removed), RoutingStrategies.Bubble);

    public SourceChip()
    {
        InitializeComponent();
        foreach (var name in new[] { "CrateRemove", "TagRemove", "SongsRemove" })
            this.FindControl<Border>(name)!.PointerPressed += OnRemovePressed;
        Refresh();
    }

    public SourceChipKind Kind
    {
        get => GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public event EventHandler<RoutedEventArgs>? Removed
    {
        add => AddHandler(RemovedEvent, value);
        remove => RemoveHandler(RemovedEvent, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == KindProperty || change.Property == TextProperty) Refresh();
    }

    private void Refresh()
    {
        if (this.FindControl<Panel>("CratePart") is not { } crate) return;
        crate.IsVisible = Kind == SourceChipKind.Crate;
        this.FindControl<Border>("TagPart")!.IsVisible = Kind == SourceChipKind.Tag;
        this.FindControl<Border>("SongsPart")!.IsVisible = Kind == SourceChipKind.Songs;
        this.FindControl<TextBlock>("CrateText")!.Text = Text;
        this.FindControl<TextBlock>("TagText")!.Text = Text;
        this.FindControl<TextBlock>("SongsText")!.Text = Text;
    }

    private void OnRemovePressed(object? sender, PointerPressedEventArgs e)
    {
        RaiseEvent(new RoutedEventArgs(RemovedEvent, this));
        e.Handled = true;
    }
}
