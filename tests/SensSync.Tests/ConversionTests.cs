using SensSync.Core;

namespace SensSync.Tests;

public sealed class ConversionTests
{
    private static readonly LinearSensitivityModel Cs2 = new(0.022);
    private static readonly LinearSensitivityModel Valorant = new(0.07);

    [Fact]
    public void GoldenCs2ToValorantExample()
    {
        Assert.Equal(51.95454545454546, Cs2.ToCmPer360(1, 800), 10);
        Assert.Equal(0.3142857142857142, Cs2.ConvertTo(1, 800, Valorant, 800), 12);
        Assert.Equal(0.1571428571428571, Cs2.ConvertTo(1, 800, Valorant, 1600), 12);
    }

    [Fact]
    public void ActualYawChangesThePhysicalResponse()
    {
        var custom = new LinearSensitivityModel(0.044);
        Assert.Equal(Cs2.ToCmPer360(1, 800) / 2, custom.ToCmPer360(1, 800), 12);
        Assert.Equal(0.6285714285714284, custom.ConvertTo(1, 800, Valorant, 800), 12);
    }

    [Theory]
    [InlineData(0.001, 400, 0.011)]
    [InlineData(0.64, 800, 0.022)]
    [InlineData(2.123456789, 1600, 0.03)]
    [InlineData(100, 25600, 0.044)]
    public void RoundTripsRetainPrecision(double sensitivity, double dpi, double yaw)
    {
        var model = new LinearSensitivityModel(yaw);
        double restored = model.FromCmPer360(model.ToCmPer360(sensitivity, dpi), dpi);
        Assert.InRange(Math.Abs(restored / sensitivity - 1), 0, 1e-12);
    }

    [Fact]
    public void IncreasingDpiWhileHoldingDistanceHalvesTheRequiredSetting()
    {
        Assert.Equal(Cs2.FromCmPer360(40, 800) / 2, Cs2.FromCmPer360(40, 1600), 12);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void InvalidInputsCannotProduceAResult(double invalid)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new LinearSensitivityModel(invalid));
        Assert.Throws<ArgumentOutOfRangeException>(() => Cs2.ToCmPer360(invalid, 800));
        Assert.Throws<ArgumentOutOfRangeException>(() => Cs2.ToCmPer360(1, invalid));
        Assert.Throws<ArgumentOutOfRangeException>(() => Cs2.FromCmPer360(invalid, 800));
        Assert.Throws<ArgumentOutOfRangeException>(() => Cs2.FromCmPer360(40, invalid));
    }

    [Fact]
    public void OverflowAndUnderflowAreRejectedInsteadOfClamped()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Cs2.ToCmPer360(double.Epsilon, double.Epsilon));
        Assert.Throws<ArgumentOutOfRangeException>(() => Cs2.FromCmPer360(double.MaxValue, double.MaxValue));
    }
}
