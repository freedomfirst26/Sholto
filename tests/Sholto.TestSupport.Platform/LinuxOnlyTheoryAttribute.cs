using Xunit;

namespace Sholto.TestSupport.Platform;

/// <summary>A <see cref="TheoryAttribute"/> that runs on Linux only and is reported skipped, with
/// <paramref name="reason"/>, everywhere else. A separate type from <see cref="LinuxOnlyFactAttribute"/>
/// because xunit discovers theories through their own discoverer.</summary>
public sealed class LinuxOnlyTheoryAttribute : TheoryAttribute
{
    public LinuxOnlyTheoryAttribute(string reason)
    {
        if (!OperatingSystem.IsLinux()) Skip = reason;
    }
}
