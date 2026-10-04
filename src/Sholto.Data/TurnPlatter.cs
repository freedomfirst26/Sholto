namespace Sholto.Data;

/// <summary>The platter of a deck turned by <see cref="Delta"/> ticks. High rate (~100 Hz): handled synchronously, no allocation. <see cref="Shifted"/> is the silent 2x search on the top platter.</summary>
public readonly record struct TurnPlatter(int Deck, int Delta, PlatterSurface Surface, bool Shifted, Origin Origin) : ICommand;
