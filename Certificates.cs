using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace FileSigner321
{
    internal sealed class SigningCertificate : IDisposable
    {
        public SigningCertificate(X509Certificate2 certificate, StoreLocation location)
        {
            Certificate = certificate;
            Location = location;
        }

        public X509Certificate2 Certificate { get; private set; }
        public StoreLocation Location { get; private set; }

        public override string ToString()
        {
            return string.Format(UiText.Get("{0}  |  до {1:dd.MM.yyyy}  |  {2}"),
                Certificate.GetNameInfo(X509NameType.SimpleName, false), Certificate.NotAfter,
                Location == StoreLocation.CurrentUser ? UiText.Get("Текущий пользователь") : UiText.Get("Компьютер"));
        }

        public void Dispose()
        {
            Certificate.Dispose();
        }
    }

    internal static class Certificates
    {
        private const string CodeSigningOid = "1.3.6.1.5.5.7.3.3";
        private const string AnyUsageOid = "2.5.29.37.0";

        public static List<SigningCertificate> ReadStores()
        {
            var results = new List<SigningCertificate>();
            var thumbprints = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (var location in new[] { StoreLocation.CurrentUser, StoreLocation.LocalMachine })
                {
                    using (var store = new X509Store(StoreName.My, location))
                    {
                        store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);
                        foreach (var cert in store.Certificates)
                        {
                            using (cert)
                            {
                                if (IsSuitable(cert) && thumbprints.Add(cert.Thumbprint))
                                    results.Add(new SigningCertificate(new X509Certificate2(cert), location));
                            }
                        }
                    }
                }
                return results.OrderBy(item => item.Certificate.Subject).ToList();
            }
            catch
            {
                foreach (var item in results) item.Dispose();
                throw;
            }
        }

        private static bool IsSuitable(X509Certificate2 certificate)
        {
            var now = DateTime.Now;
            if (!certificate.HasPrivateKey || certificate.NotBefore > now || certificate.NotAfter <= now)
                return false;

            foreach (X509Extension extension in certificate.Extensions)
            {
                switch (extension.Oid.Value)
                {
                    case "2.5.29.19":
                        if (new X509BasicConstraintsExtension(extension, extension.Critical).CertificateAuthority)
                            return false;
                        break;
                    case "2.5.29.15":
                        var usage = new X509KeyUsageExtension(extension, extension.Critical).KeyUsages;
                        if ((usage & X509KeyUsageFlags.KeyCertSign) != 0 ||
                            (usage & X509KeyUsageFlags.DigitalSignature) == 0)
                            return false;
                        break;
                    case "2.5.29.37":
                        var purposes = new X509EnhancedKeyUsageExtension(extension, extension.Critical).EnhancedKeyUsages;
                        if (!purposes.Cast<Oid>().Any(oid => oid.Value == CodeSigningOid || oid.Value == AnyUsageOid))
                            return false;
                        break;
                }
            }
            return true;
        }

        public static string Create(string name, int days)
        {
            if (string.IsNullOrWhiteSpace(name) || !System.Text.RegularExpressions.Regex.IsMatch(name, @"^[\p{L}\p{N}][\p{L}\p{N} ._-]{0,63}$"))
                throw new ArgumentException("Имя должно содержать от 1 до 64 букв, цифр или знаков . _ -.");
            if (days < 1 || days > 36525)
                throw new ArgumentOutOfRangeException("days");

            // Keep the transport key ephemeral. The stored key is exportable for PFX backups.
            var options = new CngKeyCreationParameters
            {
                ExportPolicy = CngExportPolicies.AllowExport | CngExportPolicies.AllowPlaintextExport,
                KeyUsage = CngKeyUsages.Signing
            };
            options.Parameters.Add(new CngProperty("Length", BitConverter.GetBytes(2048), CngPropertyOptions.None));
            using (var key = CngKey.Create(CngAlgorithm.Rsa, null, options))
            using (var rsa = new RSACng(key))
            {
                var request = new CertificateRequest(
                    new X500DistinguishedName("CN=" + name), rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                var purposes = new OidCollection();
                purposes.Add(new Oid(CodeSigningOid));
                request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(purposes, false));
                request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
                request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
                request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
                var now = DateTimeOffset.Now;
                using (var generated = request.CreateSelfSigned(now.AddMinutes(-5), now.AddDays(days)))
                {
                    var password = Guid.NewGuid().ToString("N");
                    byte[] pfx = generated.Export(X509ContentType.Pfx, password);
                    try
                    {
                        using (var cert = new X509Certificate2(pfx, password,
                            X509KeyStorageFlags.UserKeySet | X509KeyStorageFlags.PersistKeySet |
                            X509KeyStorageFlags.Exportable))
                        using (var store = new X509Store(StoreName.My, StoreLocation.CurrentUser))
                        {
                            if (!cert.HasPrivateKey) throw new CryptographicException("Закрытый ключ сертификата не найден.");
                            cert.FriendlyName = name;
                            store.Open(OpenFlags.ReadWrite);
                            store.Add(cert);
                            return cert.Thumbprint;
                        }
                    }
                    finally
                    {
                        Array.Clear(pfx, 0, pfx.Length);
                    }
                }
            }
        }

        public static void ExportBackup(X509Certificate2 certificate, SecureString password, string destination)
        {
            if (!certificate.HasPrivateKey)
                throw new CryptographicException("У сертификата нет доступного закрытого ключа.");

            byte[] backup;
            try
            {
                backup = certificate.Export(X509ContentType.Pfx, password);
            }
            catch (CryptographicException error)
            {
                throw new CryptographicException(
                    "Windows не смогла экспортировать закрытый ключ. Возможно, ключ создан без права экспорта " +
                    "или защищён носителем. " + error.Message, error);
            }

            try
            {
                // Verify that the password opens a backup of this exact certificate with its key.
                using (var restored = new X509Certificate2(backup, password, X509KeyStorageFlags.EphemeralKeySet))
                {
                    if (!restored.HasPrivateKey ||
                        !string.Equals(restored.Thumbprint, certificate.Thumbprint, StringComparison.OrdinalIgnoreCase))
                        throw new CryptographicException("Резервная копия не содержит закрытый ключ выбранного сертификата.");
                }

                string folder = Path.GetDirectoryName(Path.GetFullPath(destination));
                string staged = Path.Combine(folder, ".filesigner-" + Guid.NewGuid().ToString("N") + ".tmp");
                try
                {
                    using (var stream = new FileStream(staged, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                        stream.Write(backup, 0, backup.Length);
                    if (File.Exists(destination))
                        File.Replace(staged, destination, null);
                    else
                        File.Move(staged, destination);
                }
                finally
                {
                    if (File.Exists(staged)) File.Delete(staged);
                }
            }
            finally
            {
                Array.Clear(backup, 0, backup.Length);
            }
        }
    }
}
