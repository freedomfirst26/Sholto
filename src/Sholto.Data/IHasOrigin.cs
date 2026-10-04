namespace Sholto.Data;

/// <summary>Implemented by every command: reports the <see cref="Data.Origin"/> that issued it.</summary>
public interface IHasOrigin
{
    Origin Origin { get; }
}
