extern alias GatewaySource;
using GatewayProgram = GatewaySource::Program;
using GatewayIdentityDbContext = GatewaySource::JobApplicationAgent.Gateway.Identity.GatewayIdentityDbContext;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace JobApplicationAgent.Gateway.IntegrationTests;

public sealed class ProductionOpenIddictCertificateTests(
    ProductionGatewayApiFactory factory) : IClassFixture<ProductionGatewayApiFactory>
{
    [Fact]
    public async Task ProductionGateway_UsesConfiguredCertificatesForDiscovery()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

        using var metadataResponse = await client.GetAsync("/.well-known/openid-configuration");
        Assert.Equal(HttpStatusCode.OK, metadataResponse.StatusCode);
        using var metadata = System.Text.Json.JsonDocument.Parse(await metadataResponse.Content.ReadAsStringAsync());
        Assert.Equal("https://localhost/", metadata.RootElement.GetProperty("issuer").GetString());

        var jwksUri = metadata.RootElement.GetProperty("jwks_uri").GetString();
        Assert.False(string.IsNullOrWhiteSpace(jwksUri));
        using var keysResponse = await client.GetAsync(jwksUri);
        Assert.Equal(HttpStatusCode.OK, keysResponse.StatusCode);
        using var keys = System.Text.Json.JsonDocument.Parse(await keysResponse.Content.ReadAsStringAsync());
        Assert.True(keys.RootElement.GetProperty("keys").GetArrayLength() >= 2,
            "The production JWKS should publish both the current and previous signing certificates during rotation.");
    }
}

public sealed class ProductionGatewayFailClosedTests
{
    [Fact]
    public void Gateway_WithoutIdentityDatabase_FailsClosedOutsideDevelopment()
    {
        using var factoryWithoutIdentity = new WebApplicationFactory<GatewayProgram>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Production");
                builder.UseSetting("ConnectionStrings:IdentityDatabase", string.Empty);
            });

        var exception = Assert.ThrowsAny<Exception>(() => factoryWithoutIdentity.CreateClient());

        Assert.Contains("ConnectionStrings:IdentityDatabase", exception.ToString());
    }
}

public sealed class ProductionGatewayApiFactory : WebApplicationFactory<GatewayProgram>, IAsyncLifetime
{
    private const string CertificatePassword = "production-certificate-tests";
    private readonly string certificateDirectory = Path.Combine(Path.GetTempPath(), $"gateway-production-certificates-{Guid.NewGuid():N}");
    private readonly string signingCertificatePath;
    private readonly string previousSigningCertificatePath;
    private readonly string encryptionCertificatePath;
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:17")
        .WithDatabase("gateway_production_tests")
        .WithUsername("gateway_" + Guid.NewGuid().ToString("N")[..8])
        .WithPassword(Guid.NewGuid().ToString("N"))
        .Build();

    public ProductionGatewayApiFactory()
    {
        Directory.CreateDirectory(certificateDirectory);
        signingCertificatePath = CreateCertificate("signing", X509KeyUsageFlags.DigitalSignature);
        previousSigningCertificatePath = CreateCertificate("signing-previous", X509KeyUsageFlags.DigitalSignature);
        encryptionCertificatePath = CreateCertificate("encryption", X509KeyUsageFlags.KeyEncipherment);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.UseSetting("ConnectionStrings:IdentityDatabase", postgres.GetConnectionString());
        builder.UseSetting("OpenIddict:Issuer", "https://localhost/");
        builder.UseSetting("OpenIddict:Bff:ClientId", "gateway-production-tests");
        builder.UseSetting("OpenIddict:Bff:ClientSecret", Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"));
        builder.UseSetting("OpenIddict:Certificates:Signing:Path", signingCertificatePath);
        builder.UseSetting("OpenIddict:Certificates:Signing:Password", CertificatePassword);
        builder.UseSetting("OpenIddict:Certificates:Signing:Previous:0:Path", previousSigningCertificatePath);
        builder.UseSetting("OpenIddict:Certificates:Signing:Previous:0:Password", CertificatePassword);
        builder.UseSetting("OpenIddict:Certificates:Encryption:Path", encryptionCertificatePath);
        builder.UseSetting("OpenIddict:Certificates:Encryption:Password", CertificatePassword);
        builder.UseSetting("Identity:InitialOwnerId", "11111111-1111-4111-8111-111111111111");
    }

    public async Task InitializeAsync()
    {
        await postgres.StartAsync();
        var options = new DbContextOptionsBuilder<GatewayIdentityDbContext>()
            .UseNpgsql(postgres.GetConnectionString())
            .Options;
        await using var database = new GatewayIdentityDbContext(options);
        await database.Database.MigrateAsync();
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await postgres.DisposeAsync();
        Directory.Delete(certificateDirectory, recursive: true);
    }

    private string CreateCertificate(string name, X509KeyUsageFlags keyUsages)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            $"CN={name}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509KeyUsageExtension(keyUsages, critical: true));
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddYears(2));
        var path = Path.Combine(certificateDirectory, $"{name}.pfx");
        File.WriteAllBytes(path, certificate.Export(X509ContentType.Pfx, CertificatePassword));
        return path;
    }
}
