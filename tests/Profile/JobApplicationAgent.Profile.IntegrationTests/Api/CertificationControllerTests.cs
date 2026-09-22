using System.Net;
using System.Net.Http.Json;
using JobApplicationAgent.Profile.Application.Profiles.Certifications;
using JobApplicationAgent.Profile.Application.Profiles.Certifications.Add;
using JobApplicationAgent.Profile.IntegrationTests.Fixtures;

namespace JobApplicationAgent.Profile.IntegrationTests.Api;

public sealed class CertificationControllerTests : IClassFixture<ProfileApiFactory>, IAsyncLifetime
{
    private readonly ProfileApiFactory _factory;
    private readonly HttpClient _client;
    private const string Route = "/api/v1/profile/certifications";

    public CertificationControllerTests(ProfileApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // PostgreSQL timestamps retain microseconds, whereas DateTime also stores 100ns ticks.
    private static CertificationDto Normalize(CertificationDto dto) => dto with
    {
        CreatedAtUtc = new DateTime(dto.CreatedAtUtc.Ticks / 10 * 10, DateTimeKind.Utc),
        UpdatedAtUtc = new DateTime(dto.UpdatedAtUtc.Ticks / 10 * 10, DateTimeKind.Utc)
    };

    private static AddCertificationCommand ValidCommand() =>
        new("Azure Developer", "Microsoft", new DateOnly(2025, 1, 1),
            new DateOnly(2027, 1, 1), "AZ-204", "https://example.com/certificate");

    private async Task CreateProfileAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/profile",
            new { firstName = "Test", lastName = "Candidate", email = "test@example.com" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Certification_ShouldPersistThroughCreateUpdateAndDelete()
    {
        await CreateProfileAsync();
        var empty = await _client.GetFromJsonAsync<List<CertificationDto>>(Route);
        Assert.Empty(empty!);
        var response = await _client.PostAsJsonAsync(Route, ValidCommand());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<CertificationDto>();
        Assert.NotNull(created);
        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal("AZ-204", created.CredentialId);
        var persisted = Assert.Single((await _client.GetFromJsonAsync<List<CertificationDto>>(Route))!);
        Assert.Equal(Normalize(created), Normalize(persisted));

        var updatedCommand = ValidCommand() with
        {
            Name = "Updated certificate", IssuingOrganization = "New issuer",
            IssueDate = new DateOnly(2026, 1, 1), ExpirationDate = null,
            CredentialId = null, CredentialUrl = null
        };
        var update = await _client.PutAsJsonAsync($"{Route}/{created.Id}", updatedCommand);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = await update.Content.ReadFromJsonAsync<CertificationDto>();
        persisted = Assert.Single((await _client.GetFromJsonAsync<List<CertificationDto>>(Route))!);
        Assert.Equal(Normalize(updated!), Normalize(persisted));
        Assert.Equal(created.Id, persisted.Id);
        Assert.Equal(Normalize(created).CreatedAtUtc, persisted.CreatedAtUtc);
        Assert.Equal(updatedCommand.Name, persisted.Name);
        Assert.Equal(updatedCommand.IssuingOrganization, persisted.IssuingOrganization);
        Assert.Equal(updatedCommand.IssueDate, persisted.IssueDate);
        Assert.Null(persisted.ExpirationDate);
        Assert.Null(persisted.CredentialId);
        Assert.Null(persisted.CredentialUrl);

        var delete = await _client.DeleteAsync($"{Route}/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Empty((await _client.GetFromJsonAsync<List<CertificationDto>>(Route))!);
    }

    [Fact]
    public async Task Certification_ShouldReturnNotFound_WhenProfileIsMissing()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await _client.PostAsJsonAsync(Route, ValidCommand())).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync(Route)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.PutAsJsonAsync($"{Route}/{Guid.NewGuid()}", ValidCommand())).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.DeleteAsync($"{Route}/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task Certification_ShouldReturnNotFound_WhenCertificationIsMissing()
    {
        await CreateProfileAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await _client.PutAsJsonAsync($"{Route}/{Guid.NewGuid()}", ValidCommand())).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.DeleteAsync($"{Route}/{Guid.NewGuid()}")).StatusCode);
    }

    [Theory]
    [InlineData("name")]
    [InlineData("issuer")]
    [InlineData("date")]
    [InlineData("expiration")]
    [InlineData("url")]
    [InlineData("id")]
    public async Task Certification_ShouldRejectInvalidData_WithoutChangingPersistence(string field)
    {
        await CreateProfileAsync();
        var response = await _client.PostAsJsonAsync(Route, ValidCommand());
        var created = await response.Content.ReadFromJsonAsync<CertificationDto>();
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var invalid = field switch
        {
            "name" => ValidCommand() with { Name = "" },
            "issuer" => ValidCommand() with { IssuingOrganization = "" },
            "date" => ValidCommand() with { IssueDate = default },
            "expiration" => ValidCommand() with { ExpirationDate = new DateOnly(2024, 1, 1) },
            "url" => ValidCommand() with { CredentialUrl = "javascript:alert(1)" },
            _ => ValidCommand() with { CredentialId = new string('a', 201) }
        };
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.PostAsJsonAsync(Route, invalid)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.PutAsJsonAsync($"{Route}/{created!.Id}", invalid)).StatusCode);
        Assert.Equal(Normalize(created!), Normalize(Assert.Single((await _client.GetFromJsonAsync<List<CertificationDto>>(Route))!)));
    }
}
