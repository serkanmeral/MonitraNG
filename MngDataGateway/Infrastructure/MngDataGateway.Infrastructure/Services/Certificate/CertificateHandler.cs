using MngDataGateway.Application.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace MngDataGateway.Infrastructure.Services.Certificate
{
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

        private static X509Certificate2 GetSignedCertificate(Serilog.Core.Logger log, MngDataGatewaySettings settings)
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
                log.Information("Signed Cert Issuer : {0}", certWithKey.Issuer);
            }
            catch (Exception ex)
            {
                log.Error(ex, "Signed Cert Loading Error");
            }

            return new X509Certificate2(certWithKey.Export(X509ContentType.Pkcs12));
        }

        private static X509Certificate2 CreateSelfSignedCertificate(Serilog.Core.Logger log, string dns)
        {
            string countryName = "TR";
            string stateOrProvinceName = "ISTANBUL";
            string localityName = "UMRANIYE";
            string organizationName = "iSIMPLATFORM A.S.";
            string commonName = dns;
            string dnsName = dns;  // DNS adını buraya ekleyin

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
                request.CertificateExtensions.Add(sanBuilder.Build());

                // Sertifika oluştur
                var cert = request.CreateSelfSigned(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddYears(1));

                log.Information("Self Signed Cert Issuer : {0}", cert.Issuer);

                // .NET Core 3.0 ve üzerinde önerilen bir yöntemle sertifikayı X509Certificate2'ye dönüştür

                var certificate = new X509Certificate2(cert.Export(X509ContentType.Pkcs12));

                return certificate;
            }
        }

        public static X509Certificate2 GetCertificate(Serilog.Core.Logger log, MngDataGatewaySettings settings)
        {

            return string.IsNullOrEmpty(settings.CertificateSettings.DNS)
                ? GetSignedCertificate(log, settings)
                : CreateSelfSignedCertificate(log, settings.CertificateSettings.DNS);
        }
    }
}
