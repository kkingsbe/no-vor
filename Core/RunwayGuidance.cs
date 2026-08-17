using System;

namespace NOVor.Core
{
    public enum RunwayGuidancePhase
    {
        None,
        Intercept,
        Capture,
        Established,
        Passed
    }

    public struct RunwayGuidanceInput
    {
        public double CourseDegrees;
        public double CrossTrackNm;
        public double AlongTrackToThresholdNm;
        public double GroundSpeedKnots;
        public double HeadingDegrees;
        public double GroundTrackDegrees;
        public double MaxInterceptDegrees;
        public bool WasEstablished;
    }

    public struct RunwayGuidanceOutput
    {
        public double DesiredTrackDegrees;
        public double CommandHeadingDegrees;
        public double RolloutDistanceNm;
        public RunwayGuidancePhase Phase;
    }

    public static class RunwayGuidance
    {
        public const double FinalGateDistanceNm = 3d;
        private const double RolloutSeconds = 8d;
        private const double MinimumRolloutNm = 0.15d;
        private const double MaximumRolloutNm = 1d;
        private const double EstablishCrossTrackNm = 0.05d;
        private const double ReleaseCrossTrackNm = 0.1d;
        private const double EstablishTrackErrorDegrees = 5d;
        private const double ReleaseTrackErrorDegrees = 10d;

        public static RunwayGuidanceOutput Evaluate(RunwayGuidanceInput input)
        {
            double trackError = Math.Abs(NavMath.DeltaAngleDegrees(
                input.CourseDegrees, input.GroundTrackDegrees));
            bool established = input.WasEstablished
                ? Math.Abs(input.CrossTrackNm) <= ReleaseCrossTrackNm &&
                    trackError <= ReleaseTrackErrorDegrees
                : Math.Abs(input.CrossTrackNm) <= EstablishCrossTrackNm &&
                    trackError <= EstablishTrackErrorDegrees;

            double speedNmPerSecond = Math.Max(0d, input.GroundSpeedKnots) / 3600d;
            double rolloutNm = Clamp(speedNmPerSecond * RolloutSeconds,
                MinimumRolloutNm, MaximumRolloutNm);
            double magnitude = Math.Abs(input.CrossTrackNm);
            double availableNm = input.AlongTrackToThresholdNm - FinalGateDistanceNm;
            double requiredAngle = availableNm > 0.05d
                ? Math.Atan2(magnitude, availableNm) * 180d / Math.PI
                : input.MaxInterceptDegrees;
            double interceptAngle = Math.Min(input.MaxInterceptDegrees, requiredAngle);

            RunwayGuidancePhase phase;
            if (input.AlongTrackToThresholdNm < 0d)
            {
                phase = RunwayGuidancePhase.Passed;
                interceptAngle = 0d;
            }
            else if (established)
            {
                phase = RunwayGuidancePhase.Established;
                interceptAngle = 0d;
            }
            else if (magnitude <= rolloutNm)
            {
                phase = RunwayGuidancePhase.Capture;
                interceptAngle *= magnitude / rolloutNm;
            }
            else
            {
                phase = RunwayGuidancePhase.Intercept;
            }

            double desiredTrack = NavMath.NormalizeDegrees(input.CrossTrackNm > 0d
                ? input.CourseDegrees - interceptAngle
                : input.CourseDegrees + interceptAngle);
            return new RunwayGuidanceOutput
            {
                DesiredTrackDegrees = desiredTrack,
                CommandHeadingDegrees = NavMath.DriftCorrectedHeadingDegrees(desiredTrack,
                    input.HeadingDegrees, input.GroundTrackDegrees),
                RolloutDistanceNm = rolloutNm,
                Phase = phase
            };
        }

        private static double Clamp(double value, double minimum, double maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }
    }
}
