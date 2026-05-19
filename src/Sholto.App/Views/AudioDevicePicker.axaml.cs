using Avalonia.Controls;
using Avalonia.Interactivity;
using Sholto.Audio;

namespace Sholto.App.Views;

public partial class AudioDevicePicker : Window
{
    public AudioDevice? SelectedDevice { get; private set; }

    public AudioDevicePicker()
    {
        InitializeComponent();
    }

    public AudioDevicePicker(IReadOnlyList<AudioDevice> devices, string? currentName) : this()
    {
        var listBox = this.FindControl<ListBox>("DeviceList")!;
        listBox.ItemsSource = devices;

        if (currentName is not null)
        {
            for (int i = 0; i < devices.Count; i++)
            {
                if (devices[i].Name == currentName)
                {
                    listBox.SelectedIndex = i;
                    break;
                }
            }
        }

        if (listBox.SelectedIndex < 0 && devices.Count > 0)
            listBox.SelectedIndex = 0;
    }

    private void OnConfirm(object? sender, RoutedEventArgs e)
    {
        var listBox = this.FindControl<ListBox>("DeviceList")!;
        SelectedDevice = listBox.SelectedItem as AudioDevice;
        Close();
    }

    private void OnCancel(object? sender, RoutedEventArgs e)
    {
        SelectedDevice = null;
        Close();
    }
}
