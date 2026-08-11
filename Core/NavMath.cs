using System;

namespace NOVor.Core
{
    public static class NavMath
    {
        public static double NormalizeDegrees(double value)
        {
            value %= 360d;
            return value < 0d ? value + 360d : value;
        }

        public static double DeltaAngleDegrees(double from, double to)
        {
            double delta = NormalizeDegrees(to - from);
            return delta > 180d ? delta - 360d : delta;
        }

        public static bool IsToStation(double course, double bearingToStation)
        {
            return Math.Abs(DeltaAngleDegrees(course, bearingToStation)) <= 90d;
        }

        public static double CourseDeviationDegrees(double course, double bearingToStation)
        {
            double reference = IsToStation(course, bearingToStation) ? course : course + 180d;
            return DeltaAngleDegrees(reference, bearingToStation);
        }

        public static double SteeringDeviationDegrees(double heading, double steerHeading)
        {
            return DeltaAngleDegrees(steerHeading, heading);
        }

        public static double DriftCorrectedHeadingDegrees(double bearingToStation, double heading, double groundTrack)
        {
            double drift = DeltaAngleDegrees(heading, groundTrack);
            return NormalizeDegrees(bearingToStation - drift);
        }

        public static double EtaSeconds(double distanceMeters, double closureMetersPerSecond)
        {
            return distanceMeters >= 0d && closureMetersPerSecond > 1d
                ? distanceMeters / closureMetersPerSecond
                : double.NaN;
        }
    }
}
