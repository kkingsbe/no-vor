namespace NOVor.Core
{
    public static class GuidanceMath
    {
        public static double CommandHeadingDegrees(bool manual, double course, double bearing,
            double crossTrackNm, double maxInterceptDegrees, double heading, double groundTrack)
        {
            double desiredTrack = manual
                ? CdiScale.InterceptHeadingDegrees(course, crossTrackNm, maxInterceptDegrees)
                : NavMath.NormalizeDegrees(bearing);
            return NavMath.DriftCorrectedHeadingDegrees(desiredTrack, heading, groundTrack);
        }
    }
}
