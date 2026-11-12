using Xunit;

namespace Sholto.TestSupport.Platform;

/// <summary>A <see cref="FactAttribute"/> that runs on Linux only and is reported skipped, with
/// <paramref name="reason"/>, everywhere else. The reason says what ties the test to Linux.</summary>
public sealed class LinuxOnlyFactAttribute : FactAttribute
{
    public LinuxOnlyFactAttribute(string reason)
    {
        if (!OperatingSystem.IsLinux()) Skip = reason;
    }
}
