using UnityEngine;

namespace NOVor.Core
{
    public enum CourseMode
    {
        Auto,
        Manual
    }

    public enum AirportSortMode
    {
        Nearest,
        Name
    }

    public struct RunwayInfo
    {
        public string Label;
        public float Heading;
        public float LengthMeters;
    }

    public struct AirportInfo
    {
        public string Name;
        public float Bearing;
        public float DistanceNm;
        public float ElevationMeters;
        public float EtaSeconds;
        public float SteerHeading;
        public float GroundSpeedKnots;
        public bool HasPosition;
        public bool HasEta;
        public bool IsFriendly;
        public bool HasFaction;
        public bool IsMobile;
        public Color FactionColor;
        public string FactionTag;
        public int SourceIndex;
        public RunwayInfo[] Runways;
    }
}
