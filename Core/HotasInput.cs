using System;
using System.Collections.Generic;
using Rewired;

namespace NOVor.Core
{
    internal static class HotasInput
    {
        public static bool TryCapture(out HotasBinding binding)
        {
            binding = HotasBinding.Empty;
            if (!ReInput.isReady) return false;
            IList<Joystick> sticks = ReInput.controllers.Joysticks;
            for (int i = 0; i < sticks.Count; i++)
            {
                Joystick stick = sticks[i];
                if (stick == null || !stick.enabled) continue;
                int count = stick.buttonCount;
                for (int b = 0; b < count; b++)
                {
                    if (!stick.GetButtonDown(b)) continue;
                    binding = new HotasBinding
                    {
                        DeviceName = CleanName(stick),
                        DeviceGuid = stick.deviceInstanceGuid,
                        DeviceId = stick.id,
                        ButtonIndex = b
                    };
                    return true;
                }
            }
            return false;
        }

        public static bool IsDown(string value)
        {
            HotasBinding binding;
            if (!HotasBinding.TryParse(value, out binding) || !ReInput.isReady) return false;
            Joystick stick = FindJoystick(binding);
            if (stick == null) return false;
            if (binding.ButtonIndex < 0 || binding.ButtonIndex >= stick.buttonCount) return false;
            return stick.GetButtonDown(binding.ButtonIndex);
        }

        public static bool IsBound(string value)
        {
            return HotasBinding.TryParse(value, out HotasBinding binding) && !binding.IsEmpty;
        }

        public static string Label(string value)
        {
            HotasBinding binding;
            return HotasBinding.TryParse(value, out binding) ? HotasBinding.Format(binding) : null;
        }

        private static Joystick FindJoystick(HotasBinding binding)
        {
            IList<Joystick> sticks = ReInput.controllers.Joysticks;
            if (binding.DeviceGuid != Guid.Empty)
            {
                for (int i = 0; i < sticks.Count; i++)
                {
                    Joystick stick = sticks[i];
                    if (stick != null && stick.deviceInstanceGuid == binding.DeviceGuid) return stick;
                }
            }
            for (int i = 0; i < sticks.Count; i++)
            {
                Joystick stick = sticks[i];
                if (stick != null && stick.id == binding.DeviceId) return stick;
            }
            if (!string.IsNullOrEmpty(binding.DeviceName))
            {
                for (int i = 0; i < sticks.Count; i++)
                {
                    Joystick stick = sticks[i];
                    if (stick != null &&
                        string.Equals(CleanName(stick), binding.DeviceName, StringComparison.OrdinalIgnoreCase))
                        return stick;
                }
            }
            return null;
        }

        private static string CleanName(Joystick stick)
        {
            if (!string.IsNullOrEmpty(stick.hardwareName)) return stick.hardwareName;
            if (!string.IsNullOrEmpty(stick.name)) return stick.name;
            return "JOYSTICK";
        }
    }
}