using Aptema.App.Presentation;

namespace Aptema.App.Tests;

public sealed class ColorContrastPolicyTests
{
    [Theory]
    [InlineData("#0F4C52", "#FFFFFF")]
    [InlineData("#123F43", "#FFFFFF")]
    [InlineData("#1A7478", "#FFFFFF")]
    [InlineData("#145E62", "#FFFFFF")]
    [InlineData("#E7EFED", "#18211F")]
    [InlineData("#D5E4E0", "#18211F")]
    [InlineData("#23332F", "#F6F2E9")]
    [InlineData("#30443F", "#F6F2E9")]
    public void ButtonStatesMeetWcagTextContrast(string background, string foreground)
    {
        Assert.True(ColorContrastPolicy.ContrastRatio(background, foreground) >= 4.5);
    }
}
