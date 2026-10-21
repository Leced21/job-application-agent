using System.Net;
using System.Net.Http.Json;
using JobApplicationAgent.Applications.Application;
using JobApplicationAgent.Applications.Infrastructure;
using Microsoft.Extensions.Configuration;

namespace JobApplicationAgent.Applications.UnitTests;

public class LocalApplicationSourcesTests
{
    private readonly Guid owner = Guid.NewGuid();
    private readonly Guid profile = Guid.NewGuid();
    private readonly Guid job = Guid.NewGuid();
    private ApplicationSourceBinding Binding => new(new("http://localhost:5002"), new("http://localhost:5001"));

    [Theory]
    [InlineData("Discovered", true)]
    [InlineData("Saved", true)]
    [InlineData("Ignored", false)]
    [InlineData("Expired", false)]
    public async Task ParsesRealStringEnumJobContract_AndDoesNotForwardCredentials(string status, bool active)
    {
        using var client = new HttpClient(new Handler(r =>
        {
            Assert.Null(r.Headers.Authorization);
            Assert.False(r.Headers.Contains("Cookie"));
            return new(HttpStatusCode.OK) { Content = r.RequestUri!.Port == 5001
                ? JsonContent.Create(new { id = profile, firstName = "Synthetic" })
                : JsonContent.Create(new { id = job, title = "Engineer", companyName = "Company", status }) };
        }));
        var result = await new LocalApplicationSources(client, Binding).VerifyAsync(owner, profile, job, default);
        Assert.Equal(active, result.AcceptsApplications);
        Assert.Equal(job, result.JobId); Assert.Equal(profile, result.ProfileId); Assert.Equal(owner, result.OwnerId);
    }
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RejectsInvalidIdentityWithoutNetworkCall(bool wrongOwner)
    {
        using var client = new HttpClient(new Handler(_ => throw new Exception("Must not call source")));
        await Assert.ThrowsAsync<ApplicationSourceRejectedException>(() => new LocalApplicationSources(client, Binding)
            .VerifyAsync(wrongOwner ? Guid.Empty : owner, wrongOwner ? profile : Guid.Empty, job, default));
    }
    [Theory]
    [InlineData(302)]
    [InlineData(401)]
    [InlineData(500)]
    public async Task RejectsRedirectsAndUnexpectedHttpStatuses(int status)
    {
        using var client = new HttpClient(new Handler(_ => new((HttpStatusCode)status)));
        await Assert.ThrowsAsync<ApplicationSourceUnavailableException>(() => new LocalApplicationSources(client, Binding).VerifyAsync(owner, profile, job, default));
    }
    [Theory]
    [InlineData("invalid-json")]
    [InlineData("null")]
    [InlineData("oversized")]
    public async Task RejectsInvalidOrOversizedResponse(string value)
    {
        using var client = new HttpClient(new Handler(_ => new(HttpStatusCode.OK) { Content = new StringContent(value == "oversized" ? new string('a', 131073) : value) }));
        await Assert.ThrowsAsync<ApplicationSourceUnavailableException>(() => new LocalApplicationSources(client, Binding).VerifyAsync(owner, profile, job, default));
    }
    [Fact]
    public async Task MissingSourceIsRejected_AndCallerCancellationIsPreserved()
    {
        using var missing = new HttpClient(new Handler(_ => new(HttpStatusCode.NotFound)));
        await Assert.ThrowsAsync<ApplicationSourceRejectedException>(() => new LocalApplicationSources(missing, Binding).VerifyAsync(owner, profile, job, default));
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new LocalApplicationSources(missing, Binding).VerifyAsync(owner, profile, job, cancellation.Token));
    }
    [Theory]
    [InlineData("https://example.com")]
    [InlineData("http://localhost/path")]
    [InlineData("http://user:secret@localhost")]
    [InlineData("http://localhost?query=x")]
    [InlineData("http://localhost/#fragment")]
    [InlineData("file:///tmp")]
    public void ConfigurationRejectsOriginsOutsideLoopbackRoot(string origin)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["Applications:Sources:JobBaseUrl"] = origin }).Build();
        Assert.Throws<InvalidOperationException>(() => ApplicationSourceBinding.From(config));
    }
    [Fact]
    public void ConfigurationAcceptsOnlyMatchingComposeServiceOrigins()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["Applications:Sources:JobBaseUrl"] = "http://job-api:8080",
            ["Applications:Sources:ProfileBaseUrl"] = "http://profile-api:8080"
        }).Build();

        var binding = ApplicationSourceBinding.From(config);

        Assert.Equal("job-api", binding.JobOrigin.Host);
        Assert.Equal("profile-api", binding.ProfileOrigin.Host);
    }
    [Theory]
    [InlineData("http://another-api:8080")]
    [InlineData("http://job-api:8081")]
    [InlineData("https://job-api:8080")]
    public void ConfigurationRejectsUnapprovedComposeOrigins(string origin)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["Applications:Sources:JobBaseUrl"] = origin
        }).Build();

        Assert.Throws<InvalidOperationException>(() => ApplicationSourceBinding.From(config));
    }
    [Fact]
    public void RestorationReplaysHistory_AndRejectsForgedStatusOrSequence()
    {
        var c = Domain.Candidacy.Create(owner, profile, job, "Engineer", "Company", DateTime.UtcNow);
        c.AddNote(owner, c.Revision, "Note", DateTime.UtcNow);
        var restored = Domain.Candidacy.Restore(c.Id, owner, profile, job, c.JobTitle, c.CompanyName, c.Events);
        Assert.Equal(c.Events, restored.Events); Assert.Equal(c.Id, restored.Id);
        var history = c.Events.ToArray(); history[1] = history[1] with { Status = Domain.CandidacyStatus.Approved };
        Assert.Throws<ArgumentException>(() => Domain.Candidacy.Restore(c.Id, owner, profile, job, c.JobTitle, c.CompanyName, history));
        history[1] = c.Events[1] with { Sequence = 99 };
        Assert.Throws<ArgumentException>(() => Domain.Candidacy.Restore(c.Id, owner, profile, job, c.JobTitle, c.CompanyName, history));
    }
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        { cancellationToken.ThrowIfCancellationRequested(); return Task.FromResult(respond(request)); }
    }
}
