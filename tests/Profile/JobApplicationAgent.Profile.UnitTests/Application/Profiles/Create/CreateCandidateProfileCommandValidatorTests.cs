using FluentValidation.TestHelper;
using JobApplicationAgent.Profile.Application.Profiles.Create;

namespace JobApplicationAgent.Profile.UnitTests.Application.Profiles.Create
{
    public sealed class CreateCandidateProfileCommandValidatorTests
    {
        private readonly CreateCandidateProfileCommandValidator _validator = new();

        [Fact]
        public void Validate_ShouldNotHaveErrors_WhenCommandIsValid()
        {
            var command = new CreateCandidateProfileCommand(
                "Test",
                "Candidate",
                "test@example.com",
                null,
                "Data Engineer",
                "Candidate profile");

            var result = _validator.TestValidate(command);

            result.ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        public void Validate_ShouldHaveError_WhenFirstNameIsEmpty()
        {
            var command = new CreateCandidateProfileCommand(
                "",
                "Candidate",
                "test@example.com",
                null,
                "Data Engineer",
                null);

            var result = _validator.TestValidate(command);

            result.ShouldHaveValidationErrorFor(x => x.FirstName);
        }

        [Fact]
        public void Validate_ShouldHaveError_WhenLastNameIsEmpty()
        {
            var command = new CreateCandidateProfileCommand(
                "Test",
                "",
                "test@example.com",
                null,
                "Data Engineer",
                null);

            var result = _validator.TestValidate(command);

            result.ShouldHaveValidationErrorFor(x => x.LastName);
        }

        [Fact]
        public void Validate_ShouldHaveError_WhenEmailIsEmpty()
        {
            var command = new CreateCandidateProfileCommand(
                "Test",
                "Candidate",
                "",
                null,
                "Data Engineer",
                null);

            var result = _validator.TestValidate(command);

            result.ShouldHaveValidationErrorFor(x => x.Email);
        }

        [Fact]
        public void Validate_ShouldHaveError_WhenEmailIsInvalid()
        {
            var command = new CreateCandidateProfileCommand(
                "Test",
                "Candidate",
                "email-invalide",
                null,
                "Data Engineer",
                null);

            var result = _validator.TestValidate(command);

            result.ShouldHaveValidationErrorFor(x => x.Email);
        }

        [Fact]
        public void Validate_ShouldHaveError_WhenSummaryExceedsMaximumLength()
        {
            var command = new CreateCandidateProfileCommand(
                "Test",
                "Candidate",
                "test@example.com",
                null,
                "Data Engineer",
                new string('A', 2001));

            var result = _validator.TestValidate(command);

            result.ShouldHaveValidationErrorFor(x => x.Summary);
        }
    }
}