using JobApplicationAgent.Profile.Domain.Entities;

namespace JobApplicationAgent.Profile.UnitTests.Domain.Entities;

public sealed class CandidatePreferencesTests
{
    [Fact]
    public void UpdatePreferences_ShouldReturnNullWithoutChangingProfile_WhenMissing()
    {
        var profile = new CandidateProfile("Test", "Candidate", "test@example.com");
        var timestamp = profile.UpdatedAtUtc;

        Assert.Null(profile.UpdatePreferences([], [], [], []));
        Assert.Null(profile.Preferences);
        Assert.Equal(timestamp, profile.UpdatedAtUtc);
    }

    [Fact]
    public void AddPreferences_ShouldRejectDuplicateWithoutReplacingExistingPreferences()
    {
        var profile = new CandidateProfile("Test", "Candidate", "test@example.com");
        var original = profile.AddPreferences(["Developer"], [], [], []);
        var timestamp = profile.UpdatedAtUtc;

        Assert.Null(original.MinimumAnnualGrossSalary);
        Assert.Null(original.SalaryCurrency);
        Assert.Null(original.AvailableFrom);
        Assert.Throws<InvalidOperationException>(() => profile.AddPreferences([], [], [], []));
        Assert.Same(original, profile.Preferences);
        Assert.Equal("Developer", original.DesiredJobTitles[0]);
        Assert.Equal(timestamp, profile.UpdatedAtUtc);
    }

    [Fact]
    public void Preferences_ShouldCopyInputAndPreserveIdentityOnUpdate()
    {
        var profile = new CandidateProfile("Test", "Candidate", "test@example.com");
        string[] titles = ["Developer"];
        var preferences = profile.AddPreferences(titles, [], [], [], null, null, null);
        titles[0] = "Changed";
        Assert.Equal("Developer", preferences.DesiredJobTitles[0]);
        Assert.Equal(profile.Id, preferences.CandidateProfileId);
        var created = preferences.CreatedAtUtc;
        var updated = profile.UpdatePreferences([], [], [], [], null, null, null);
        Assert.NotNull(updated);
        Assert.Same(preferences, updated);
        Assert.Equal(created, updated.CreatedAtUtc);
        Assert.Empty(updated.DesiredJobTitles);
        Assert.True(profile.RemovePreferences());
        Assert.Null(profile.Preferences);
        var timestamp = profile.UpdatedAtUtc;
        Assert.False(profile.RemovePreferences());
        Assert.Equal(timestamp, profile.UpdatedAtUtc);
    }
}
