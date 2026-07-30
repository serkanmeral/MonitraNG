using MngLogs.Agent.ServiceWatch;

namespace MngLogs.Tests;

public class ApplicationWatchProbeTests
{
    [Theory]
    [InlineData("notepad", "notepad")]
    [InlineData("notepad.exe", "notepad")]
    [InlineData("  Chrome.EXE ", "Chrome")]
    public void NormalizeProcessName_strips_exe_and_trims(string input, string expected)
    {
        Assert.Equal(expected, ApplicationWatchProbe.NormalizeProcessName(input), ignoreCase: true);
    }

    [Fact]
    public void TryStart_requires_executable_path()
    {
        Assert.False(ApplicationWatchProbe.TryStart(null, null, null, out var error, out var pid));
        Assert.Contains("executablePath", error ?? "", StringComparison.OrdinalIgnoreCase);
        Assert.Null(pid);
    }

    [Fact]
    public void TryStart_rejects_missing_file()
    {
        Assert.False(ApplicationWatchProbe.TryStart(
            @"C:\Windows\System32\this-file-does-not-exist-mnglogs.exe",
            null,
            null,
            out var error,
            out _));
        Assert.Contains("not found", error ?? "", StringComparison.OrdinalIgnoreCase);
    }
}
