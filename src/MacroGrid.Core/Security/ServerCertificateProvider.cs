using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace MacroGrid.Core.Security;

/// <summary>The server's own self-signed certificate for <c>wss://</c>/<c>https://</c>, used instead of a
/// certificate authority: the pairing QR carries the certificate's SHA-256 fingerprint (see
/// <see cref="Fingerprint"/>), and a phone pins that fingerprint on first pairing instead of validating a
/// chain. Generated once (ECDSA P-256, 10-year validity) and kept in <c>tls-cert.dat</c> in the data
/// folder, its private key protected the same way a paired device's token is (<see cref="ISecretProtector"/>).
/// Regenerating it (the file removed, or undecryptable — another Windows user or PC) invalidates every phone's
/// pinned fingerprint; they pair again the same way a device does after a token it cannot decrypt is dropped.</summary>
public sealed class ServerCertificateProvider
{
    private const string FileName = "tls-cert.dat";
    private static readonly TimeSpan Validity = TimeSpan.FromDays(3650);

    public X509Certificate2 Certificate { get; }

    /// <summary>Lowercase hex SHA-256 of the certificate's DER bytes — what the pairing QR's <c>fp</c> query
    /// parameter carries and what a phone compares its TLS connection's certificate against.</summary>
    public string Fingerprint { get; }

    public ServerCertificateProvider(string dataDir, ISecretProtector protector)
    {
        var path = Path.Combine(dataDir, FileName);
        Certificate = Load(path, protector) ?? Create(path, protector);
        Fingerprint = Convert.ToHexStringLower(Certificate.GetCertHash(HashAlgorithmName.SHA256));
    }

    private static X509Certificate2? Load(string path, ISecretProtector protector)
    {
        if (!File.Exists(path)) return null;
        try
        {
            var pfxBytes = Convert.FromBase64String(protector.Unprotect(File.ReadAllText(path)) ?? "");
            var cert = X509CertificateLoader.LoadPkcs12(pfxBytes, password: null, X509KeyStorageFlags.Exportable);
            // A cert generated with the validity below but sitting unused past it (the PC's clock moved, or the
            // file is very old) is replaced rather than served expired.
            return cert.NotAfter > DateTime.Now ? cert : null;
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            return null;
        }
    }

    private static X509Certificate2 Create(string path, ISecretProtector protector)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest("CN=Macro Grid", key, HashAlgorithmName.SHA256);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, false));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid("1.3.6.1.5.5.7.3.1")], false)); // Server Authentication

        var now = DateTimeOffset.UtcNow;
        using var created = request.CreateSelfSigned(now.AddMinutes(-5), now.Add(Validity));
        // CreateSelfSigned's result does not carry an exportable private key on Windows; re-import it as PFX bytes to get one.
        var pfxBytes = created.Export(X509ContentType.Pfx);
        File.WriteAllText(path, protector.Protect(Convert.ToBase64String(pfxBytes)));
        return X509CertificateLoader.LoadPkcs12(pfxBytes, password: null, X509KeyStorageFlags.Exportable);
    }
}
