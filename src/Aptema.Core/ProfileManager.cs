namespace Aptema.Core;

public sealed record ComfortProfile(
    ComfortProfileId Id,
    string DisplayName,
    int DayBrightness,
    int EveningBrightness,
    int NightBrightness,
    int DayKelvin,
    int EveningKelvin,
    int NightKelvin);

public sealed class ProfileManager
{
    private readonly IReadOnlyDictionary<ComfortProfileId, ComfortProfile> _profiles;

    public ProfileManager()
    {
        var profiles = new[]
        {
            new ComfortProfile(ComfortProfileId.Auto, "Auto", 78, 66, 56, 6500, 4600, 3800),
            new ComfortProfile(ComfortProfileId.Day, "Day", 80, 80, 80, 6500, 6500, 6500),
            new ComfortProfile(ComfortProfileId.Evening, "Evening", 74, 64, 56, 5800, 4600, 3800),
            new ComfortProfile(ComfortProfileId.Night, "Night", 62, 56, 50, 4600, 3900, 3400),
            new ComfortProfile(ComfortProfileId.Reading, "Reading", 72, 58, 52, 5800, 4200, 3600),
            new ComfortProfile(ComfortProfileId.Gaming, "Gaming", 70, 66, 62, 6500, 5000, 4700),
            new ComfortProfile(ComfortProfileId.Video, "Video", 70, 68, 64, 6500, 5200, 4800),
            new ComfortProfile(ComfortProfileId.Development, "Development", 74, 64, 56, 6200, 4600, 3900),
            new ComfortProfile(ComfortProfileId.Manual, "Manual", 70, 70, 70, 6500, 6500, 6500),
            new ComfortProfile(ComfortProfileId.Paused, "Paused", 70, 70, 70, 6500, 6500, 6500)
        };

        _profiles = profiles.ToDictionary(profile => profile.Id);
    }

    public IReadOnlyCollection<ComfortProfile> Profiles => _profiles.Values.ToArray();

    public ComfortProfile Get(ComfortProfileId id) => _profiles[id];
}
