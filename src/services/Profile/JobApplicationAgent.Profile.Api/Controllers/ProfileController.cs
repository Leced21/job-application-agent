using JobApplicationAgent.Profile.Application.Profiles.Create;
using JobApplicationAgent.Profile.Application.Profiles.Get;
using Microsoft.AspNetCore.Mvc;
using JobApplicationAgent.Profile.Application.Profiles.Experiences.Add;

namespace JobApplicationAgent.Profile.Api.Controllers
{
    [ApiController]
    [Route("api/v1/profile")]
    public sealed class ProfileController(CreateCandidateProfileHandler createHandler, GetCandidateProfileHandler getHandler, AddProfessionalExperienceHandler addExperienceHandler) : ControllerBase
    {
        [HttpGet]
        public async Task<IActionResult> Get(CancellationToken cancellationToken)
        {
            var profile = await getHandler.HandleAsync(cancellationToken);

            return profile is null ? NotFound() : Ok(profile);
        }

        [HttpPost]
        public async Task<IActionResult> Create(CreateCandidateProfileCommand command, CancellationToken cancellationToken)
        {
            var profile =
                await createHandler.HandleAsync(command, cancellationToken);

            return CreatedAtAction(nameof(Get), profile);
        }
        [HttpPost("experiences")]
        public async Task<IActionResult> AddExperience(AddProfessionalExperienceCommand command,CancellationToken cancellationToken)
        {
            var experience = await addExperienceHandler.HandleAsync(command,cancellationToken);

            return Created($"/api/v1/profile/experiences/{experience.Id}", experience);
        }

    }
}
