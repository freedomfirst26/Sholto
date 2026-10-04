using Sholto.App.Analysis.Analyzers.Keys;

namespace Sholto.App.Storage;

/// <summary>Binary (de)serialization of <see cref="KeyAnalysis"/> for the key analyses table.</summary>
internal interface IKeyAnalysisSerializer
{
    byte[] Encode(KeyAnalysis a);

    KeyAnalysis? Decode(byte[] blob);
}
