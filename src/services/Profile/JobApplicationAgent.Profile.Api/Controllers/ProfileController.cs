using JobApplicationAgent.Profile.Application.Profiles.Preferences.Get;
using JobApplicationAgent.Profile.Application.Profiles.Preferences.Update;
using JobApplicationAgent.Profile.Application.Profiles.Preferences.Delete;
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
using JobApplicationAgent.Profile.Application.Profiles.Skills.Add;
using JobApplicationAgent.Profile.Application.Profiles.Skills.Get;
using JobApplicationAgent.Profile.Application.Profiles.Skills.Update;
using JobApplicationAgent.Profile.Application.Profiles.Skills.Delete;
using JobApplicationAgent.Profile.Application.Profiles.Languages.Add;
using JobApplicationAgent.Profile.Application.Profiles.Links.Add;
using JobApplicationAgent.Profile.Application.Profiles.Certifications.Add;
using JobApplicationAgent.Profile.Application.Profiles.Languages.Get;
using JobApplicationAgent.Profile.Application.Profiles.Links.Get;
using JobApplicationAgent.Profile.Application.Profiles.Certifications.Get;
using JobApplicationAgent.Profile.Application.Profiles.Languages.Update;
using JobApplicationAgent.Profile.Application.Profiles.Links.Update;
using JobApplicationAgent.Profile.Application.Profiles.Certifications.Update;
using JobApplicationAgent.Profile.Application.Profiles.Languages.Delete;
using JobApplicationAgent.Profile.Application.Profiles.Links.Delete;
using JobApplicationAgent.Profile.Application.Profiles.Certifications.Delete;


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
                                            DeleteEducationHandler deleteEducationHandler,
                                            AddSkillHandler addSkillHandler,
                                            GetSkillsHandler getSkillsHandler,
                                            UpdateSkillHandler updateSkillHandler,
                                            DeleteSkillHandler deleteSkillHandler,
                                            AddLanguageHandler addLanguageHandler,
                                            GetLanguagesHandler getLanguagesHandler,
                                            UpdateLanguageHandler updateLanguageHandler,
                                            DeleteLanguageHandler deleteLanguageHandler,
                                            AddCertificationHandler addCertificationHandler,
                                            GetCertificationsHandler getCertificationsHandler,
                                            UpdateCertificationHandler updateCertificationHandler,
                                            DeleteCertificationHandler deleteCertificationHandler,
                                            AddLinkHandler addLinkHandler,
                                            GetLinksHandler getLinksHandler,
                                            UpdateLinkHandler updateLinkHandler,
                                            DeleteLinkHandler deleteLinkHandler,
                                            GetPreferencesHandler getPreferencesHandler,
                                            UpdatePreferencesHandler updatePreferencesHandler,
                                            DeletePreferencesHandler deletePreferencesHandler
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
        public async Task<IActionResult> DeleteEducation(Guid id, CancellationToken cancellationToken)
        {
            await deleteEducationHandler.HandleAsync(
                id,
                cancellationToken);

            return NoContent();
        }

        [HttpPost("skills")]
        public async Task<IActionResult> AddSkill(AddSkillCommand command, CancellationToken cancellationToken)
        {
            var skill = await addSkillHandler.HandleAsync(command, cancellationToken);

            return StatusCode(StatusCodes.Status201Created, skill);
        }
        [HttpGet("skills")]
        public async Task<IActionResult> GetSkills(CancellationToken cancellationToken)
        {
            var skills =
                await getSkillsHandler.HandleAsync(
                    cancellationToken);

            return Ok(skills);
        }
        [HttpPut("skills/{skillId:guid}")]
        public async Task<IActionResult> UpdateSkill(Guid skillId, UpdateSkillCommand command, CancellationToken cancellationToken)
        {
            var skill =
                await updateSkillHandler.HandleAsync(skillId, command, cancellationToken);
            return Ok(skill);
        }
        [HttpDelete("skills/{skillId:guid}")]
        public async Task<IActionResult> DeleteSkill(Guid skillId, CancellationToken cancellationToken)
        {
            await deleteSkillHandler.HandleAsync(skillId, cancellationToken);

            return NoContent();
        }
        [HttpPost("languages")]
        public async Task<IActionResult> AddLanguage(AddLanguageCommand command, CancellationToken cancellationToken)
        {
            var language = await addLanguageHandler.HandleAsync(
                command,
                cancellationToken);

            return StatusCode(
                StatusCodes.Status201Created,
                language);
        }
        [HttpGet("languages")]
        public async Task<IActionResult> GetLanguages(CancellationToken cancellationToken)
        {
            var languages = await getLanguagesHandler.HandleAsync(cancellationToken);

            return Ok(languages);
        }
        [HttpPut("languages/{languageId:guid}")]
        public async Task<IActionResult> UpdateLanguage(Guid languageId, UpdateLanguageCommand command, CancellationToken cancellationToken)
        {
            var language =
                await updateLanguageHandler.HandleAsync(
                    languageId,
                    command,
                    cancellationToken);

            return Ok(language);
        }
        [HttpDelete("languages/{languageId:guid}")]
        public async Task<IActionResult> DeleteLanguage(Guid languageId, CancellationToken cancellationToken)
        {
            await deleteLanguageHandler.HandleAsync(
                languageId,
                cancellationToken);

            return NoContent();
        }
        [HttpPost("links")]
        public async Task<IActionResult> AddLink(AddLinkCommand command, CancellationToken cancellationToken)
        {
            var link = await addLinkHandler.HandleAsync(
                command,
                cancellationToken);

            return StatusCode(
                StatusCodes.Status201Created,
                link);
        }
        [HttpGet("links")]
        public async Task<IActionResult> GetLinks(CancellationToken cancellationToken)
        {
            var links = await getLinksHandler.HandleAsync(cancellationToken);

            return Ok(links);
        }
        [HttpPut("links/{linkId:guid}")]
        public async Task<IActionResult> UpdateLink(Guid linkId, UpdateLinkCommand command, CancellationToken cancellationToken)
        {
            var link =
                await updateLinkHandler.HandleAsync(
                    linkId,
                    command,
                    cancellationToken);

            return Ok(link);
        }
        [HttpDelete("links/{linkId:guid}")]
        public async Task<IActionResult> DeleteLink(Guid linkId, CancellationToken cancellationToken)
        {
            await deleteLinkHandler.HandleAsync(
                linkId,
                cancellationToken);

            return NoContent();
        }
        [HttpPost("certifications")]
        public async Task<IActionResult> AddCertification(AddCertificationCommand command, CancellationToken cancellationToken)
        {
            var certification = await addCertificationHandler.HandleAsync(
                command,
                cancellationToken);

            return StatusCode(
                StatusCodes.Status201Created,
                certification);
        }
        [HttpGet("certifications")]
        public async Task<IActionResult> GetCertifications(CancellationToken cancellationToken)
        {
            var certifications = await getCertificationsHandler.HandleAsync(cancellationToken);

            return Ok(certifications);
        }
        [HttpPut("certifications/{certificationId:guid}")]
        public async Task<IActionResult> UpdateCertification(Guid certificationId, UpdateCertificationCommand command, CancellationToken cancellationToken)
        {
            var certification =
                await updateCertificationHandler.HandleAsync(
                    certificationId,
                    command,
                    cancellationToken);

            return Ok(certification);
        }
        [HttpDelete("certifications/{certificationId:guid}")]
        public async Task<IActionResult> DeleteCertification(Guid certificationId, CancellationToken cancellationToken)
        {
            await deleteCertificationHandler.HandleAsync(
                certificationId,
                cancellationToken);

            return NoContent();
        }

        [HttpGet("preferences")]
        public async Task<IActionResult> GetPreferences(CancellationToken cancellationToken)
        {
            return Ok(await getPreferencesHandler.HandleAsync(cancellationToken));
        }

        [HttpPut("preferences")]
        public async Task<IActionResult> UpdatePreferences(UpdatePreferencesCommand command, CancellationToken cancellationToken)
        {
            return Ok(await updatePreferencesHandler.HandleAsync(command, cancellationToken));
        }

        [HttpDelete("preferences")]
        public async Task<IActionResult> DeletePreferences(CancellationToken cancellationToken)
        {
            await deletePreferencesHandler.HandleAsync(cancellationToken);
            return NoContent();
        }
    }
}
