using JobApplicationAgent.Profile.Domain.Entities;

namespace JobApplicationAgent.Profile.UnitTests.Domain.Entities;

public sealed class CandidatePreferencesTests
{
    [Fact]
    public void SetPreferences_ShouldCopyInputAndPreserveIdentityOnUpdate()
    {
        var profile = new CandidateProfile("Test", "Candidate", "test@example.com");
        string[] titles = ["Developer"];
        var preferences = profile.SetPreferences(titles, [], [], [], null, null, null);
        titles[0] = "Changed";
        Assert.Equal("Developer", preferences.DesiredJobTitles[0]);
        Assert.Equal(profile.Id, preferences.CandidateProfileId);
        var created = preferences.CreatedAtUtc;
        var updated = profile.SetPreferences([], [], [], [], null, null, null);
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
