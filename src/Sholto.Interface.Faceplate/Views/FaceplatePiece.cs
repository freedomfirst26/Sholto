using Avalonia.Controls.Shapes;

namespace Sholto.Interface.Faceplate.Views;

/// <summary>The four parallel shapes the board paints over one control, plus whether the app
/// acts on it and how strong its resting bloom is.</summary>
public sealed record FaceplatePiece(Shape Halo, Shape Bloom, Shape Tint, Shape Outline,
                                    bool Implemented, double RestBloom);
