namespace Sholto.Data;

/// <summary>Reset a deck's tempo and beatgrid to the analysed detection.</summary>
public readonly record struct ResetDeckToAnalysis(int Deck, Origin Origin) : ICommand;
