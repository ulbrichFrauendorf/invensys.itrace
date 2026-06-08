namespace Invensys.ITrace.Api.Services;

public static class MetricMath
{
    public static double Percentile(IEnumerable<double> values, double percentile)
    {
        var ordered = values
            .Where(value => !double.IsNaN(value) && !double.IsInfinity(value))
            .OrderBy(value => value)
            .ToArray();

        if (ordered.Length == 0)
        {
            return 0;
        }

        var normalized = Math.Clamp(percentile, 0, 100) / 100;
        var position = (ordered.Length - 1) * normalized;
        var lower = (int)Math.Floor(position);
        var upper = (int)Math.Ceiling(position);

        if (lower == upper)
        {
            return ordered[lower];
        }

        var weight = position - lower;
        return ordered[lower] + ((ordered[upper] - ordered[lower]) * weight);
    }

    public static double Round(double value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
