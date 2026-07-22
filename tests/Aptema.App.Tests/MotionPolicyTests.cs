using Aptema.App.Presentation;

namespace Aptema.App.Tests;

public sealed class MotionPolicyTests
{
    [Theory]
    [InlineData(false, 160)]
    [InlineData(true, 0)]
    public void UsesPremiumMotionOnlyWhenMotionIsAllowed(bool reducedMotion, int expectedMilliseconds)
    {
        Assert.Equal(TimeSpan.FromMilliseconds(expectedMilliseconds), MotionPolicy.GetTransitionDuration(reducedMotion));
    }
}
