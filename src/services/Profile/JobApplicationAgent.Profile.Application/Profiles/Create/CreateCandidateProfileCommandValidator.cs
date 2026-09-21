using FluentValidation;

namespace JobApplicationAgent.Profile.Application.Profiles.Create
{
    public sealed class CreateCandidateProfileCommandValidator:AbstractValidator<CreateCandidateProfileCommand>
    {
        public CreateCandidateProfileCommandValidator() 
        {
            RuleFor(x => x.FirstName)
                .NotEmpty()
                .MaximumLength(100);
            RuleFor(x => x.LastName)
                .NotEmpty()
                .MaximumLength(100);

            RuleFor(x => x.Email)
                .NotEmpty()
                .EmailAddress()
                .MaximumLength(255);

            RuleFor(x => x.PhoneNumber)
                .MaximumLength(30)
                .When(x => x.PhoneNumber is not null);

            RuleFor(x => x.JobTitle)
                .MaximumLength(150)
                .When(x => x.JobTitle is not null);

            RuleFor(x => x.Summary)
                .MaximumLength(2000)
                .When(x => x.Summary is not null);
        }
    }
}
