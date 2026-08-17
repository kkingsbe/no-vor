using System;

namespace NOVor.Core
{
    public struct CockpitPresentationInput
    {
        public string AirportName;
        public float DistanceNm;
        public float Course;
        public float Bearing;
        public float CommandHeading;
        public float GroundSpeedKnots;
        public float EtaSeconds;
        public float FullScaleNm;
        public string RunwayLabel;
        public bool Manual;
        public bool HasRunway;
        public bool ToStation;
        public bool OffScale;
        public bool HasEta;
        public CdiScaleMode ScaleMode;
        public RunwayGuidancePhase RunwayPhase;
        public NavigationDisplayUnits Units;
    }

    public struct CockpitReadout
    {
        public string TargetLine;
        public string ContextLine;
        public string CommandLine;
        public string SupportLine;
        public string ScaleLine;
        public bool ShowCdi;
        public bool CommandAttention;
    }

    public static class CockpitPresentation
    {
        public static CockpitReadout Build(CockpitPresentationInput input)
        {
            string targetName = CompactFieldName(input.AirportName);
            if (input.HasRunway && !string.IsNullOrEmpty(input.RunwayLabel))
                targetName += "  " + input.RunwayLabel.ToUpperInvariant();
            string context = input.HasRunway
                ? "CRS " + Degrees(input.Course) + "°  ·  RWY"
                : input.Manual
                    ? "CRS " + Degrees(input.Course) + "°  ·  " + NavigationPresentation.ToFromLabel(input.ToStation)
                    : "BRG " + Degrees(input.Bearing) + "°  ·  DIRECT";
            string verb = input.HasRunway
                ? input.RunwayPhase == RunwayGuidancePhase.Intercept ||
                    input.RunwayPhase == RunwayGuidancePhase.Capture ? "INTCP" : "TRACK"
                : input.Manual ? input.OffScale ? "INTCP" : "TRACK" : "STEER";
            string command = verb + "  ◆  " + Degrees(input.CommandHeading) + "°";
            return new CockpitReadout
            {
                TargetLine = targetName + "  ·  " +
                    NavigationPresentation.FormatDistance(input.DistanceNm, input.Units),
                ContextLine = context,
                CommandLine = command,
                SupportLine = FormatEta(input) + "  ·  GS " +
                    NavigationPresentation.FormatSpeed(input.GroundSpeedKnots, input.Units),
                ScaleLine = input.Manual
                    ? ScaleTag(input.ScaleMode) + "  ·  ±" +
                        NavigationPresentation.FormatScaleDistance(input.FullScaleNm, input.Units)
                    : string.Empty,
                ShowCdi = input.Manual,
                CommandAttention = input.HasRunway
                    ? input.RunwayPhase == RunwayGuidancePhase.Intercept ||
                        input.RunwayPhase == RunwayGuidancePhase.Capture
                    : input.Manual && input.OffScale
            };
        }

        private static string FormatEta(CockpitPresentationInput input)
        {
            if (!input.HasEta || input.EtaSeconds <= 0f || input.EtaSeconds > 359940f)
                return "ETA --:--";
            int total = (int)Math.Round(input.EtaSeconds);
            return "ETA " + (total / 60).ToString("00") + ":" + (total % 60).ToString("00");
        }

        private static string Degrees(float value)
        {
            int rounded = (int)Math.Round(NavMath.NormalizeDegrees(value));
            if (rounded == 360) rounded = 0;
            return rounded.ToString("000");
        }

        private static string CompactFieldName(string value)
        {
            string compact = (value ?? "NAV").ToUpperInvariant()
                .Replace("ANNEX CLASS CARRIER", "ANNEX CV")
                .Replace("INTERNATIONAL", "INTL")
                .Replace("AIRFIELD", "")
                .Replace("AIRSTRIP", "")
                .Replace("AIRPORT", "")
                .Replace("AIRBASE", "")
                .Trim();
            while (compact.Contains("  ")) compact = compact.Replace("  ", " ");
            if (compact.Length > 12)
            {
                int cut = compact.LastIndexOf(' ', 12);
                compact = cut > 3 ? compact.Substring(0, cut) : compact.Substring(0, 12);
            }
            return compact.Length > 0 ? compact : "NAV";
        }

        private static string ScaleTag(CdiScaleMode mode)
        {
            switch (mode)
            {
                case CdiScaleMode.Angular: return "VOR";
                default: return "FIX";
            }
        }
    }
}
