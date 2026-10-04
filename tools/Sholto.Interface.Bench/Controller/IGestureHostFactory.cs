namespace Sholto.Interface.Bench.Controller;

/// <summary>Builds a <see cref="GestureHost"/> over a fresh scripted surface and manual clock.</summary>
public interface IGestureHostFactory
{
    GestureHost Create();
}
