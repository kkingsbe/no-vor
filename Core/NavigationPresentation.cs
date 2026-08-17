using System;

namespace NOVor.Core
{
    public enum NavigationDisplayUnits
    {
        Aviation,
        Metric
    }

    public static class NavigationPresentation
    {
        private const float KilometersPerNauticalMile = 1.852f;

        public static string FormatDistance(float nauticalMiles, NavigationDisplayUnits units)
        {
            if (units == NavigationDisplayUnits.Metric)
                return (nauticalMiles * KilometersPerNauticalMile).ToString("0.0") + " km";
            return nauticalMiles.ToString("0.0") + " NM";
        }

        public static string FormatSpeed(float knots, NavigationDisplayUnits units)
        {
            if (units == NavigationDisplayUnits.Metric)
                return Math.Round(knots * KilometersPerNauticalMile).ToString("0") + " km/h";
            return Math.Round(knots).ToString("0") + " KT";
        }

        public static string FormatScaleDistance(float nauticalMiles, NavigationDisplayUnits units)
        {
            if (units == NavigationDisplayUnits.Metric)
                return (nauticalMiles * KilometersPerNauticalMile).ToString("0.#") + " km";
            return nauticalMiles.ToString("0.#") + " NM";
        }

        public static string ToFromLabel(bool toStation)
        {
            return toStation ? "TO" : "FROM";
        }
    }
}
