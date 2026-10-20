using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Configuration;
using JobApplicationAgent.Gateway.OpenIddict;

namespace JobApplicationAgent.Gateway.UnitTests;

public sealed class GatewayOpenIddictCertificateLoaderTests
{
    [Fact]
    public void Load_WithSeparateValidRsaCertificates_ReturnsSigningAndEncryptionCertificates()
    {
        using var directory = new TemporaryDirectory();
        const string password = "unit-test-password";
        var signingPath = CreateCertificate(directory.Root, "signing", password, X509KeyUsageFlags.DigitalSignature);
        var encryptionPath = CreateCertificate(directory.Root, "encryption", password, X509KeyUsageFlags.KeyEncipherment);

        using var certificates = GatewayOpenIddictCertificateLoader.Load(
            CreateConfiguration(signingPath, encryptionPath, password), directory.Root);

        Assert.NotEqual(certificates.Signing.Thumbprint, certificates.Encryption.Thumbprint);
        Assert.True(certificates.Signing.HasPrivateKey);
        Assert.True(certificates.Encryption.HasPrivateKey);
    }

    [Fact]
    public void Load_WithPasswordFiles_ReadsSecretsAndIgnoresTrailingLineBreaks()
    {
        using var directory = new TemporaryDirectory();
        const string password = "unit-test-password with spaces";
        var signingPath = CreateCertificate(directory.Root, "signing", password, X509KeyUsageFlags.DigitalSignature);
        var encryptionPath = CreateCertificate(directory.Root, "encryption", password, X509KeyUsageFlags.KeyEncipherment);
        var signingPasswordFile = Path.Combine(directory.Root, "signing-password");
        var encryptionPasswordFile = Path.Combine(directory.Root, "encryption-password");
        File.WriteAllText(signingPasswordFile, password + "\r\n");
        File.WriteAllText(encryptionPasswordFile, password + "\n");

        using var certificates = GatewayOpenIddictCertificateLoader.Load(
            CreateConfigurationWithPasswordFiles(signingPath, encryptionPath, signingPasswordFile, encryptionPasswordFile), directory.Root);

        Assert.True(certificates.Signing.HasPrivateKey);
        Assert.True(certificates.Encryption.HasPrivateKey);
    }

    [Fact]
    public void Load_WhenInlinePasswordAndPasswordFileAreBothConfigured_RejectsAmbiguousSettings()
    {
        using var directory = new TemporaryDirectory();
        const string password = "unit-test-password";
        var signingPath = CreateCertificate(directory.Root, "signing", password, X509KeyUsageFlags.DigitalSignature);
        var encryptionPath = CreateCertificate(directory.Root, "encryption", password, X509KeyUsageFlags.KeyEncipherment);
        var signingPasswordFile = Path.Combine(directory.Root, "signing-password");
        File.WriteAllText(signingPasswordFile, password);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["OpenIddict:Certificates:Signing:Path"] = signingPath,
            ["OpenIddict:Certificates:Signing:Password"] = password,
            ["OpenIddict:Certificates:Signing:PasswordFile"] = signingPasswordFile,
            ["OpenIddict:Certificates:Encryption:Path"] = encryptionPath,
            ["OpenIddict:Certificates:Encryption:Password"] = password
        }).Build();

        var exception = Assert.Throws<InvalidOperationException>(
            () => GatewayOpenIddictCertificateLoader.Load(configuration, directory.Root));

        Assert.Contains("Configure either OpenIddict:Certificates:Signing:Password or OpenIddict:Certificates:Signing:PasswordFile", exception.Message);
    }

    [Fact]
    public void Load_WithPreviousCertificates_ReturnsOverlappingSigningAndEncryptionKeys()
    {
        using var directory = new TemporaryDirectory();
        const string password = "unit-test-password";
        var signingPath = CreateCertificate(directory.Root, "signing", password, X509KeyUsageFlags.DigitalSignature);
        var previousSigningPath = CreateCertificate(directory.Root, "signing-previous", password, X509KeyUsageFlags.DigitalSignature);
        var encryptionPath = CreateCertificate(directory.Root, "encryption", password, X509KeyUsageFlags.KeyEncipherment);
        var previousEncryptionPath = CreateCertificate(directory.Root, "encryption-previous", password, X509KeyUsageFlags.KeyEncipherment);

        using var certificates = GatewayOpenIddictCertificateLoader.Load(
            CreateConfiguration(signingPath, encryptionPath, password, previousSigningPath, previousEncryptionPath), directory.Root);

        Assert.Equal(2, certificates.SigningCertificates.Count);
        Assert.Equal(2, certificates.EncryptionCertificates.Count);
        Assert.NotEqual(certificates.SigningCertificates[0].Thumbprint, certificates.SigningCertificates[1].Thumbprint);
        Assert.NotEqual(certificates.EncryptionCertificates[0].Thumbprint, certificates.EncryptionCertificates[1].Thumbprint);
    }

    [Fact]
    public void Load_WithPasswordFileOnPreviousCertificate_AcceptsSecretFileConfiguration()
    {
        using var directory = new TemporaryDirectory();
        const string password = "unit-test-password";
        var signingPath = CreateCertificate(directory.Root, "signing", password, X509KeyUsageFlags.DigitalSignature);
        var previousSigningPath = CreateCertificate(directory.Root, "signing-previous", password, X509KeyUsageFlags.DigitalSignature);
        var encryptionPath = CreateCertificate(directory.Root, "encryption", password, X509KeyUsageFlags.KeyEncipherment);
        var previousPasswordFile = Path.Combine(directory.Root, "previous-signing-password");
        File.WriteAllText(previousPasswordFile, password);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["OpenIddict:Certificates:Signing:Path"] = signingPath,
            ["OpenIddict:Certificates:Signing:Password"] = password,
            ["OpenIddict:Certificates:Signing:Previous:0:Path"] = previousSigningPath,
            ["OpenIddict:Certificates:Signing:Previous:0:PasswordFile"] = previousPasswordFile,
            ["OpenIddict:Certificates:Encryption:Path"] = encryptionPath,
            ["OpenIddict:Certificates:Encryption:Password"] = password
        }).Build();

        using var certificates = GatewayOpenIddictCertificateLoader.Load(configuration, directory.Root);

        Assert.Equal(2, certificates.SigningCertificates.Count);
        Assert.True(certificates.SigningCertificates[1].HasPrivateKey);
    }

    [Fact]
    public void Load_WhenCertificateSettingsAreMissing_ReportsRequiredConfiguration()
    {
        var configuration = new ConfigurationBuilder().Build();

        var exception = Assert.Throws<InvalidOperationException>(
            () => GatewayOpenIddictCertificateLoader.Load(configuration, Path.GetTempPath()));

        Assert.Contains("OpenIddict:Certificates:Signing:Path", exception.Message);
    }

    [Fact]
    public void Load_WhenSameCertificateIsUsedForBothPurposes_RejectsConfiguration()
    {
        using var directory = new TemporaryDirectory();
        const string password = "unit-test-password";
        var certificatePath = CreateCertificate(
            directory.Root, "shared", password, X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment);

        var exception = Assert.Throws<InvalidOperationException>(
            () => GatewayOpenIddictCertificateLoader.Load(
                CreateConfiguration(certificatePath, certificatePath, password), directory.Root));

        Assert.Contains("must be different certificates", exception.Message);
    }

    [Fact]
    public void Load_WhenPreviousCertificateIsRepeatedAcrossPurposes_RejectsConfiguration()
    {
        using var directory = new TemporaryDirectory();
        const string password = "unit-test-password";
        var signingPath = CreateCertificate(directory.Root, "signing", password, X509KeyUsageFlags.DigitalSignature);
        var encryptionPath = CreateCertificate(directory.Root, "encryption", password, X509KeyUsageFlags.KeyEncipherment);
        var repeatedPreviousPath = CreateCertificate(
            directory.Root,
            "shared-previous",
            password,
            X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment);

        var exception = Assert.Throws<InvalidOperationException>(
            () => GatewayOpenIddictCertificateLoader.Load(
                CreateConfiguration(signingPath, encryptionPath, password, repeatedPreviousPath, repeatedPreviousPath), directory.Root));

        Assert.Contains("must be different certificates", exception.Message);
    }

    [Fact]
    public void Load_WhenEncryptionCertificateLacksKeyEncipherment_RejectsCertificate()
    {
        using var directory = new TemporaryDirectory();
        const string password = "unit-test-password";
        var signingPath = CreateCertificate(directory.Root, "signing", password, X509KeyUsageFlags.DigitalSignature);
        var encryptionPath = CreateCertificate(directory.Root, "wrong-usage", password, X509KeyUsageFlags.DigitalSignature);

        var exception = Assert.Throws<InvalidOperationException>(
            () => GatewayOpenIddictCertificateLoader.Load(
                CreateConfiguration(signingPath, encryptionPath, password), directory.Root));

        Assert.Contains("does not allow its required key usage", exception.Message);
    }

    private static IConfiguration CreateConfiguration(
        string signingPath,
        string encryptionPath,
        string password,
        string? previousSigningPath = null,
        string? previousEncryptionPath = null)
    {
        var settings = new Dictionary<string, string?>
        {
            ["OpenIddict:Certificates:Signing:Path"] = signingPath,
            ["OpenIddict:Certificates:Signing:Password"] = password,
            ["OpenIddict:Certificates:Encryption:Path"] = encryptionPath,
            ["OpenIddict:Certificates:Encryption:Password"] = password
        };
        if (previousSigningPath is not null)
        {
            settings["OpenIddict:Certificates:Signing:Previous:0:Path"] = previousSigningPath;
            settings["OpenIddict:Certificates:Signing:Previous:0:Password"] = password;
        }
        if (previousEncryptionPath is not null)
        {
            settings["OpenIddict:Certificates:Encryption:Previous:0:Path"] = previousEncryptionPath;
            settings["OpenIddict:Certificates:Encryption:Previous:0:Password"] = password;
        }

        return new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
    }

    private static IConfiguration CreateConfigurationWithPasswordFiles(
        string signingPath,
        string encryptionPath,
        string signingPasswordFile,
        string encryptionPasswordFile) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["OpenIddict:Certificates:Signing:Path"] = signingPath,
            ["OpenIddict:Certificates:Signing:PasswordFile"] = signingPasswordFile,
            ["OpenIddict:Certificates:Encryption:Path"] = encryptionPath,
            ["OpenIddict:Certificates:Encryption:PasswordFile"] = encryptionPasswordFile
        }).Build();

    private static string CreateCertificate(string directory, string name, string password, X509KeyUsageFlags keyUsages)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            $"CN={name}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509KeyUsageExtension(keyUsages, critical: true));
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddYears(2));
        var path = Path.Combine(directory, $"{name}.pfx");
        File.WriteAllBytes(path, certificate.Export(X509ContentType.Pfx, password));
        return path;
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), $"gateway-certificate-tests-{Guid.NewGuid():N}");

        public TemporaryDirectory() => Directory.CreateDirectory(Root);

        public void Dispose() => Directory.Delete(Root, recursive: true);
    }
}
