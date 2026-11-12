using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Sholto.App.ExternalTools.Tests;

/// <summary>The Windows installer scripts stay in step with the Linux ones and stay
/// readable by Windows PowerShell 5.1: <c>sholto-deps.ps1</c> carries the same pins as
/// <c>sholto-deps.sh</c>, every <c>.ps1</c> is pure ASCII (5.1 reads a BOM-less UTF-8
/// file as ANSI), and on Windows each one parses without errors.</summary>
public class WindowsInstallerScriptsTests
{
    private readonly string[] _scripts = ["sholto-deps.ps1", "install-deps.ps1", "get-sholto.ps1"];

    [Fact]
    public void Every_pin_in_sholto_deps_sh_has_an_equal_value_in_sholto_deps_ps1()
    {
        var shell = ShellAssignments(Read("sholto-deps.sh"));
        var powershell = PowerShellAssignments(Read("sholto-deps.ps1"));

        Assert.True(shell.Count >= 7, $"expected at least 7 SHOLTO_* pins in sholto-deps.sh, found {shell.Count}");
        foreach (var (name, values) in shell)
        {
            Assert.True(powershell.TryGetValue(name, out var windows), $"${name} is missing from sholto-deps.ps1");
            Assert.Equal(values, windows);
        }
    }

    [Fact]
    public void Every_windows_script_is_ascii_only()
    {
        foreach (var script in _scripts)
        {
            var bytes = File.ReadAllBytes(Path.Combine(RepoRoot(), script));
            var offset = Array.FindIndex(bytes, b => b > 0x7F);
            Assert.True(offset < 0, $"{script} has a non-ASCII byte at offset {offset}");
        }
    }

    [Fact]
    public void Windows_powershell_parses_every_windows_script_without_errors()
    {
        if (!OperatingSystem.IsWindows())
            return; // Windows PowerShell 5.1 exists only on Windows; CI's windows-latest job runs this.

        foreach (var script in _scripts)
        {
            var path = Path.Combine(RepoRoot(), script).Replace("'", "''");
            var command =
                "$errors = $null; " +
                $"[void][System.Management.Automation.Language.Parser]::ParseFile('{path}', [ref]$null, [ref]$errors); " +
                "foreach ($e in $errors) { Write-Output (\"line \" + $e.Extent.StartLineNumber + \": \" + $e.Message) }; " +
                "Write-Output (\"ERRORS=\" + @($errors).Count)";

            var info = new ProcessStartInfo("powershell.exe")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            foreach (var arg in new[] { "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-Command", command })
                info.ArgumentList.Add(arg);

            using var process = Process.Start(info)!;
            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();

            Assert.True(process.ExitCode == 0, $"powershell.exe failed on {script}: {stderr}");
            Assert.True(stdout.Contains("ERRORS=0"), $"{script} does not parse in Windows PowerShell:\n{stdout}{stderr}");
        }
    }

    /// <summary>Top-level <c>SHOLTO_X="value"</c> and <c>SHOLTO_X=(a b c)</c> lines, except the
    /// check-detail scratch variable, which is state, not a pin.</summary>
    private Dictionary<string, string[]> ShellAssignments(string text)
    {
        var result = new Dictionary<string, string[]>();
        foreach (Match m in Regex.Matches(text, @"^(SHOLTO_[A-Z0-9_]+)=(.*)$", RegexOptions.Multiline))
        {
            var name = m.Groups[1].Value;
            if (name == "SHOLTO_CHECK_DETAIL")
                continue;
            var value = m.Groups[2].Value.Trim();
            result[name] = value.StartsWith('(') && value.EndsWith(')')
                ? value[1..^1].Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(Unquote).ToArray()
                : [Unquote(value)];
        }
        return result;
    }

    /// <summary>Top-level <c>$SHOLTO_X = "value"</c> and <c>$SHOLTO_X = @("a", "b")</c> lines.</summary>
    private Dictionary<string, string[]> PowerShellAssignments(string text)
    {
        var result = new Dictionary<string, string[]>();
        foreach (Match m in Regex.Matches(text, @"^\$(SHOLTO_[A-Z0-9_]+)\s*=\s*(.*)$", RegexOptions.Multiline))
        {
            var value = m.Groups[2].Value.Trim();
            result[m.Groups[1].Value] = value.StartsWith("@(") && value.EndsWith(')')
                ? value[2..^1].Split(',', StringSplitOptions.RemoveEmptyEntries).Select(v => Unquote(v.Trim())).ToArray()
                : [Unquote(value)];
        }
        return result;
    }

    private string Unquote(string value) =>
        value.Length >= 2 && (value[0] == '"' || value[0] == '\'') && value[^1] == value[0] ? value[1..^1] : value;

    /// <summary>The file's text with CR stripped, so a CRLF checkout compares the same as LF.</summary>
    private string Read(string file) => File.ReadAllText(Path.Combine(RepoRoot(), file)).Replace("\r", "");

    private string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Sholto.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Sholto.slnx not found above the test binary.");
    }
}
