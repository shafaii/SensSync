using SensSync.Core;

namespace SensSync.Tests;

public sealed class TargetConversionTests
{
    [Fact]
    public void SamePhysicalSourceProducesGameSpecificValues()
    {
        double cm = new LinearSensitivityModel(0.022).ToCmPer360(0.64, 1600);
        Assert.Equal(0.2011428571428571, ConversionTarget.Valorant.FromCmPer360(cm, 1600), 12);
        Assert.Equal(0.64, ConversionTarget.Cs2.FromCmPer360(cm, 1600), 12);
        Assert.Equal(0.8045714285714284, ConversionTarget.MarvelRivals.FromCmPer360(cm, 1600), 12);
        Assert.Equal(6.776170102071705, ConversionTarget.Battlefield6.FromCmPer360(cm, 1600), 10);
    }

    [Fact]
    public void BattlefieldRejectsUnreachableDistancesInsteadOfClamping()
    {
        Assert.Throws<ArgumentException>(() => ConversionTarget.Battlefield6.FromCmPer360(1000, 1600));
        Assert.Throws<ArgumentException>(() => ConversionTarget.Battlefield6.FromCmPer360(0.01, 1600));
        // Non-zero base turn rate means the zero menu setting is physically meaningful.
        double slowest = 914.4 / 800 / 0.002314981532;
        Assert.Equal(0, ConversionTarget.Battlefield6.FromCmPer360(slowest, 800), 10);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void EveryTargetRejectsInvalidPhysicalInput(double value)
    {
        foreach (var target in ConversionTarget.All)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => target.FromCmPer360(value, 800));
            Assert.Throws<ArgumentOutOfRangeException>(() => target.FromCmPer360(40, value));
        }
    }

    [Fact]
    public void CustomSourceYawDoesNotBecomeTargetCs2Yaw()
    {
        double cm = new LinearSensitivityModel(0.044).ToCmPer360(1, 800);
        Assert.Equal(2, ConversionTarget.Cs2.FromCmPer360(cm, 800), 12);
    }
}
