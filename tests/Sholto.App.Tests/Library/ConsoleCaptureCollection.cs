namespace Sholto.App.Tests;

/// <summary>Tests that swap <see cref="Console.Out"/> run alone, so no other test's output lands in the capture.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ConsoleCaptureCollection
{
    public const string Name = "Console capture";
}
