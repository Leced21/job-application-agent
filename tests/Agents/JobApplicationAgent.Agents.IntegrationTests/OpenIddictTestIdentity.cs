extern alias GatewaySource;
using GatewayIdentityDbContext = GatewaySource::JobApplicationAgent.Gateway.Identity.GatewayIdentityDbContext;
using GatewayUser = GatewaySource::JobApplicationAgent.Gateway.Identity.ApplicationUser;
using GatewayProgram = GatewaySource::Program;
using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;

namespace JobApplicationAgent.Agents.IntegrationTests;

internal sealed class OpenIddictTestIdentity : IAsyncDisposable
{
    private const string ClientId = "agents-integration-tests";
    private static readonly string ClientSecret = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
    private readonly ConcurrentDictionary<Guid, (string Token, DateTimeOffset ExpiresAt)> tokens = new();
    private readonly string connectionString;
    private WebApplicationFactory<GatewayProgram> gateway = null!;
    private HttpClient client = null!;

    private OpenIddictTestIdentity(string connectionString) => this.connectionString = connectionString;

    public Uri Issuer { get; private set; } = null!;
    public OpenIddictConfiguration Configuration { get; private set; } = null!;

    public static async Task<OpenIddictTestIdentity> StartAsync(string connectionString)
    {
        var identity = new OpenIddictTestIdentity(connectionString);
        await identity.StartAsync();
        return identity;
    }

    public string TokenFor(Guid ownerId) => TokenForAsync(ownerId).GetAwaiter().GetResult();

    private async Task StartAsync()
    {
        var options = new DbContextOptionsBuilder<GatewayIdentityDbContext>().UseNpgsql(connectionString).Options;
        await using (var database = new GatewayIdentityDbContext(options))
            await database.Database.MigrateAsync();

        gateway = new WebApplicationFactory<GatewayProgram>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:IdentityDatabase", connectionString);
            builder.UseSetting("OpenIddict:Issuer", "https://localhost/");
            builder.UseSetting("OpenIddict:Bff:ClientId", ClientId);
            builder.UseSetting("OpenIddict:Bff:ClientSecret", ClientSecret);
        });
        client = gateway.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

        using var metadataResponse = await client.GetAsync("/.well-known/openid-configuration");
        if (!metadataResponse.IsSuccessStatusCode)
            throw new InvalidOperationException($"OpenID discovery failed ({(int)metadataResponse.StatusCode}): {await metadataResponse.Content.ReadAsStringAsync()}");
        using var metadata = JsonDocument.Parse(await metadataResponse.Content.ReadAsStringAsync());
        Issuer = new Uri(metadata.RootElement.GetProperty("issuer").GetString()!, UriKind.Absolute);
        var keysUri = new Uri(metadata.RootElement.GetProperty("jwks_uri").GetString()!, UriKind.Absolute);
        using var keysResponse = await client.GetAsync(keysUri);
        keysResponse.EnsureSuccessStatusCode();
        var keySet = new JsonWebKeySet(await keysResponse.Content.ReadAsStringAsync());
        Configuration = new OpenIddictConfiguration { Issuer = Issuer, JsonWebKeySet = keySet };
        foreach (var key in keySet.GetSigningKeys())
            Configuration.SigningKeys.Add(key);
    }

    private async Task<string> TokenForAsync(Guid ownerId)
    {
        if (ownerId == Guid.Empty) throw new ArgumentException("A test owner is required.", nameof(ownerId));
        if (tokens.TryGetValue(ownerId, out var cached) && cached.ExpiresAt > DateTimeOffset.UtcNow.AddSeconds(15))
            return cached.Token;

        await EnsureOwnerAsync(ownerId);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/connect/token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["scope"] = "jobagent_api",
                ["jobagent_owner_id"] = ownerId.ToString("D")
            })
        };
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{ClientId}:{ClientSecret}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var token = body.RootElement.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("The OpenIddict test Gateway returned no access token.");
        var lifetime = body.RootElement.GetProperty("expires_in").GetInt32();
        tokens[ownerId] = (token, DateTimeOffset.UtcNow.AddSeconds(lifetime));
        return token;
    }

    private async Task EnsureOwnerAsync(Guid ownerId)
    {
        using var scope = gateway.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<GatewayUser>>();
        var existing = await users.Users.SingleOrDefaultAsync(user => user.OwnerId == ownerId);
        if (existing is not null) return;

        var user = new GatewayUser
        {
            UserName = $"agents-{ownerId:N}@example.test",
            Email = $"agents-{ownerId:N}@example.test",
            EmailConfirmed = true,
            OwnerId = ownerId
        };
        var result = await users.CreateAsync(user);
        if (!result.Succeeded)
            throw new InvalidOperationException("Could not provision the OpenIddict integration-test owner: " +
                string.Join("; ", result.Errors.Select(error => error.Code)));
    }

    public async ValueTask DisposeAsync()
    {
        client?.Dispose();
        if (gateway is not null) await gateway.DisposeAsync();
    }
}
