using MngKeeper.Application.Directory;
using MngKeeper.Domain.Entities;
using Xunit;

namespace MngKeeper.Application.Tests.Directory;

public class DirectoryPasswordPolicyTests
{
    [Fact]
    public void Default_domain_does_not_write_to_active_directory()
    {
        Assert.False(DirectoryPasswordPolicy.IsAdWriteEnabled(new DomainSettings()));
        Assert.False(DirectoryPasswordPolicy.IsAdWriteEnabled(null));
    }

    [Fact]
    public void Keycloak_mode_stays_closed_even_when_the_directory_bind_is_filled()
    {
        var settings = SelfServiceSettings();
        settings.Password!.Mode = DomainPasswordModes.Keycloak;

        Assert.False(DirectoryPasswordPolicy.IsAdWriteEnabled(settings));
        Assert.False(DirectoryPasswordPolicy.IsAdminResetEnabled(settings));
    }

    [Fact]
    public void Self_service_opens_without_a_personnel_container_or_reset_account()
    {
        var settings = SelfServiceSettings();

        Assert.True(DirectoryPasswordPolicy.IsAdWriteEnabled(settings));
        Assert.False(DirectoryPasswordPolicy.IsAdminResetEnabled(settings));

        var missingThumbprint = SelfServiceSettings();
        missingThumbprint.Password!.ServerCertificateThumbprint = " ";
        Assert.False(DirectoryPasswordPolicy.IsAdWriteEnabled(missingThumbprint));
    }

    [Fact]
    public void Admin_reset_opens_only_when_the_reset_account_and_personnel_dn_are_filled()
    {
        var settings = SelfServiceSettings();
        settings.Password!.PersonnelDn = "OU=Personnel,DC=example,DC=local";
        settings.Password.ResetBindUsername = "CN=svc-reset,OU=Service,DC=example,DC=local";
        settings.Password.ResetBindPassword = "reset-secret";

        Assert.True(DirectoryPasswordPolicy.IsAdminResetEnabled(settings));

        settings.Password.ResetBindPassword = "";
        Assert.False(DirectoryPasswordPolicy.IsAdminResetEnabled(settings));
        Assert.True(DirectoryPasswordPolicy.IsAdWriteEnabled(settings));
    }

    private static DomainSettings SelfServiceSettings() => new()
    {
        DirectoryLdap = new DirectoryLdapSettings
        {
            Enabled = true,
            UseSsl = true,
            Host = "dc01.example.local",
            Port = 636,
            BaseDn = "DC=example,DC=local",
            BindUsername = "CN=svc-read,DC=example,DC=local",
            BindPassword = "read-secret"
        },
        Password = new DomainPasswordSettings
        {
            Mode = DomainPasswordModes.KeycloakAndAd,
            ServerCertificateThumbprint = "BEF42AFA8D9A89F7328D70F6F87882DE46B62615"
        }
    };
}
