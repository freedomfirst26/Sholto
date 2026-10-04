namespace Sholto.Data;

/// <summary>The library database is up (or known to be unavailable): tag and crate queries and commands now work. State.</summary>
public readonly record struct LibraryDatabaseAttached(bool Available) : IStateEvent
{
    public int Slot => 0;
}
