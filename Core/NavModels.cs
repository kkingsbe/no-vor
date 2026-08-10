namespace NOVor.Core
{
    public enum CourseMode
    {
        Auto,
        Manual
    }

    public struct AirportInfo
    {
        public string Name;
        public float Bearing;
        public float DistanceKm;
        public bool HasPosition;
        public int SourceIndex;
    }
}
