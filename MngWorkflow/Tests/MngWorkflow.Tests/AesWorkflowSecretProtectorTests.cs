using Microsoft.Extensions.Options;
using MngWorkflow.Application.Configuration;
using MngWorkflow.Infrastructure.Secrets;
using Xunit;

namespace MngWorkflow.Tests.Secrets;

public sealed class AesWorkflowSecretProtectorTests
{
    [Fact]
    public void Roundtrip_protect_unprotect()
    {
        var key = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("0123456789abcdefghijklmnopqrstuv"));
        var protector = new AesWorkflowSecretProtector(Options.Create(new MngWorkflowSettings
        {
            Secrets = new SecretSettings { EncryptionKeyBase64 = key }
        }));

        var cipher = protector.Protect("my-api-token");
        var plain = protector.Unprotect(cipher);
        Assert.Equal("my-api-token", plain);
    }
}
