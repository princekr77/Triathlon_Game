#if UNITY_EDITOR
namespace SuperHorizon.xRulerPro
{
    /// <summary>
    /// Handles unit conversion from Unity meters to various units.
    /// </summary>
    public static class UnitConverter
    {
        // Conversion factors from Unity meters
        private const float METERS_TO_MILLIMETERS = 1000f;
        private const float METERS_TO_CENTIMETERS = 100f;
        private const float METERS_TO_KILOMETERS = 0.001f;
        private const float METERS_TO_FEET = 3.28084f;
        private const float METERS_TO_INCHES = 39.3701f;

        /// <summary>
        /// Convert Unity meters value to target unit.
        /// </summary>
        public static float ConvertUnits(float unityMetersValue, UnitType unit)
        {
            switch (unit)
            {
                case UnitType.Millimeters:
                    return unityMetersValue * METERS_TO_MILLIMETERS;
                case UnitType.Centimeters:
                    return unityMetersValue * METERS_TO_CENTIMETERS;
                case UnitType.Meters:
                    return unityMetersValue;
                case UnitType.Kilometers:
                    return unityMetersValue * METERS_TO_KILOMETERS;
                case UnitType.Feet:
                    return unityMetersValue * METERS_TO_FEET;
                case UnitType.Inches:
                    return unityMetersValue * METERS_TO_INCHES;
                default:
                    return unityMetersValue;
            }
        }

        /// <summary>
        /// Format measurement value with appropriate unit suffix.
        /// </summary>
        public static string FormatMeasurement(float value, UnitType unit)
        {
            float convertedValue = ConvertUnits(value, unit);
            string unitSuffix = GetUnitSuffix(unit);
            
            // Format with appropriate precision
            if (unit == UnitType.Kilometers)
            {
                return $"{convertedValue:F4} {unitSuffix}";
            }
            else if (unit == UnitType.Millimeters)
            {
                return $"{convertedValue:F1} {unitSuffix}";
            }
            else
            {
                return $"{convertedValue:F2} {unitSuffix}";
            }
        }

        /// <summary>
        /// Get unit suffix string.
        /// </summary>
        private static string GetUnitSuffix(UnitType unit)
        {
            switch (unit)
            {
                case UnitType.Millimeters:
                    return "mm";
                case UnitType.Centimeters:
                    return "cm";
                case UnitType.Meters:
                    return "m";
                case UnitType.Kilometers:
                    return "km";
                case UnitType.Feet:
                    return "ft";
                case UnitType.Inches:
                    return "in";
                default:
                    return "m";
            }
        }
    }
}
#endif