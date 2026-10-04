using Sholto.App.Analysis.Analyzers;

namespace Sholto.App.Storage;

/// <summary>Binary (de)serialization of <see cref="BasicAnalysis"/> for the analyses table.</summary>
internal interface IBasicAnalysisSerializer
{
    byte[] Encode(BasicAnalysis a);

    BasicAnalysis? Decode(byte[] blob);
}
