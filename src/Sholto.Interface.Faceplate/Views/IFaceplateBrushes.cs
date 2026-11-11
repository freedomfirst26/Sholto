using Avalonia.Media;

namespace Sholto.Interface.Faceplate.Views;

/// <summary>The Faceplate's amber language as the board and its factories read it. The
/// theme-bound <see cref="FaceplateBrushes"/> implements it; <c>Attach</c> stays on that
/// concrete class because only the overlay binds it to a host.</summary>
public interface IFaceplateBrushes
{
    /// <summary>Resting tint on a control Sholto acts on.</summary>
    IBrush AmberRest { get; }

    /// <summary>Resting tint under a pointer.</summary>
    IBrush AmberHoverFill { get; }

    /// <summary>Held selection fill.</summary>
    IBrush AmberSelectedFill { get; }

    /// <summary>Hover bloom, click blink and sustained selection colour.</summary>
    Color AmberGlow { get; }

    /// <summary>The bloom, as a brush.</summary>
    IBrush AmberGlowBrush { get; }
}
