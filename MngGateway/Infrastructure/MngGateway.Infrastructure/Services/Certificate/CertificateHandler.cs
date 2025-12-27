using MngGateway.Application.Configuration;
using Serilog;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace MngGateway.Infrastructure.Services.Certificate;

public class CertificateHandler
{
    private static byte[] UnPem(string pem)
    {
        // This is a shortcut that assumes valid PEM
        // -----BEGIN words-----\nbase64\n-----END words-----
        const string Dashes = "-----";
        int index0 = pem.IndexOf(Dashes);
        int index1 = pem.IndexOf('\n', index0 + Dashes.Length);
        int index2 = pem.IndexOf(Dashes, index1 + 1);

        return Convert.FromBase64String(pem.Substring(index1, index2 - index1));
    }

    private static X509Certificate2 GetSignedCertificate(ILogger log, MngGatewaySettings settings)
    {
        X509Certificate2 certWithKey = new X509Certificate2();

        try
        {
            string crtVal = !string.IsNullOrEmpty(settings.CertificateSettings.MNG_CERT_FILE_CONTENT)
                ? settings.CertificateSettings.MNG_CERT_FILE_CONTENT
                : File.ReadAllText(settings.CertificateSettings.MNG_CERT_FILE);

            string keyVal = !string.IsNullOrEmpty(settings.CertificateSettings.MNG_KEY_FILE_CONTENT)
                ? settings.CertificateSettings.MNG_KEY_FILE_CONTENT
                : File.ReadAllText(settings.CertificateSettings.MNG_KEY_FILE);

            byte[] keyDer = UnPem(keyVal);
            byte[] crtDer = UnPem(crtVal);

            using (X509Certificate2 certOnly = new X509Certificate2(crtDer))
            using (RSA rsa = RSA.Create(2048))
            {
                rsa.ImportPkcs8PrivateKey(keyDer, out _);
                certWithKey = certOnly.CopyWithPrivateKey(rsa);
            }
            log.Information("Signed Cert Issuer : {Issuer}", certWithKey.Issuer);
        }
        catch (Exception ex)
        {
            log.Error(ex, "Signed Cert Loading Error");
        }

        return new X509Certificate2(certWithKey.Export(X509ContentType.Pkcs12));
    }

    private static X509Certificate2 CreateSelfSignedCertificate(ILogger log, string dns)
    {
        string countryName = "TR";
        string stateOrProvinceName = "ISTANBUL";
        string localityName = "UMRANIYE";
        string organizationName = "Serkan MERAL";
        string commonName = dns;
        string dnsName = dns;

        using (RSA rsa = RSA.Create(2048))
        {
            var request = new CertificateRequest(
                new X500DistinguishedName($"C={countryName}, ST={stateOrProvinceName}, L={localityName}, O={organizationName}, CN={commonName}"),
                rsa,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);

            // Subject Alternative Name ekleyerek DNS adını belirtin
            var sanBuilder = new SubjectAlternativeNameBuilder();
            sanBuilder.AddDnsName(dnsName);
            sanBuilder.AddIpAddress(System.Net.IPAddress.Parse("127.0.0.1"));
            sanBuilder.AddIpAddress(System.Net.IPAddress.Parse("::1"));
            request.CertificateExtensions.Add(sanBuilder.Build());

            // Sertifika oluştur
            var cert = request.CreateSelfSigned(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddYears(1));

            log.Information("Self Signed Cert Issuer : {Issuer}", cert.Issuer);

            var certificate = new X509Certificate2(cert.Export(X509ContentType.Pkcs12));

            return certificate;
        }
    }

    public static X509Certificate2 GetCertificate(ILogger log, MngGatewaySettings settings)
    {
        return string.IsNullOrEmpty(settings.CertificateSettings.DNS)
            ? GetSignedCertificate(log, settings)
            : CreateSelfSignedCertificate(log, settings.CertificateSettings.DNS);
    }
}

