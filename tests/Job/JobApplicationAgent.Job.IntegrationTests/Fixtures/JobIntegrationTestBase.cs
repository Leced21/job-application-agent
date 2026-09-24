using JobApplicationAgent.Job.IntegrationTests.Infrastructure;

namespace JobApplicationAgent.Job.IntegrationTests.Fixtures;

public abstract class JobIntegrationTestBase: IClassFixture<JobApiFactory>
{
    protected JobIntegrationTestBase(
        JobApiFactory factory)
    {
        Client = factory.CreateClient();
    }

    protected HttpClient Client { get; }
}