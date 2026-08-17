namespace NOVor.Core
{
    public class CdiData
    {
        public float Heading;
        public float GroundTrack;
        public float Bearing;
        public float Course;
        public float CrossTrackNm;
        public float FullScaleNm;
        public float CommandHeading;
        public float CommandError;
        public float Deflection;
        public float DistanceNm;
        public float GroundSpeedKnots;
        public float EtaSeconds;
        public string AirportName;
        public string RunwayLabel;
        public CourseMode Mode;
        public CdiScaleMode ScaleMode;
        public RunwayGuidancePhase RunwayPhase;
        public int Side;
        public bool ToStation;
        public bool OffScale;
        public bool HasEta;
        public bool HasRunway;
        public float AlongTrackToThresholdNm;
    }
}
