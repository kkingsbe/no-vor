using System;
using NOVor.Core;

internal static class Program
{
    private static int _failures;

    private static void Main()
    {
        Equal(359d, NavMath.NormalizeDegrees(-1d), "normalize negative");
        Equal(1d, NavMath.NormalizeDegrees(361d), "normalize overflow");
        True(NavMath.IsToStation(0d, 10d), "ten degrees is TO");
        True(!NavMath.IsToStation(0d, 190d), "reciprocal is FROM");
        Equal(1852d, NavMath.CrossTrackMeters(0d, 1852d, 0d), "aircraft right of north course");
        Equal(-1852d, NavMath.CrossTrackMeters(0d, -1852d, 0d), "aircraft left of north course");
        Equal(-1852d, NavMath.CrossTrackMeters(90d, 0d, 1852d), "aircraft left of east course");
        Equal(-1d, NavMath.CrossTrackDeflection(1852d, 1852d), "right error commands left");
        Equal(0.5d, NavMath.CrossTrackDeflection(-926d, 1852d), "left error commands right");
        Equal(1d, NavMath.CrossTrackDeflection(-3704d, 1852d), "cross track clamps");
        Equal(90d, NavMath.SteeringErrorDegrees(0d, 90d), "steer right error");
        Equal(-90d, NavMath.SteeringErrorDegrees(0d, 270d), "steer left error");
        Equal(2d, NavMath.SteeringErrorDegrees(359d, 1d), "steer wraparound");
        Equal(110d, NavMath.DriftCorrectedHeadingDegrees(100d, 90d, 80d), "left drift correction");
        Equal(90d, NavMath.DriftCorrectedHeadingDegrees(100d, 90d, 100d), "right drift correction");
        Equal(30d, NavMath.EtaSeconds(1852d, 1852d / 30d), "eta from closure");
        True(double.IsNaN(NavMath.EtaSeconds(1852d, 0.5d)), "no useful closure");

        if (_failures > 0) Environment.Exit(1);
        Console.WriteLine("NavMathHarness: 18 passed");
    }

    private static void Equal(double expected, double actual, string name)
    {
        if (Math.Abs(expected - actual) <= 0.001d) return;
        Console.Error.WriteLine($"FAIL {name}: expected {expected}, actual {actual}");
        _failures++;
    }

    private static void True(bool value, string name)
    {
        if (value) return;
        Console.Error.WriteLine($"FAIL {name}");
        _failures++;
    }
}
