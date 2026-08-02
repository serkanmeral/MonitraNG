using MngLogCollector.Application.Services.Discovery;

namespace MngLogCollector.Tests;

public class IdentityClassifierTests
{
    [Fact]
    public void Windows_Rdp_Smb_IsHighConfidence()
    {
        var c = IdentityClassifier.Classify([445, 3389], "windows", new ServiceFingerprintSignals());
        Assert.Equal("windows", c.OsFamilyHint);
        Assert.Equal("high", c.IdentityConfidence);
        Assert.Contains("Windows", c.IdentitySummary);
    }

    [Fact]
    public void Linux_Ssh_Banner_IsLinux()
    {
        var c = IdentityClassifier.Classify(
            [22],
            "linux",
            new ServiceFingerprintSignals { SshBanner = "SSH-2.0-OpenSSH_9.2p1 Debian-2" });
        Assert.Equal("linux", c.OsFamilyHint);
        Assert.NotEqual("low", c.IdentityConfidence);
    }

    [Fact]
    public void Printer_HttpTitle_IsPrinterHigh()
    {
        var c = IdentityClassifier.Classify(
            [80, 443],
            "unknown",
            new ServiceFingerprintSignals { HttpTitle = "HP LaserJet Pro" });
        Assert.Equal("printer", c.DeviceRoleHint);
        Assert.Equal("high", c.IdentityConfidence);
    }
}
