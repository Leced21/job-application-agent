using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Configuration;

namespace JobApplicationAgent.Gateway.OpenIddict;

public sealed class GatewayOpenIddictCertificatePair : IDisposable
{
    internal GatewayOpenIddictCertificatePair(
        IReadOnlyList<X509Certificate2> signingCertificates,
        IReadOnlyList<X509Certificate2> encryptionCertificates)
    {
        SigningCertificates = signingCertificates;
        EncryptionCertificates = encryptionCertificates;
    }

    public IReadOnlyList<X509Certificate2> SigningCertificates { get; }

    public IReadOnlyList<X509Certificate2> EncryptionCertificates { get; }

    // Keep the primary-certificate accessors for callers that only need to inspect
    // the configured current certificate.
    public X509Certificate2 Signing => SigningCertificates[0];

    public X509Certificate2 Encryption => EncryptionCertificates[0];

    public void Dispose()
    {
        foreach (var certificate in SigningCertificates.Concat(EncryptionCertificates))
            certificate.Dispose();
    }
}

public static class GatewayOpenIddictCertificateLoader
{
    public static GatewayOpenIddictCertificatePair Load(IConfiguration configuration, string contentRootPath)
    {
        var loadedCertificates = new List<X509Certificate2>();
        try
        {
            var signingCertificates = LoadUsage(configuration, contentRootPath, "Signing", X509KeyUsageFlags.DigitalSignature, loadedCertificates);
            var encryptionCertificates = LoadUsage(configuration, contentRootPath, "Encryption", X509KeyUsageFlags.KeyEncipherment, loadedCertificates);

            var duplicate = signingCertificates.Concat(encryptionCertificates)
                .GroupBy(certificate => certificate.Thumbprint, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(group => group.Count() > 1);
            if (duplicate is not null)
                throw new InvalidOperationException("Every configured OpenIddict signing and encryption certificate must be different certificates.");

            return new GatewayOpenIddictCertificatePair(signingCertificates, encryptionCertificates);
        }
        catch
        {
            foreach (var certificate in loadedCertificates)
                certificate.Dispose();
            throw;
        }
    }

    private static IReadOnlyList<X509Certificate2> LoadUsage(
        IConfiguration configuration,
        string contentRootPath,
        string usage,
        X509KeyUsageFlags requiredKeyUsage,
        ICollection<X509Certificate2> loadedCertificates)
    {
        var section = configuration.GetSection($"OpenIddict:Certificates:{usage}");
        var certificates = new List<X509Certificate2>
        {
            LoadCertificate(
                section["Path"],
                section["Password"],
                section["PasswordFile"],
                $"OpenIddict:Certificates:{usage}:Path",
                contentRootPath,
                requiredKeyUsage,
                loadedCertificates)
        };

        var previous = section.GetSection("Previous").GetChildren()
            .Select(child => (Section: child, Index: int.TryParse(child.Key, out var index) ? index : (int?)null))
            .OrderBy(child => child.Index ?? int.MaxValue)
            .ThenBy(child => child.Section.Key, StringComparer.Ordinal);

        foreach (var entry in previous)
        {
            if (entry.Index is null || entry.Index < 0)
                throw new InvalidOperationException($"OpenIddict:Certificates:{usage}:Previous keys must use non-negative numeric indexes.");

            certificates.Add(LoadCertificate(
                entry.Section["Path"],
                entry.Section["Password"],
                entry.Section["PasswordFile"],
                $"OpenIddict:Certificates:{usage}:Previous:{entry.Section.Key}:Path",
                contentRootPath,
                requiredKeyUsage,
                loadedCertificates));
        }

        return certificates;
    }

    private static X509Certificate2 LoadCertificate(
        string? configuredPath,
        string? password,
        string? passwordFile,
        string pathSetting,
        string contentRootPath,
        X509KeyUsageFlags requiredKeyUsage,
        ICollection<X509Certificate2> loadedCertificates)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
            throw new InvalidOperationException($"Configure {pathSetting} with a PFX certificate path.");
        password = ResolvePassword(password, passwordFile, pathSetting, contentRootPath);

        var path = Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.Combine(contentRootPath, configuredPath);

        X509Certificate2 certificate;
        try
        {
            certificate = X509CertificateLoader.LoadPkcs12FromFile(path, password, X509KeyStorageFlags.EphemeralKeySet);
        }
        catch (Exception exception) when (exception is CryptographicException or IOException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException($"Could not load the OpenIddict certificate configured by {pathSetting}.", exception);
        }

        loadedCertificates.Add(certificate);
        var now = DateTime.UtcNow;
        if (now < certificate.NotBefore.ToUniversalTime() || now > certificate.NotAfter.ToUniversalTime())
            throw new InvalidOperationException($"The OpenIddict certificate configured by {pathSetting} is not currently valid.");
        if (!certificate.HasPrivateKey)
            throw new InvalidOperationException($"The OpenIddict certificate configured by {pathSetting} must contain a private key.");

        using var rsa = certificate.GetRSAPrivateKey();
        if (rsa is null || rsa.KeySize < 2048)
            throw new InvalidOperationException($"The OpenIddict certificate configured by {pathSetting} must contain an RSA private key of at least 2048 bits.");

        var keyUsage = certificate.Extensions.OfType<X509KeyUsageExtension>().FirstOrDefault();
        if (keyUsage is not null && (keyUsage.KeyUsages & requiredKeyUsage) == 0)
            throw new InvalidOperationException($"The OpenIddict certificate configured by {pathSetting} does not allow its required key usage.");

        return certificate;
    }

    private static string ResolvePassword(
        string? password,
        string? passwordFile,
        string pathSetting,
        string contentRootPath)
    {
        var settingPrefix = pathSetting[..pathSetting.LastIndexOf(':')];
        if (password is not null && !string.IsNullOrWhiteSpace(passwordFile))
            throw new InvalidOperationException($"Configure either {settingPrefix}:Password or {settingPrefix}:PasswordFile, not both.");
        if (password is not null)
            return password;
        if (string.IsNullOrWhiteSpace(passwordFile))
            throw new InvalidOperationException($"Configure either {settingPrefix}:Password or {settingPrefix}:PasswordFile.");

        var resolvedPath = Path.IsPathRooted(passwordFile)
            ? passwordFile
            : Path.Combine(contentRootPath, passwordFile);
        try
        {
            // Docker/Kubernetes secret files commonly end with a line break.
            // Remove only line terminators so intentional spaces remain part of the password.
            return File.ReadAllText(resolvedPath).TrimEnd('\r', '\n');
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            throw new InvalidOperationException($"Could not read the OpenIddict password file configured by {settingPrefix}:PasswordFile.", exception);
        }
    }
}
