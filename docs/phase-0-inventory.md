# Inventaire de phase 0

Inventaire actualisé après l'ajout 84.1. Les compteurs initiaux et constats avant modification figurent dans le rapport d'audit.

## Projets

- `src/gateway/JobApplicationAgent.Gateway/JobApplicationAgent.Gateway.csproj`
- `src/services/Job/JobApplicationAgent.Job.Api/JobApplicationAgent.Job.Api.csproj`
- `src/services/Job/JobApplicationAgent.Job.Application/JobApplicationAgent.Job.Application.csproj`
- `src/services/Job/JobApplicationAgent.Job.Domain/JobApplicationAgent.Job.Domain.csproj`
- `src/services/Job/JobApplicationAgent.Job.Infrastructure/JobApplicationAgent.Job.Infrastructure.csproj`
- `src/services/Matching/JobApplicationAgent.Matching.Api/JobApplicationAgent.Matching.Api.csproj`
- `src/services/Matching/JobApplicationAgent.Matching.Application/JobApplicationAgent.Matching.Application.csproj`
- `src/services/Matching/JobApplicationAgent.Matching.Domain/JobApplicationAgent.Matching.Domain.csproj`
- `src/services/Matching/JobApplicationAgent.Matching.Infrastructure/JobApplicationAgent.Matching.Infrastructure.csproj`
- `src/services/Profile/JobApplicationAgent.Profile.Api/JobApplicationAgent.Profile.Api.csproj`
- `src/services/Profile/JobApplicationAgent.Profile.Application/JobApplicationAgent.Profile.Application.csproj`
- `src/services/Profile/JobApplicationAgent.Profile.Domain/JobApplicationAgent.Profile.Domain.csproj`
- `src/services/Profile/JobApplicationAgent.Profile.Infrastructure/JobApplicationAgent.Profile.Infrastructure.csproj`
- `tests/Job/JobApplicationAgent.Job.IntegrationTests/JobApplicationAgent.Job.IntegrationTests.csproj`
- `tests/Job/JobApplicationAgent.Job.UnitTests/JobApplicationAgent.Job.UnitTests.csproj`
- `tests/Matching/JobApplicationAgent.Matching.IntegrationTests/JobApplicationAgent.Matching.IntegrationTests.csproj`
- `tests/Matching/JobApplicationAgent.Matching.UnitTests/JobApplicationAgent.Matching.UnitTests.csproj`
- `tests/Profile/JobApplicationAgent.Profile.IntegrationTests/JobApplicationAgent.Profile.IntegrationTests.csproj`
- `tests/Profile/JobApplicationAgent.Profile.UnitTests/JobApplicationAgent.Profile.UnitTests.csproj`

## Contrôleurs

- `src/services/Job/JobApplicationAgent.Job.Api/Controllers/JobsController.cs`
- `src/services/Matching/JobApplicationAgent.Matching.Api/Controllers/MatchingController.cs`
- `src/services/Profile/JobApplicationAgent.Profile.Api/Controllers/ProfileController.cs`

## Handlers

- `src/services/Job/JobApplicationAgent.Job.Application/Jobs/Create/CreateJobHandler.cs`
- `src/services/Job/JobApplicationAgent.Job.Application/Jobs/Delete/DeleteJobHandler.cs`
- `src/services/Job/JobApplicationAgent.Job.Application/Jobs/Get/GetJobByIdHandler.cs`
- `src/services/Job/JobApplicationAgent.Job.Application/Jobs/Get/GetJobsHandler.cs`
- `src/services/Job/JobApplicationAgent.Job.Application/Jobs/Get/GetRecommendableJobsHandler.cs`
- `src/services/Job/JobApplicationAgent.Job.Application/Jobs/Status/ChangeJobStatusHandler.cs`
- `src/services/Job/JobApplicationAgent.Job.Application/Jobs/Update/UpdateJobHandler.cs`
- `src/services/Matching/JobApplicationAgent.Matching.Application/Matching/CalculateMatchingHandler.cs`
- `src/services/Matching/JobApplicationAgent.Matching.Application/Matching/GetMatchingResultHandler.cs`
- `src/services/Matching/JobApplicationAgent.Matching.Application/Matching/RecommendationHandler.cs`
- `src/services/Matching/JobApplicationAgent.Matching.Application/Matching/SearchMatchingResultsHandler.cs`
- `src/services/Profile/JobApplicationAgent.Profile.Application/Profiles/Certifications/Add/AddCertificationHandler.cs`
- `src/services/Profile/JobApplicationAgent.Profile.Application/Profiles/Certifications/Delete/DeleteCertificationHandler.cs`
- `src/services/Profile/JobApplicationAgent.Profile.Application/Profiles/Certifications/Get/GetCertificationsHandler.cs`
- `src/services/Profile/JobApplicationAgent.Profile.Application/Profiles/Certifications/Update/UpdateCertificationHandler.cs`
- `src/services/Profile/JobApplicationAgent.Profile.Application/Profiles/Create/CreateCandidateProfileHandler.cs`
- `src/services/Profile/JobApplicationAgent.Profile.Application/Profiles/Educations/Add/AddEducationHandler.cs`
- `src/services/Profile/JobApplicationAgent.Profile.Application/Profiles/Educations/Delete/DeleteEducationHandler.cs`
- `src/services/Profile/JobApplicationAgent.Profile.Application/Profiles/Educations/Get/GetEducationsHandler.cs`
- `src/services/Profile/JobApplicationAgent.Profile.Application/Profiles/Educations/Update/UpdateEducationHandler.cs`
- `src/services/Profile/JobApplicationAgent.Profile.Application/Profiles/Experieneces/Add/AddProfessionalExperienceHandler.cs`
- `src/services/Profile/JobApplicationAgent.Profile.Application/Profiles/Experieneces/Delete/DeleteProfessionalExperienceHandler.cs`
- `src/services/Profile/JobApplicationAgent.Profile.Application/Profiles/Experieneces/Update/UpdateProfessionalExperienceHandler.cs`
- `src/services/Profile/JobApplicationAgent.Profile.Application/Profiles/Get/GetCandidateProfileHandler.cs`
- `src/services/Profile/JobApplicationAgent.Profile.Application/Profiles/Get/GetProfessionalExperiencesHandler.cs`
- `src/services/Profile/JobApplicationAgent.Profile.Application/Profiles/Languages/Add/AddLanguageHandler.cs`
- `src/services/Profile/JobApplicationAgent.Profile.Application/Profiles/Languages/Delete/DeleteLanguageHandler.cs`
- `src/services/Profile/JobApplicationAgent.Profile.Application/Profiles/Languages/Get/GetLanguagesHandler.cs`
- `src/services/Profile/JobApplicationAgent.Profile.Application/Profiles/Languages/Update/UpdateLanguageHandler.cs`
- `src/services/Profile/JobApplicationAgent.Profile.Application/Profiles/Links/Add/AddLinkHandler.cs`
- `src/services/Profile/JobApplicationAgent.Profile.Application/Profiles/Links/Delete/DeleteLinkHandler.cs`
- `src/services/Profile/JobApplicationAgent.Profile.Application/Profiles/Links/Get/GetLinksHandler.cs`
- `src/services/Profile/JobApplicationAgent.Profile.Application/Profiles/Links/Update/UpdateLinkHandler.cs`
- `src/services/Profile/JobApplicationAgent.Profile.Application/Profiles/Matching/GetProfileMatchingDataHandler.cs`
- `src/services/Profile/JobApplicationAgent.Profile.Application/Profiles/Preferences/Add/AddPreferencesHandler.cs`
- `src/services/Profile/JobApplicationAgent.Profile.Application/Profiles/Preferences/Delete/DeletePreferencesHandler.cs`
- `src/services/Profile/JobApplicationAgent.Profile.Application/Profiles/Preferences/Get/GetPreferencesHandler.cs`
- `src/services/Profile/JobApplicationAgent.Profile.Application/Profiles/Preferences/Update/UpdatePreferencesHandler.cs`
- `src/services/Profile/JobApplicationAgent.Profile.Application/Profiles/Skills/Add/AddSkillHandler.cs`
- `src/services/Profile/JobApplicationAgent.Profile.Application/Profiles/Skills/Delete/DeleteSkillHandler.cs`
- `src/services/Profile/JobApplicationAgent.Profile.Application/Profiles/Skills/Get/GetSkillsHandler.cs`
- `src/services/Profile/JobApplicationAgent.Profile.Application/Profiles/Skills/Update/UpdateSkillHandler.cs`
- `src/services/Profile/JobApplicationAgent.Profile.Application/Profiles/Update/UpdateCandidateProfileHandler.cs`

## Repositories et interfaces

- `src/services/Job/JobApplicationAgent.Job.Application/Abstractions/IJobRepository.cs`
- `src/services/Job/JobApplicationAgent.Job.Infrastructure/Repositories/JobRepository.cs`
- `src/services/Matching/JobApplicationAgent.Matching.Application/Abstractions/IMatchingResultRepository.cs`
- `src/services/Matching/JobApplicationAgent.Matching.Infrastructure/Persistence/Repositories/MatchingResultRepository.cs`
- `src/services/Profile/JobApplicationAgent.Profile.Application/Abstractions/ICandidateProfileRepository.cs`
- `src/services/Profile/JobApplicationAgent.Profile.Infrastructure/Repositories/CandidateProfileRepository.cs`

## Contextes EF

- `src/services/Job/JobApplicationAgent.Job.Infrastructure/Persistence/JobDbContext.cs`
- `src/services/Matching/JobApplicationAgent.Matching.Infrastructure/Persistence/MatchingDbContext.cs`
- `src/services/Profile/JobApplicationAgent.Profile.Infrastructure/Persistence/ProfileDbContext.cs`

## Migrations et snapshots

- `src/services/Job/JobApplicationAgent.Job.Infrastructure/Migrations/20260923140050_InitialJobs.cs`
- `src/services/Job/JobApplicationAgent.Job.Infrastructure/Migrations/JobDbContextModelSnapshot.cs`
- `src/services/Job/JobApplicationAgent.Job.Infrastructure/Persistence/Migrations/20260923160550_InitialJobSchema.cs`
- `src/services/Matching/JobApplicationAgent.Matching.Infrastructure/Persistence/Migrations/20261014125328_InitialMatchingSchema.cs`
- `src/services/Matching/JobApplicationAgent.Matching.Infrastructure/Persistence/Migrations/MatchingDbContextModelSnapshot.cs`
- `src/services/Profile/JobApplicationAgent.Profile.Infrastructure/Persistence/Migrations/20260919095933_InitialProfileSchema.cs`
- `src/services/Profile/JobApplicationAgent.Profile.Infrastructure/Persistence/Migrations/20260921111452_AddProfessionalExperiences.cs`
- `src/services/Profile/JobApplicationAgent.Profile.Infrastructure/Persistence/Migrations/20260921150458_AddEducations.cs`
- `src/services/Profile/JobApplicationAgent.Profile.Infrastructure/Persistence/Migrations/20260921194634_AddSkills.cs`
- `src/services/Profile/JobApplicationAgent.Profile.Infrastructure/Persistence/Migrations/20260922123619_AddLanguages.cs`
- `src/services/Profile/JobApplicationAgent.Profile.Infrastructure/Persistence/Migrations/20260922154203_AddCertifications.cs`
- `src/services/Profile/JobApplicationAgent.Profile.Infrastructure/Persistence/Migrations/20260922160504_AddLinks.cs`
- `src/services/Profile/JobApplicationAgent.Profile.Infrastructure/Persistence/Migrations/20260922161818_AddPreferences.cs`
- `src/services/Profile/JobApplicationAgent.Profile.Infrastructure/Persistence/Migrations/ProfileDbContextModelSnapshot.cs`

## Fichiers de tests

- `tests/Job/JobApplicationAgent.Job.IntegrationTests/Api/JobsControllerTests.cs`
- `tests/Job/JobApplicationAgent.Job.IntegrationTests/Api/RecommendableJobsTests.cs`
- `tests/Job/JobApplicationAgent.Job.IntegrationTests/Jobs/JobsEndpointsTests.cs`
- `tests/Job/JobApplicationAgent.Job.UnitTests/Application/Jobs/Create/CreateJobCommandValidatorTests.cs`
- `tests/Job/JobApplicationAgent.Job.UnitTests/Application/Jobs/Create/CreateJobHandlerTests.cs`
- `tests/Job/JobApplicationAgent.Job.UnitTests/Application/Jobs/Delete/DeleteJobHandlerTests.cs`
- `tests/Job/JobApplicationAgent.Job.UnitTests/Application/Jobs/Get/GetJobByIdHandlerTests.cs`
- `tests/Job/JobApplicationAgent.Job.UnitTests/Application/Jobs/Get/GetJobsHandlerTests.cs`
- `tests/Job/JobApplicationAgent.Job.UnitTests/Application/Jobs/Get/GetRecommendableJobsHandlerTests.cs`
- `tests/Job/JobApplicationAgent.Job.UnitTests/Application/Jobs/Update/UpdateJobCommandValidatorTests.cs`
- `tests/Job/JobApplicationAgent.Job.UnitTests/Application/Jobs/Update/UpdateJobHandlerTests.cs`
- `tests/Job/JobApplicationAgent.Job.UnitTests/Domain/Jobs/JobTests.cs`
- `tests/Matching/JobApplicationAgent.Matching.IntegrationTests/Api/MatchingControllerTests.cs`
- `tests/Matching/JobApplicationAgent.Matching.IntegrationTests/Infrastructure/MatchingResultRepositoryTests.cs`
- `tests/Matching/JobApplicationAgent.Matching.UnitTests/Application/Matching/CalculateMatchingHandlerTests.cs`
- `tests/Matching/JobApplicationAgent.Matching.UnitTests/Application/Matching/GetMatchingResultHandlerTests.cs`
- `tests/Matching/JobApplicationAgent.Matching.UnitTests/Application/Matching/RecommendationHandlerTests.cs`
- `tests/Matching/JobApplicationAgent.Matching.UnitTests/Application/Matching/RecommendationRankingServiceTests.cs`
- `tests/Matching/JobApplicationAgent.Matching.UnitTests/Application/Matching/SearchMatchingResultsHandlerTests.cs`
- `tests/Matching/JobApplicationAgent.Matching.UnitTests/Application/Scoring/LocationMatchingServiceTests.cs`
- `tests/Matching/JobApplicationAgent.Matching.UnitTests/Application/Scoring/StructuredMatchingScoringEngineTests.cs`
- `tests/Matching/JobApplicationAgent.Matching.UnitTests/Domain/Entities/MatchingResultTests.cs`
- `tests/Matching/JobApplicationAgent.Matching.UnitTests/Infrastructure/HttpClients/JobMatchingHttpClientTests.cs`
- `tests/Matching/JobApplicationAgent.Matching.UnitTests/Infrastructure/HttpClients/ProfileMatchingHttpClientTests.cs`
- `tests/Matching/JobApplicationAgent.Matching.UnitTests/Scoring/JobTitleSimilarityServiceTests.cs`
- `tests/Matching/JobApplicationAgent.Matching.UnitTests/Scoring/SkillMatchingServiceTests.cs`
- `tests/Profile/JobApplicationAgent.Profile.IntegrationTests/Api/CertificationControllerTests.cs`
- `tests/Profile/JobApplicationAgent.Profile.IntegrationTests/Api/ProfileControllerTests.cs`
- `tests/Profile/JobApplicationAgent.Profile.IntegrationTests/Api/ProfileMatchingControllerTests.cs`
- `tests/Profile/JobApplicationAgent.Profile.IntegrationTests/Infrastructure/Persistence/CandidateProfileRepositoryTests.cs`
- `tests/Profile/JobApplicationAgent.Profile.UnitTests/Application/Profiles/Certifications/Add/AddCertificationCommandValidatorTests.cs`
- `tests/Profile/JobApplicationAgent.Profile.UnitTests/Application/Profiles/Certifications/Add/AddCertificationHandlerTests.cs`
- `tests/Profile/JobApplicationAgent.Profile.UnitTests/Application/Profiles/Certifications/Delete/DeleteCertificationHandlerTests.cs`
- `tests/Profile/JobApplicationAgent.Profile.UnitTests/Application/Profiles/Certifications/Get/GetCertificationsHandlerTests.cs`
- `tests/Profile/JobApplicationAgent.Profile.UnitTests/Application/Profiles/Certifications/Update/UpdateCertificationCommandValidatorTests.cs`
- `tests/Profile/JobApplicationAgent.Profile.UnitTests/Application/Profiles/Certifications/Update/UpdateCertificationHandlerTests.cs`
- `tests/Profile/JobApplicationAgent.Profile.UnitTests/Application/Profiles/Create/CreateCandidateProfileCommandValidatorTests.cs`
- `tests/Profile/JobApplicationAgent.Profile.UnitTests/Application/Profiles/Create/CreateCandidateProfileHandlerTests.cs`
- `tests/Profile/JobApplicationAgent.Profile.UnitTests/Application/Profiles/Educations/Add/AddEducationCommandValidatorTests.cs`
- `tests/Profile/JobApplicationAgent.Profile.UnitTests/Application/Profiles/Educations/Add/AddEducationHandlerTests.cs`
- `tests/Profile/JobApplicationAgent.Profile.UnitTests/Application/Profiles/Educations/Delete/DeleteEducationHandlerTests.cs`
- `tests/Profile/JobApplicationAgent.Profile.UnitTests/Application/Profiles/Educations/Update/UpdateEducationCommandValidatorTests.cs`
- `tests/Profile/JobApplicationAgent.Profile.UnitTests/Application/Profiles/Educations/Update/UpdateEducationHandlerTests.cs`
- `tests/Profile/JobApplicationAgent.Profile.UnitTests/Application/Profiles/Experiences/Add/AddProfessionalExperienceCommandValidatorTests.cs`
- `tests/Profile/JobApplicationAgent.Profile.UnitTests/Application/Profiles/Experiences/Add/AddProfessionalExperienceHandlerTests.cs`
- `tests/Profile/JobApplicationAgent.Profile.UnitTests/Application/Profiles/Experiences/Delete/DeleteProfessionalExperienceHandlerTests.cs`
- `tests/Profile/JobApplicationAgent.Profile.UnitTests/Application/Profiles/Experiences/Update/UpdateProfessionalExperienceCommandValidatorTests.cs`
- `tests/Profile/JobApplicationAgent.Profile.UnitTests/Application/Profiles/Experiences/Update/UpdateProfessionalExperienceHandlerTests.cs`
- `tests/Profile/JobApplicationAgent.Profile.UnitTests/Application/Profiles/Get/GetCandidateProfileHandlerTests.cs`
- `tests/Profile/JobApplicationAgent.Profile.UnitTests/Application/Profiles/Get/GetEducationsHandlerTests.cs`
- `tests/Profile/JobApplicationAgent.Profile.UnitTests/Application/Profiles/Get/GetProfessionalExperiencesHandlerTests.cs`
- `tests/Profile/JobApplicationAgent.Profile.UnitTests/Application/Profiles/Languages/Add/AddLanguageCommandValidatorTests.cs`
- `tests/Profile/JobApplicationAgent.Profile.UnitTests/Application/Profiles/Languages/Add/AddLanguageHandlerTests.cs`
- `tests/Profile/JobApplicationAgent.Profile.UnitTests/Application/Profiles/Languages/Delete/DeleteLanguageHandlerTests.cs`
- `tests/Profile/JobApplicationAgent.Profile.UnitTests/Application/Profiles/Languages/Get/GetLanguagesHandlerTests.cs`
- `tests/Profile/JobApplicationAgent.Profile.UnitTests/Application/Profiles/Languages/Update/UpdateLanguageCommandValidatorTests.cs`
- `tests/Profile/JobApplicationAgent.Profile.UnitTests/Application/Profiles/Languages/Update/UpdateLanguageHandlerTests.cs`
- `tests/Profile/JobApplicationAgent.Profile.UnitTests/Application/Profiles/Links/Add/AddLinkCommandValidatorTests.cs`
- `tests/Profile/JobApplicationAgent.Profile.UnitTests/Application/Profiles/Links/Add/AddLinkHandlerTests.cs`
- `tests/Profile/JobApplicationAgent.Profile.UnitTests/Application/Profiles/Links/Delete/DeleteLinkHandlerTests.cs`
- `tests/Profile/JobApplicationAgent.Profile.UnitTests/Application/Profiles/Links/Get/GetLinksHandlerTests.cs`
- `tests/Profile/JobApplicationAgent.Profile.UnitTests/Application/Profiles/Links/Update/UpdateLinkCommandValidatorTests.cs`
- `tests/Profile/JobApplicationAgent.Profile.UnitTests/Application/Profiles/Links/Update/UpdateLinkHandlerTests.cs`
- `tests/Profile/JobApplicationAgent.Profile.UnitTests/Application/Profiles/Matching/GetProfileMatchingDataHandlerTests.cs`
- `tests/Profile/JobApplicationAgent.Profile.UnitTests/Application/Profiles/Preferences/Add/AddPreferencesCommandValidatorTests.cs`
- `tests/Profile/JobApplicationAgent.Profile.UnitTests/Application/Profiles/Preferences/Add/AddPreferencesHandlerTests.cs`
- `tests/Profile/JobApplicationAgent.Profile.UnitTests/Application/Profiles/Preferences/Delete/DeletePreferencesHandlerTests.cs`
- `tests/Profile/JobApplicationAgent.Profile.UnitTests/Application/Profiles/Preferences/Get/GetPreferencesHandlerTests.cs`
- `tests/Profile/JobApplicationAgent.Profile.UnitTests/Application/Profiles/Preferences/Update/UpdatePreferencesCommandValidatorTests.cs`
- `tests/Profile/JobApplicationAgent.Profile.UnitTests/Application/Profiles/Preferences/Update/UpdatePreferencesHandlerTests.cs`
- `tests/Profile/JobApplicationAgent.Profile.UnitTests/Application/Profiles/Skills/Add/AddSkillCommandValidatorTests.cs`
- `tests/Profile/JobApplicationAgent.Profile.UnitTests/Application/Profiles/Skills/Add/AddSkillHandlerTests.cs`
- `tests/Profile/JobApplicationAgent.Profile.UnitTests/Application/Profiles/Skills/Delete/DeleteSkillHandlerTests.cs`
- `tests/Profile/JobApplicationAgent.Profile.UnitTests/Application/Profiles/Skills/Get/GetSkillsHandlerTests.cs`
- `tests/Profile/JobApplicationAgent.Profile.UnitTests/Application/Profiles/Skills/Update/UpdateSkillCommandValidatorTests.cs`
- `tests/Profile/JobApplicationAgent.Profile.UnitTests/Application/Profiles/Skills/Update/UpdateSkillHandlerTests.cs`
- `tests/Profile/JobApplicationAgent.Profile.UnitTests/Application/Profiles/Update/UpdateCandidateProfileCommandValidatorTests.cs`
- `tests/Profile/JobApplicationAgent.Profile.UnitTests/Application/Profiles/Update/UpdateCandidateProfileHandlerTests.cs`
- `tests/Profile/JobApplicationAgent.Profile.UnitTests/Domain/Entities/CandidatePreferencesTests.cs`
- `tests/Profile/JobApplicationAgent.Profile.UnitTests/Domain/Entities/CandidateProfileTests.cs`
