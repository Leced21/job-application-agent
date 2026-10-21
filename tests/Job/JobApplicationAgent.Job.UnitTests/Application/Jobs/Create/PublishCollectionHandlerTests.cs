using FluentValidation;
using JobApplicationAgent.Job.Application.Jobs.Create;
using JobApplicationAgent.Job.Domain.Enums;
using NSubstitute;

namespace JobApplicationAgent.Job.UnitTests.Application.Jobs.Create;

public sealed class PublishCollectionHandlerTests
{
    [Theory]
    [InlineData("skills")]
    [InlineData("mode")]
    [InlineData("title")]
    public async Task InvalidPayload_ShouldFailBeforePersistence(string field)
    {
        var repository = Substitute.For<ICollectionPublicationRepository>();
        var command = new PublishCollectionCommand(Guid.NewGuid(), Guid.NewGuid(), new string('A', 64),
            new("Engineer", "Company", null, WorkMode.Remote, ContractType.Permanent, null, null, null,
                null, "Manual", null, null), ["SQL"], null);
        command = field switch
        {
            "skills" => command with { Skills = [" "] },
            "mode" => command with { Job = command.Job with { WorkMode = (WorkMode)999 } },
            _ => command with { Job = command.Job with { Title = " " } }
        };
        var handler = new PublishCollectionHandler(repository, new CreateJobCommandValidator());
        await Assert.ThrowsAsync<ValidationException>(() => handler.HandleAsync(command, default));
        Assert.Empty(repository.ReceivedCalls());
    }
}
