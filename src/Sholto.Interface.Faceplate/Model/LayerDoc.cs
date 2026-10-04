namespace Sholto.Interface.Faceplate.Model;

/// <summary>A way of reading the whole board. "Plain" is the board with nothing held;
/// the others are named after the modifier that produces them.</summary>
/// <param name="Modifier">The control you hold to enter this layer, or null for plain.</param>
public sealed record LayerDoc(string Id, string Name, string? Modifier = null);
