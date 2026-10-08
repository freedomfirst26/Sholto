using System;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Sholto.Interface.MainUI.Views;

/// <summary>The Track List while it is empty: a plate and the keys that fill it. Binds to an
/// <c>ITrackListViewModel</c> for its visibility; a click on the plate raises <see cref="SearchRequested"/>.</summary>
public partial class TrackListEmpty : UserControl
{
    public TrackListEmpty() => InitializeComponent();

    /// <summary>The plate was clicked: the window opens search.</summary>
    public event EventHandler? SearchRequested;

    private void OnPlateClick(object? sender, RoutedEventArgs e) => SearchRequested?.Invoke(this, EventArgs.Empty);
}
