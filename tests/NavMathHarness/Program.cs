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
        Same(CdiScaleMode.Enroute, CdiScale.SelectMode(45d, CdiScaleMode.Enroute), "enroute beyond thirty miles");
        Same(CdiScaleMode.Terminal, CdiScale.SelectMode(25d, CdiScaleMode.Enroute), "terminal inside thirty miles");
        Same(CdiScaleMode.Approach, CdiScale.SelectMode(1.5d, CdiScaleMode.Terminal), "approach inside two miles");
        Same(CdiScaleMode.Terminal, CdiScale.SelectMode(31d, CdiScaleMode.Terminal), "terminal hysteresis holds");
        Same(CdiScaleMode.Enroute, CdiScale.SelectMode(33d, CdiScaleMode.Terminal), "terminal hysteresis releases");
        Same(CdiScaleMode.Approach, CdiScale.SelectMode(2.2d, CdiScaleMode.Approach), "approach hysteresis holds");
        Same(CdiScaleMode.Terminal, CdiScale.SelectMode(2.5d, CdiScaleMode.Approach), "approach hysteresis releases");
        Equal(5d, CdiScale.FullScaleNm(CdiScaleMode.Enroute, 1d), "enroute full scale");
        Equal(1d, CdiScale.FullScaleNm(CdiScaleMode.Terminal, 1d), "terminal full scale");
        Equal(0.3d, CdiScale.FullScaleNm(CdiScaleMode.Approach, 1d), "approach full scale");
        Equal(2.5d, CdiScale.FullScaleNm(CdiScaleMode.Fixed, 2.5d), "fixed full scale from config");
        Equal(45d, CdiScale.InterceptHeadingDegrees(90d, 5d, 45d), "intercept turns left when right of course");
        Equal(55d, CdiScale.InterceptHeadingDegrees(10d, -5d, 45d), "intercept turns right when left of course");
        Equal(347.5d, CdiScale.InterceptHeadingDegrees(10d, 0.5d, 45d), "intercept shallows inside one mile");
        True(!CdiScale.Evaluate(90d, 3d, 45d, CdiScaleMode.Enroute, true, 1d, 45d).OffScale,
            "three miles is on the enroute scale");
        True(CdiScale.Evaluate(90d, 21d, 23.5d, CdiScaleMode.Terminal, true, 1d, 45d).OffScale,
            "twenty one miles is off the terminal scale");
        Equal(-0.6d, CdiScale.Evaluate(90d, 3d, 45d, CdiScaleMode.Enroute, true, 1d, 45d).Deflection,
            "enroute deflection scales to five miles");
        Equal(1d, CdiScale.Evaluate(90d, -9d, 45d, CdiScaleMode.Enroute, true, 1d, 45d).Deflection,
            "left of course clamps the needle right");
        Equal(1d, CdiScale.Evaluate(90d, 3d, 45d, CdiScaleMode.Enroute, true, 1d, 45d).Side,
            "positive cross track is right of course");

        if (_failures > 0) Environment.Exit(1);
        Console.WriteLine("NavMathHarness: 36 passed");
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

    private static void Same(CdiScaleMode expected, CdiScaleMode actual, string name)
    {
        if (expected == actual) return;
        Console.Error.WriteLine($"FAIL {name}: expected {expected}, actual {actual}");
        _failures++;
    }
}
