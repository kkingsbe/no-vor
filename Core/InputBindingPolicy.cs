using System;

namespace NOVor.Core
{
    public static class InputBindingPolicy
    {
        private const string JoystickPrefix = "Joystick";
        private const string ButtonMarker = "Button";

        public static bool IsJoystickButtonName(string name)
        {
            if (string.IsNullOrEmpty(name) ||
                !name.StartsWith(JoystickPrefix, StringComparison.Ordinal))
                return false;

            int marker = name.IndexOf(ButtonMarker, JoystickPrefix.Length,
                StringComparison.Ordinal);
            if (marker < JoystickPrefix.Length) return false;

            int buttonStart = marker + ButtonMarker.Length;
            if (buttonStart >= name.Length) return false;
            for (int i = buttonStart; i < name.Length; i++)
                if (!char.IsDigit(name[i])) return false;
            return true;
        }

        public static bool IsDeviceSpecificJoystickButtonName(string name)
        {
            return IsJoystickButtonName(name) && name.Length > JoystickPrefix.Length &&
                   char.IsDigit(name[JoystickPrefix.Length]);
        }

        public static int CapturePreference(string name)
        {
            if (IsDeviceSpecificJoystickButtonName(name)) return 2;
            return IsJoystickButtonName(name) ? 1 : 0;
        }
    }
}
