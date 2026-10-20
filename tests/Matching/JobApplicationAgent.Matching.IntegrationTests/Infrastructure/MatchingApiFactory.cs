using JobApplicationAgent.Matching.Application.Abstractions;
using JobApplicationAgent.Matching.Application.Models;
using JobApplicationAgent.Matching.Domain.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using JobApplicationAgent.Matching.Infrastructure.HttpClients;
using JobApplicationAgent.Matching.Application.Matching;
using JobApplicationAgent.Matching.Application.Scoring;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JobApplicationAgent.Matching.IntegrationTests.Infrastructure;

public sealed class MatchingApiFactory : WebApplicationFactory<Program>
{
    public JobMatchingData? Job { get; set; }

    public ProfileMatchingData? Profile { get; set; }
    public Exception? JobClientException { get; set; }

    public Exception? ProfileClientException { get; set; }

    public HttpMessageHandler? JobHttpHandler { get; set; }

    public RecommendationProcessingLimits ProcessingLimits { get; set; } = new();
    public ScoringRules ScoringRules { get; set; } = new();
    public bool UseRealScoring { get; set; }

    public IReadOnlyList<JobMatchingData>? Jobs { get; set; }

    public Dictionary<Guid, IReadOnlyCollection<MatchingCriterion>> CriteriaByJob { get; } = [];

    public int JobPageCallCount { get; private set; }

    public int? RequestedPage { get; private set; }

    public int? RequestedPageSize { get; private set; }


    public IMatchingResultRepository MatchingRepository { get; } =
        Substitute.For<IMatchingResultRepository>();

    public IReadOnlyCollection<MatchingCriterion> Criteria { get; set; } =
    [
        new MatchingCriterion(
            "JobTitle",
            80m,
            1m,
            "Job title is compatible.")
    ];


    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((context, configuration) =>
        {
            configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Services:Job:BaseUrl"] =
                        "http://localhost:5002/",

                    ["Services:Profile:BaseUrl"] =
                        "http://localhost:5001/"
                });
        });

        builder.ConfigureTestServices(services =>
        {
            services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = "MatchingIntegrationTest";
                    options.DefaultChallengeScheme = "MatchingIntegrationTest";
                })
                .AddScheme<AuthenticationSchemeOptions, MatchingIntegrationTestAuthenticationHandler>(
                    "MatchingIntegrationTest", _ => { });
            services.RemoveAll<RecommendationProcessingLimits>();
            services.AddSingleton(ProcessingLimits);
            services.RemoveAll<ScoringRules>();
            services.AddSingleton(ScoringRules);
            services.RemoveAll<IJobMatchingDataClient>();
            services.RemoveAll<IProfileMatchingDataClient>();
            services.RemoveAll<IMatchingScoringEngine>();
            services.RemoveAll<IMatchingResultRepository>();

            if (JobHttpHandler is not null)
            {
                services.AddHttpClient<IJobMatchingDataClient, JobMatchingHttpClient>()
                    .ConfigurePrimaryHttpMessageHandler(() => JobHttpHandler);
            }
            else
            {
                services.AddScoped<IJobMatchingDataClient>(_ => new StubJobClient(this));
            }

            services.AddScoped<IProfileMatchingDataClient>(
                _ => new StubProfileClient(this));

            if (UseRealScoring)
                services.AddScoped<IMatchingScoringEngine, StructuredMatchingScoringEngine>();
            else
                services.AddScoped<IMatchingScoringEngine>(_ => new StubScoringEngine(this));

            services.AddScoped<IMatchingResultRepository>(
                _ => MatchingRepository);
        });
    }

    private sealed class MatchingIntegrationTestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var identity = new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, "matching-integration-owner")], Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(principal, Scheme.Name)));
        }
    }


    private sealed class StubJobClient(MatchingApiFactory factory) : IJobMatchingDataClient
    {

        public Task<JobMatchingData?> GetByIdAsync(
            Guid jobId,
            CancellationToken cancellationToken = default)
        {
            if (factory.JobClientException is not null)
            {
                return Task.FromException<JobMatchingData?>(
                    factory.JobClientException);
            }

            return Task.FromResult(
                factory.Job?.Id == jobId ? factory.Job : null);
        }

        public Task<IReadOnlyList<JobMatchingData>> GetPageAsync(
            int page, int pageSize, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Recommendations must use eligible jobs.");

        public Task<IReadOnlyList<JobMatchingData>> GetRecommendablePageAsync(
            int page,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            factory.JobPageCallCount++;
            factory.RequestedPage = page;
            factory.RequestedPageSize = pageSize;
            if (factory.JobClientException is not null)
            {
                return Task.FromException<IReadOnlyList<JobMatchingData>>(
                    factory.JobClientException);
            }

            IReadOnlyList<JobMatchingData> jobs =
                factory.Jobs ??
                (factory.Job is null
                    ? Array.Empty<JobMatchingData>()
                    : new[] { factory.Job });

            return Task.FromResult<IReadOnlyList<JobMatchingData>>(
                jobs.Skip((page - 1) * pageSize).Take(pageSize).ToArray());
        }
    }

    private sealed class StubProfileClient(MatchingApiFactory factory) : IProfileMatchingDataClient
    {

        public Task<ProfileMatchingData?> GetAsync(
            CancellationToken cancellationToken = default)
        {
            if (factory.ProfileClientException is not null)
            {
                return Task.FromException<ProfileMatchingData?>(
                    factory.ProfileClientException);
            }

            return Task.FromResult(factory.Profile);
        }

    }

    private sealed class StubScoringEngine(MatchingApiFactory factory) : IMatchingScoringEngine
    {
        public Task<IReadOnlyCollection<MatchingCriterion>> EvaluateAsync(ProfileMatchingData profile, JobMatchingData job, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(factory.CriteriaByJob.TryGetValue(job.Id, out var criteria)
                ? criteria
                : factory.Criteria);
        }
    }
}
