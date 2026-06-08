using Invensys.ITrace.Application.Telemetry;

namespace Invensys.ITrace.Tests;

public sealed class MetricMathTests
{
    [Fact]
    public void Percentile_ReturnsZero_ForEmptyInput()
    {
        var result = MetricMath.Percentile([], 95);

        Assert.Equal(0, result);
    }

    [Fact]
    public void Percentile_InterpolatesBetweenNearestRanks()
    {
        var result = MetricMath.Percentile([10, 20, 30, 40], 75);

        Assert.Equal(32.5, result);
    }

    [Fact]
    public void Percentile_IgnoresInvalidFloatingPointValues()
    {
        var result = MetricMath.Percentile([10, double.NaN, 20, double.PositiveInfinity], 50);

        Assert.Equal(15, result);
    }
}
