using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace NOVor.Core
{
    internal static class InputBinding
    {
        private static readonly KeyCode[] ModifierKeys =
        {
            KeyCode.LeftControl, KeyCode.RightControl,
            KeyCode.LeftShift, KeyCode.RightShift,
            KeyCode.LeftAlt, KeyCode.RightAlt
        };

        private static readonly KeyCode[] AllKeys = (KeyCode[])Enum.GetValues(typeof(KeyCode));

        public static bool IsDown(KeyboardShortcut shortcut)
        {
            KeyCode mainKey = shortcut.MainKey;
            if (mainKey == KeyCode.None) return false;
            if (!InputBindingPolicy.IsJoystickButtonName(mainKey.ToString())) return shortcut.IsDown();
            if (!Input.GetKeyDown(mainKey)) return false;
            foreach (KeyCode modifier in shortcut.Modifiers)
                if (!Input.GetKey(modifier)) return false;
            return true;
        }

        public static bool IsDown(KeyboardShortcut keyboard, string hotas)
        {
            return IsDown(keyboard) || HotasInput.IsDown(hotas);
        }

        public static bool TryCapture(out KeyboardShortcut shortcut)
        {
            KeyCode fallback = KeyCode.None;
            int fallbackPreference = -1;
            for (int i = 0; i < AllKeys.Length; i++)
            {
                KeyCode key = AllKeys[i];
                if (!IsCapturableMainKey(key) || !Input.GetKeyDown(key)) continue;
                int preference = InputBindingPolicy.CapturePreference(key.ToString());
                if (preference == 2)
                {
                    shortcut = BuildShortcut(key);
                    return true;
                }
                if (preference > fallbackPreference)
                {
                    fallback = key;
                    fallbackPreference = preference;
                }
            }
            if (fallback != KeyCode.None)
            {
                shortcut = BuildShortcut(fallback);
                return true;
            }
            shortcut = KeyboardShortcut.Empty;
            return false;
        }

        private static bool IsCapturableMainKey(KeyCode key)
        {
            if (key == KeyCode.None || key == KeyCode.Escape) return false;
            if (key >= KeyCode.Mouse0 && key <= KeyCode.Mouse6) return false;
            if (key >= KeyCode.JoystickButton0) return false;
            for (int i = 0; i < ModifierKeys.Length; i++)
                if (key == ModifierKeys[i]) return false;
            return true;
        }

        private static KeyboardShortcut BuildShortcut(KeyCode mainKey)
        {
            var modifiers = new List<KeyCode>();
            for (int i = 0; i < ModifierKeys.Length; i++)
                if (Input.GetKey(ModifierKeys[i])) modifiers.Add(ModifierKeys[i]);
            return modifiers.Count == 0 ? new KeyboardShortcut(mainKey) :
                new KeyboardShortcut(mainKey, modifiers.ToArray());
        }
    }
}
