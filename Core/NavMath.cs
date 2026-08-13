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

        public static double CrossTrackMeters(double course, double aircraftEastOfStation,
            double aircraftNorthOfStation)
        {
            double radians = course * Math.PI / 180d;
            double rightEast = Math.Cos(radians);
            double rightNorth = -Math.Sin(radians);
            return aircraftEastOfStation * rightEast + aircraftNorthOfStation * rightNorth;
        }

        public static double CrossTrackDeflection(double crossTrackMeters, double fullDeflectionMeters)
        {
            if (fullDeflectionMeters <= 0d) return 0d;
            double deflection = -crossTrackMeters / fullDeflectionMeters;
            return Math.Max(-1d, Math.Min(1d, deflection));
        }

        public static double SteeringErrorDegrees(double heading, double steerHeading)
        {
            return DeltaAngleDegrees(heading, steerHeading);
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
