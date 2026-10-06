namespace Sholto.Interface.Bench.Headless;

/// <summary>Builds a <see cref="IHeadlessHost"/> with the whole headless graph wired.</summary>
public interface IHeadlessHostFactory
{
    /// <summary>Creates the bus, the core, the input stack factory and the host over them.</summary>
    IHeadlessHost Create();
}
