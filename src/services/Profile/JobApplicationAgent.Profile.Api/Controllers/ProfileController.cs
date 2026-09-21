using JobApplicationAgent.Profile.Application.Profiles.Create;
using JobApplicationAgent.Profile.Application.Profiles.Get;
using Microsoft.AspNetCore.Mvc;
using JobApplicationAgent.Profile.Application.Profiles.Experiences.Add;
using JobApplicationAgent.Profile.Application.Profiles.Experiences.Get;
using JobApplicationAgent.Profile.Application.Profiles.Experiences.Update;
using JobApplicationAgent.Profile.Application.Profiles.Experiences.Delete;
using JobApplicationAgent.Profile.Application.Profiles.Educations.Add;
using JobApplicationAgent.Profile.Application.Profiles.Educations.Get;
using JobApplicationAgent.Profile.Application.Profiles.Educations.Update;
using JobApplicationAgent.Profile.Application.Profiles.Educations.Delete;


namespace JobApplicationAgent.Profile.Api.Controllers
{
    [ApiController]
    [Route("api/v1/profile")]
    public sealed class ProfileController(CreateCandidateProfileHandler createHandler,
                                            GetCandidateProfileHandler getHandler,
                                            AddProfessionalExperienceHandler addExperienceHandler,
                                            GetProfessionalExperiencesHandler getExperiencesHandler,
                                            UpdateProfessionalExperienceHandler updateExperienceHandler,
                                            DeleteProfessionalExperienceHandler deleteExperienceHandler,
                                            AddEducationHandler addEducationHandler,
                                            GetEducationsHandler getEducationsHandler,
                                            UpdateEducationHandler updateEducationHandler,
                                            DeleteEducationHandler deleteEducationHandler
                                            ) : ControllerBase
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
        public async Task<IActionResult> AddExperience(AddProfessionalExperienceCommand command, CancellationToken cancellationToken)
        {
            var experience = await addExperienceHandler.HandleAsync(command, cancellationToken);

            return Created($"/api/v1/profile/experiences/{experience.Id}", experience);
        }
        [HttpGet("experiences")]
        public async Task<IActionResult> GetExperiences(CancellationToken cancellationToken)
        {
            var experiences = await getExperiencesHandler.HandleAsync(cancellationToken);
            return Ok(experiences);
        }
        [HttpPut("experiences/{id:guid}")]
        public async Task<IActionResult> UpdateExperience(Guid id, UpdateProfessionalExperienceCommand command, CancellationToken cancellationToken)
        {
            var experience =
                await updateExperienceHandler.HandleAsync(
                    id,
                    command,
                    cancellationToken);

            return Ok(experience);
        }

        [HttpDelete("experiences/{id:guid}")]
        public async Task<IActionResult> DeleteExperience(Guid id, CancellationToken cancellationToken)
        {
            await deleteExperienceHandler.HandleAsync(
                id,
                cancellationToken);

            return NoContent();
        }
        [HttpPost("educations")]
        public async Task<IActionResult> AddEducation(AddEducationCommand command, CancellationToken cancellationToken)
        {
            var education = await addEducationHandler.HandleAsync(command, cancellationToken);

            return StatusCode(
                StatusCodes.Status201Created,
                education);
        }
        [HttpGet("educations")]
        public async Task<IActionResult> GetEducations(CancellationToken cancellationToken)
        {
            var educations = await getEducationsHandler.HandleAsync(cancellationToken);

            return Ok(educations);
        }
        [HttpPut("educations/{id:guid}")]
        public async Task<IActionResult> UpdateEducation(Guid id, UpdateEducationCommand command, CancellationToken cancellationToken)
        {
            var education =
                await updateEducationHandler.HandleAsync(
                    id,
                    command,
                    cancellationToken);

            return Ok(education);
        }
        [HttpDelete("educations/{id:guid}")]
        public async Task<IActionResult> DeleteEducation(Guid id,CancellationToken cancellationToken)
        {
            await deleteEducationHandler.HandleAsync(
                id,
                cancellationToken);

            return NoContent();
        }
    }
}
