using System.Net;
using System.Net.Http.Json;

namespace JobApplicationAgent.Gateway.IntegrationTests;

public sealed class AccountRegistrationApiTests(GatewayAccountApiFactory factory) : IClassFixture<GatewayAccountApiFactory>
{
    [Fact]
    public async Task Registration_ShouldCreateFirstAndSubsequentAccountsWithSeparateOwners()
    {
        using var firstClient = factory.NewClient();

        using var initialSessionResponse = await firstClient.GetAsync("/auth/session");
        Assert.True(initialSessionResponse.IsSuccessStatusCode,
            $"Session endpoint returned {(int)initialSessionResponse.StatusCode}: {await initialSessionResponse.Content.ReadAsStringAsync()}");
        var initialSession = await initialSessionResponse.Content.ReadFromJsonAsync<SessionResponse>();
        Assert.NotNull(initialSession);
        Assert.False(initialSession.IsAuthenticated);
        Assert.True(initialSession.RegistrationEnabled);

        var firstRegistration = await RegisterAsync(firstClient, "first@example.test");
        Assert.Equal(HttpStatusCode.OK, firstRegistration.StatusCode);
        var firstSession = await firstRegistration.Content.ReadFromJsonAsync<SessionResponse>();
        Assert.NotNull(firstSession);
        Assert.True(firstSession.IsAuthenticated);
        Assert.True(firstSession.RegistrationEnabled);
        var firstAuthenticatedSession = await firstClient.GetFromJsonAsync<SessionResponse>("/auth/session");
        Assert.NotNull(firstAuthenticatedSession);
        Assert.True(firstAuthenticatedSession.IsAuthenticated);
        Assert.Equal("first@example.test", firstAuthenticatedSession.Email);

        using var secondClient = factory.NewClient();
        var secondRegistration = await RegisterAsync(secondClient, "second@example.test");
        Assert.Equal(HttpStatusCode.OK, secondRegistration.StatusCode);
        var secondSession = await secondRegistration.Content.ReadFromJsonAsync<SessionResponse>();
        Assert.NotNull(secondSession);
        Assert.True(secondSession.IsAuthenticated);
        Assert.True(secondSession.RegistrationEnabled);
        var secondAuthenticatedSession = await secondClient.GetFromJsonAsync<SessionResponse>("/auth/session");
        Assert.NotNull(secondAuthenticatedSession);
        Assert.True(secondAuthenticatedSession.IsAuthenticated);
        Assert.Equal("second@example.test", secondAuthenticatedSession.Email);

        var users = await factory.ReadUsersAsync();
        var firstUser = Assert.Single(users, user => user.Email == "first@example.test");
        var secondUser = Assert.Single(users, user => user.Email == "second@example.test");

        Assert.Equal(GatewayAccountApiFactory.InitialOwnerId, firstUser.OwnerId);
        Assert.NotNull(secondUser.OwnerId);
        Assert.NotEqual(firstUser.OwnerId, secondUser.OwnerId);
    }

    private static async Task<HttpResponseMessage> RegisterAsync(HttpClient client, string email)
    {
        var antiforgery = await client.GetFromJsonAsync<AntiforgeryResponse>("/auth/antiforgery");
        Assert.NotNull(antiforgery);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/auth/register")
        {
            Content = JsonContent.Create(new { email, password = "Strong!Password123" })
        };
        request.Headers.Add("X-CSRF-TOKEN", antiforgery.RequestToken);
        return await client.SendAsync(request);
    }

    private sealed record AntiforgeryResponse(string RequestToken);
    private sealed record SessionResponse(bool IsAuthenticated, string? Email, bool RegistrationEnabled);
}
