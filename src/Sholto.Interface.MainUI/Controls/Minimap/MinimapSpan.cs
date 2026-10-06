namespace Sholto.Interface.MainUI.Controls.Minimap;

/// <summary>A horizontal extent on the strip, in DIPs from its left edge.</summary>
public readonly record struct MinimapSpan(double Left, double Right)
{
    public double Width => Right - Left;
}
