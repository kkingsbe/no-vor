using System;

namespace NOVor.Core
{
    public enum CdiScaleMode
    {
        Enroute,
        Terminal,
        Approach,
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
        public const double EnrouteFullScaleNm = 5d;
        public const double TerminalFullScaleNm = 1d;
        public const double ApproachFullScaleNm = 0.3d;
        public const double TerminalEntryNm = 30d;
        public const double ApproachEntryNm = 2d;
        public const double TerminalHysteresisNm = 2d;
        public const double ApproachHysteresisNm = 0.3d;
        private const double SideDeadbandNm = 0.005d;

        public static CdiScaleMode SelectMode(double distanceNm, CdiScaleMode previous)
        {
            if (previous == CdiScaleMode.Approach && distanceNm <= ApproachEntryNm + ApproachHysteresisNm)
                return CdiScaleMode.Approach;
            if (previous == CdiScaleMode.Terminal && distanceNm > ApproachEntryNm
                && distanceNm <= TerminalEntryNm + TerminalHysteresisNm)
                return CdiScaleMode.Terminal;
            if (distanceNm <= ApproachEntryNm) return CdiScaleMode.Approach;
            if (distanceNm <= TerminalEntryNm) return CdiScaleMode.Terminal;
            return CdiScaleMode.Enroute;
        }

        public static double FullScaleNm(CdiScaleMode mode, double fixedFullScaleNm)
        {
            switch (mode)
            {
                case CdiScaleMode.Approach: return ApproachFullScaleNm;
                case CdiScaleMode.Terminal: return TerminalFullScaleNm;
                case CdiScaleMode.Enroute: return EnrouteFullScaleNm;
                default: return fixedFullScaleNm;
            }
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
            CdiScaleMode mode = autoScale ? SelectMode(distanceNm, previousMode) : CdiScaleMode.Fixed;
            double fullScale = FullScaleNm(mode, fixedFullScaleNm);
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
