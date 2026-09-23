namespace JobApplicationAgent.Profile.Domain.Entities;

public sealed class CandidatePreferences
{
    private CandidatePreferences() { }

    public CandidatePreferences(Guid candidateProfileId,
        string[] desiredJobTitles,
        string[] preferredLocations,
        string[] contractTypes,
        string[] workModes,
        decimal? minimumAnnualGrossSalary,
        string? salaryCurrency,
        DateOnly? availableFrom)
    {
        CandidateProfileId = candidateProfileId;
        CreatedAtUtc = DateTime.UtcNow;
        Update(desiredJobTitles, preferredLocations, contractTypes, workModes, minimumAnnualGrossSalary, salaryCurrency, availableFrom);
    }

    public Guid CandidateProfileId { get; private set; }
    public CandidateProfile CandidateProfile { get; private set; } = null!;
    public string[] DesiredJobTitles { get; private set; } = [];
    public string[] PreferredLocations { get; private set; } = [];
    public string[] ContractTypes { get; private set; } = [];
    public string[] WorkModes { get; private set; } = [];
    public decimal? MinimumAnnualGrossSalary { get; private set; }
    public string? SalaryCurrency { get; private set; }
    public DateOnly? AvailableFrom { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public void Update(
        string[] desiredJobTitles,
        string[] preferredLocations,
        string[] contractTypes,
        string[] workModes,
        decimal? minimumAnnualGrossSalary,
        string? salaryCurrency,
        DateOnly? availableFrom)
    {
        DesiredJobTitles = desiredJobTitles.ToArray();
        PreferredLocations = preferredLocations.ToArray();
        ContractTypes = contractTypes.ToArray();
        WorkModes = workModes.ToArray();
        MinimumAnnualGrossSalary = minimumAnnualGrossSalary;
        SalaryCurrency = salaryCurrency;
        AvailableFrom = availableFrom;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
