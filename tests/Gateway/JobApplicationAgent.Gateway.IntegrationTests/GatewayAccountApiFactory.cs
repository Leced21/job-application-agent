extern alias GatewaySource;
using GatewayProgram = GatewaySource::Program;
using GatewayIdentityDbContext = GatewaySource::JobApplicationAgent.Gateway.Identity.GatewayIdentityDbContext;
using GatewayUser = GatewaySource::JobApplicationAgent.Gateway.Identity.ApplicationUser;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace JobApplicationAgent.Gateway.IntegrationTests;

public sealed class GatewayAccountApiFactory : WebApplicationFactory<GatewayProgram>, IAsyncLifetime
{
    public static readonly Guid InitialOwnerId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:17")
        .WithDatabase("gateway_identity_tests")
        .WithUsername("gateway_" + Guid.NewGuid().ToString("N")[..8])
        .WithPassword(Guid.NewGuid().ToString("N"))
        .Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:IdentityDatabase", postgres.GetConnectionString());
        builder.UseSetting("OpenIddict:Issuer", "https://localhost/");
        builder.UseSetting("OpenIddict:Bff:ClientId", "gateway-account-tests");
        builder.UseSetting("OpenIddict:Bff:ClientSecret", Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"));
        builder.UseSetting("Identity:AllowRegistration", "true");
        builder.UseSetting("Identity:InitialOwnerId", InitialOwnerId.ToString("D"));
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

    public async Task<IReadOnlyList<GatewayUser>> ReadUsersAsync()
    {
        using var scope = Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<GatewayIdentityDbContext>();
        return await database.Users.AsNoTracking().OrderBy(user => user.Email).ToListAsync();
    }

    public HttpClient NewClient() => CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost")
    });

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await postgres.DisposeAsync();
    }
}
