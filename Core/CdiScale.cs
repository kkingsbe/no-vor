using System;

namespace NOVor.Core
{
    public enum CdiScaleMode
    {
        Angular,
        Fixed
    }

    public struct CdiDeviation
    {
        public CdiScaleMode Mode;
        public double FullScaleNm;
        public double Deflection;
        public bool OffScale;
        public int Side;
        public double InterceptHeading;
    }

    public static class CdiScale
    {
        // Conventional VOR CDI full-scale deflection is angular, not a set lateral
        // distance. This is the approximate full-scale angular displacement.
        public const double FullScaleDeflectionDegrees = 10d;
        private const double SideDeadbandNm = 0.005d;

        public static double AngularFullScaleNm(double distanceNm)
        {
            double radians = FullScaleDeflectionDegrees * Math.PI / 180d;
            return Math.Max(0d, distanceNm) * Math.Sin(radians);
        }

        public static double InterceptHeadingDegrees(double course, double crossTrackNm,
            double maxInterceptDegrees)
        {
            double magnitude = Math.Abs(crossTrackNm);
            double angle = magnitude >= 1d ? maxInterceptDegrees : magnitude * maxInterceptDegrees;
            return NavMath.NormalizeDegrees(crossTrackNm > 0d ? course - angle : course + angle);
        }

        public static CdiDeviation Evaluate(double course, double crossTrackNm, double distanceNm,
            CdiScaleMode previousMode, bool autoScale, double fixedFullScaleNm,
            double maxInterceptDegrees)
        {
            CdiScaleMode mode = autoScale ? CdiScaleMode.Angular : CdiScaleMode.Fixed;
            double fullScale = autoScale ? AngularFullScaleNm(distanceNm) : fixedFullScaleNm;
            double magnitude = Math.Abs(crossTrackNm);
            return new CdiDeviation
            {
                Mode = mode,
                FullScaleNm = fullScale,
                Deflection = NavMath.CrossTrackDeflection(crossTrackNm, fullScale),
                OffScale = fullScale > 0d && magnitude >= fullScale,
                Side = crossTrackNm > SideDeadbandNm ? 1 : crossTrackNm < -SideDeadbandNm ? -1 : 0,
                InterceptHeading = InterceptHeadingDegrees(course, crossTrackNm, maxInterceptDegrees)
            };
        }
    }
}
