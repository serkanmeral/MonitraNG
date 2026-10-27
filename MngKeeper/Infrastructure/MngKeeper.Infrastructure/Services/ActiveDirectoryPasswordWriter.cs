using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Microsoft.Extensions.Logging;
using MngKeeper.Application.Directory;
using MngKeeper.Application.Interfaces;
using MngKeeper.Domain.Entities;
using Novell.Directory.Ldap;

namespace MngKeeper.Infrastructure.Services;

/// <summary>
/// Writes an Active Directory password over LDAPS.
/// The read-only directory bind only looks up the DN. It never sets a password.
/// Linux libldap reports an untrusted DC certificate as "server unavailable", so TLS uses
/// the managed stack and accepts only the pinned thumbprint.
/// </summary>
public class ActiveDirectoryPasswordWriter : IActiveDirectoryPasswordWriter
{
    private readonly ILogger<ActiveDirectoryPasswordWriter> _logger;

    public ActiveDirectoryPasswordWriter(ILogger<ActiveDirectoryPasswordWriter> logger)
    {
        _logger = logger;
    }

    public Task ChangeOwnPasswordAsync(
        MngKeeper.Domain.Entities.Domain domain,
        string username,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var context = RequireSelfService(domain);
        var userDn = FindUserDn(context, username, restrictToDn: null);

        try
        {
            using var connection = Connect(context, userDn, currentPassword);
            connection.Modify(userDn, new[]
            {
                new LdapModification(LdapModification.Delete, new LdapAttribute("unicodePwd", QuotedPassword(currentPassword))),
                new LdapModification(LdapModification.Add, new LdapAttribute("unicodePwd", QuotedPassword(newPassword)))
            });
            _logger.LogInformation("Active Directory password changed for user {Username}", username);
            return Task.CompletedTask;
        }
        catch (DirectoryPasswordException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw MapFailure(ex, username, "change");
        }
    }

    public Task ResetPasswordAsync(
        MngKeeper.Domain.Entities.Domain domain,
        string username,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var context = RequireAdminReset(domain);
        var userDn = FindUserDn(context, username, context.PersonnelDn);

        try
        {
            using var connection = Connect(context, context.ResetBindUsername!, context.ResetBindPassword!);
            connection.Modify(userDn, new[]
            {
                new LdapModification(LdapModification.Replace, new LdapAttribute("unicodePwd", QuotedPassword(newPassword)))
            });
            _logger.LogInformation("Active Directory password reset for user {Username}", username);
            return Task.CompletedTask;
        }
        catch (DirectoryPasswordException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw MapFailure(ex, username, "reset");
        }
    }

    private string FindUserDn(LdapContext context, string username, string? restrictToDn)
    {
        if (string.IsNullOrWhiteSpace(username))
            throw new DirectoryPasswordException("user_not_found", "Directory username is empty.");

        var searchBase = string.IsNullOrWhiteSpace(restrictToDn) ? context.SearchBaseDn : restrictToDn.Trim();

        try
        {
            using var connection = Connect(context, context.LookupUsername, context.LookupPassword);
            var filter = $"(&(objectCategory=person)(objectClass=user)(sAMAccountName={EscapeFilter(username.Trim())}))";
            var constraints = connection.SearchConstraints;
            constraints.ReferralFollowing = false;
            constraints.TimeLimit = 20_000;
            var results = connection.Search(
                searchBase,
                LdapConnection.ScopeSub,
                filter,
                new[] { "distinguishedName" },
                false,
                constraints);

            string? userDn = null;
            var count = 0;
            var skippedReferrals = 0;
            while (skippedReferrals < 8)
            {
                try
                {
                    if (!results.HasMore())
                        break;

                    var entry = results.Next();
                    count++;
                    userDn = entry.Dn;
                    skippedReferrals = 0;
                }
                catch (LdapReferralException)
                {
                    // The domain root also returns references to DNS zones. Those are not user accounts.
                    skippedReferrals++;
                }
            }

            if (count != 1 || string.IsNullOrWhiteSpace(userDn))
            {
                throw new DirectoryPasswordException(
                    restrictToDn == null ? "user_not_found" : "user_not_in_personnel",
                    restrictToDn == null
                        ? "The user was not found in the directory."
                        : "The user was not found under the personnel container.");
            }

            if (restrictToDn != null && !IsUnder(userDn, restrictToDn))
            {
                throw new DirectoryPasswordException(
                    "user_not_in_personnel",
                    "The user is outside the personnel container.");
            }

            return userDn;
        }
        catch (DirectoryPasswordException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw MapFailure(ex, username, "lookup");
        }
    }

    private static LdapContext RequireSelfService(MngKeeper.Domain.Entities.Domain domain)
    {
        if (!DirectoryPasswordPolicy.IsAdWriteEnabled(domain.Settings))
        {
            throw new DirectoryPasswordException(
                "password_change_disabled",
                "Directory password change is not enabled for this domain.");
        }

        return CreateContext(domain, includeResetAccount: false);
    }

    private static LdapContext RequireAdminReset(MngKeeper.Domain.Entities.Domain domain)
    {
        if (!DirectoryPasswordPolicy.IsAdminResetEnabled(domain.Settings))
        {
            throw new DirectoryPasswordException(
                "password_change_disabled",
                "Directory password reset is not enabled for this domain.");
        }

        return CreateContext(domain, includeResetAccount: true);
    }

    private static LdapContext CreateContext(MngKeeper.Domain.Entities.Domain domain, bool includeResetAccount)
    {
        var ldap = domain.Settings.DirectoryLdap!;
        var password = domain.Settings.Password!;
        return new LdapContext(
            ldap.Host.Trim(),
            ldap.Port > 0 ? ldap.Port : 636,
            password.ServerCertificateThumbprint,
            ldap.BaseDn.Trim(),
            string.IsNullOrWhiteSpace(password.PersonnelDn) ? null : password.PersonnelDn.Trim(),
            ldap.BindUsername.Trim(),
            ldap.BindPassword,
            includeResetAccount ? password.ResetBindUsername.Trim() : null,
            includeResetAccount ? password.ResetBindPassword : null);
    }

    private static LdapConnection Connect(LdapContext context, string bindDn, string bindPassword)
    {
        var expected = NormalizeThumbprint(context.Thumbprint);
        var options = new LdapConnectionOptions()
            .UseSsl()
            .ConfigureRemoteCertificateValidationCallback((_, certificate, _, _) =>
                ThumbprintMatches(certificate, expected));

        var connection = new LdapConnection(options)
        {
            ConnectionTimeout = 20_000
        };
        connection.Connect(context.Host, context.Port);
        connection.Bind(bindDn, bindPassword);
        return connection;
    }

    private static bool ThumbprintMatches(X509Certificate? certificate, string expectedThumbprint)
    {
        if (certificate == null)
            return false;
        var actual = NormalizeThumbprint(certificate.GetCertHashString(HashAlgorithmName.SHA1));
        return string.Equals(actual, expectedThumbprint, StringComparison.OrdinalIgnoreCase);
    }

    private DirectoryPasswordException MapFailure(Exception ex, string username, string operation)
    {
        if (ex is LdapException ldap)
        {
            _logger.LogWarning(
                ldap,
                "Active Directory password {Operation} failed for user {Username}. Result {ResultCode}. {LdapMessage}",
                operation,
                username,
                ldap.ResultCode,
                ldap.LdapErrorMessage);
            return new DirectoryPasswordException("ad_rejected", Describe(ldap.ResultCode, operation));
        }

        _logger.LogWarning(ex, "Active Directory password {Operation} failed for user {Username}", operation, username);
        return new DirectoryPasswordException(
            "ad_rejected",
            "Active Directory rejected the password change.");
    }

    private static string Describe(int resultCode, string operation) => resultCode switch
    {
        LdapException.InvalidCredentials when operation == "lookup" =>
            "Active Directory rejected the directory lookup account.",
        LdapException.InvalidCredentials =>
            "Active Directory rejected the current password.",
        LdapException.ConstraintViolation =>
            "Active Directory rejected the new password because it does not meet the password policy.",
        LdapException.UnwillingToPerform =>
            "Active Directory refused the password change.",
        _ when operation == "lookup" =>
            "Active Directory could not be reached to look up the account.",
        _ =>
            "Active Directory rejected the password change."
    };

    private static byte[] QuotedPassword(string password) =>
        Encoding.Unicode.GetBytes($"\"{password}\"");

    private static string EscapeFilter(string value) =>
        value
            .Replace("\\", "\\5c")
            .Replace("*", "\\2a")
            .Replace("(", "\\28")
            .Replace(")", "\\29")
            .Replace("\0", "\\00");

    private static bool IsUnder(string distinguishedName, string parentDn)
    {
        var dn = distinguishedName.Trim();
        var parent = parentDn.Trim();
        return dn.Equals(parent, StringComparison.OrdinalIgnoreCase)
            || dn.EndsWith("," + parent, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeThumbprint(string? value) =>
        new((value ?? string.Empty).Where(char.IsLetterOrDigit).ToArray());

    private sealed record LdapContext(
        string Host,
        int Port,
        string Thumbprint,
        string SearchBaseDn,
        string? PersonnelDn,
        string LookupUsername,
        string LookupPassword,
        string? ResetBindUsername,
        string? ResetBindPassword);
}
