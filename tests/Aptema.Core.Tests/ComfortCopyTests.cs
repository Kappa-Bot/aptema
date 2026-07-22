using Aptema.Core;

namespace Aptema.Core.Tests;

public sealed class ComfortCopyTests
{
    [Theory]
    [InlineData(25, "Light")]
    [InlineData(45, "Balanced")]
    [InlineData(75, "Deep comfort")]
    public void DescribesComfortIntensityWithoutPercentages(int intensity, string expected)
    {
        Assert.Equal(expected, ComfortCopy.DescribeIntensity(intensity));
    }

    [Theory]
    [InlineData(88, "Bright")]
    [InlineData(68, "Comfortable")]
    [InlineData(48, "Soft")]
    [InlineData(30, "Very soft")]
    public void DescribesLightLevelWithoutTechnicalValues(int brightness, string expected)
    {
        Assert.Equal(expected, ComfortCopy.DescribeLightLevel(brightness));
    }

    [Fact]
    public void FriendlyReasonHidesCooldownAndHysteresisDetails()
    {
        var cooldown = Decision("Cooling down before next adjustment", "CooldownActive");
        var steady = Decision("No visible change needed", "NoChangeWithinHysteresis");

        Assert.Equal("Adjusting softly", ComfortCopy.DescribeReason(cooldown));
        Assert.Equal("Comfort steady", ComfortCopy.DescribeReason(steady));
    }

    private static ComfortDecision Decision(string reason, string code)
    {
        return new ComfortDecision(ComfortProfileId.Evening, 64, 5100, 0.03, TimeSpan.FromSeconds(90), false, reason, new[] { code });
    }
}
