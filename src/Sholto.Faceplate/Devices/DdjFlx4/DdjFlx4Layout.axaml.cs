using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Sholto.Faceplate.Devices.DdjFlx4;

/// <summary>The drawing of the DDJ-FLX4. All of it lives in the .axaml — this file
/// exists only because <c>x:Class</c> needs a partial class to attach to.</summary>
public partial class DdjFlx4Layout : UserControl
{
    public DdjFlx4Layout() => AvaloniaXamlLoader.Load(this);
}
