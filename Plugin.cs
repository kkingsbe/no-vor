using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;

namespace NOVor
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;
        internal static Plugin Instance { get; private set; }

        internal static ConfigEntry<KeyboardShortcut> NextAirportKey;
        internal static ConfigEntry<KeyboardShortcut> PrevAirportKey;
        internal static ConfigEntry<KeyboardShortcut> ToggleHudKey;
        internal static ConfigEntry<KeyboardShortcut> ToggleMenuKey;
        internal static ConfigEntry<float> FullDeflectionDeg;
        internal static ConfigEntry<float> HudOffsetX;
        internal static ConfigEntry<float> HudOffsetY;
        internal static ConfigEntry<float> HudNudgeStep;
        internal static ConfigEntry<KeyboardShortcut> HudNudgeUpKey;
        internal static ConfigEntry<KeyboardShortcut> HudNudgeDownKey;
        internal static ConfigEntry<KeyboardShortcut> HudNudgeLeftKey;
        internal static ConfigEntry<KeyboardShortcut> HudNudgeRightKey;
        internal static ConfigEntry<KeyboardShortcut> CourseDecreaseKey;
        internal static ConfigEntry<KeyboardShortcut> CourseIncreaseKey;
        internal static ConfigEntry<bool> CourseModeManual;
        internal static ConfigEntry<float> DefaultManualCourse;
        internal static ConfigEntry<float> CourseStep;
        internal static ConfigEntry<float> PanelX;
        internal static ConfigEntry<float> PanelY;

        private GameObject _controller;

        private void Awake()
        {
            if (Instance != null)
            {
                Log?.LogWarning("NOVor duplicate instance detected, destroying");
                Destroy(gameObject);
                return;
            }
            Instance = this;
            Log = Logger;

            NextAirportKey = Config.Bind("Hotkeys", "NextAirport", new KeyboardShortcut(KeyCode.N),
                "Cycle to the next airport.");
            PrevAirportKey = Config.Bind("Hotkeys", "PrevAirport", new KeyboardShortcut(KeyCode.B),
                "Cycle to the previous airport.");
            ToggleHudKey = Config.Bind("Hotkeys", "ToggleHud", new KeyboardShortcut(KeyCode.C),
                "Show or hide the CDI HUD instrument.");
            ToggleMenuKey = Config.Bind("Hotkeys", "ToggleMenu", new KeyboardShortcut(KeyCode.F9),
                "Open or close the airport selection list.");
            CourseDecreaseKey = Config.Bind("Hotkeys", "CourseDecrease", new KeyboardShortcut(KeyCode.LeftBracket),
                "Decrease the manual course (switches to manual course mode).");
            CourseIncreaseKey = Config.Bind("Hotkeys", "CourseIncrease", new KeyboardShortcut(KeyCode.RightBracket),
                "Increase the manual course (switches to manual course mode).");

            CourseModeManual = Config.Bind("Navigation", "ManualCourseByDefault", false,
                "Start in manual course mode instead of automatic direct-to-airport.");
            DefaultManualCourse = Config.Bind("Navigation", "DefaultManualCourse", 90f,
                new ConfigDescription("Initial manual course in degrees (0-359).",
                    new AcceptableValueRange<float>(0f, 359f)));
            CourseStep = Config.Bind("Navigation", "CourseStep", 1f,
                new ConfigDescription("Course change per key press, in degrees.",
                    new AcceptableValueRange<float>(1f, 30f)));

            FullDeflectionDeg = Config.Bind("Navigation", "FullDeflectionDegrees", 10f,
                new ConfigDescription("Degrees of course deviation that moves the CDI needle to full deflection.",
                    new AcceptableValueRange<float>(1f, 90f)));
            HudOffsetX = Config.Bind("Hud", "OffsetX", 0f,
                new ConfigDescription("Horizontal offset of the CDI instrument from HUD center (screen px).",
                    new AcceptableValueRange<float>(-800f, 800f)));
            HudOffsetY = Config.Bind("Hud", "OffsetY", 180f,
                new ConfigDescription("Vertical offset of the CDI instrument from HUD center (screen px).",
                    new AcceptableValueRange<float>(-800f, 800f)));
            HudNudgeStep = Config.Bind("Hud", "NudgeStep", 20f,
                new ConfigDescription("Pixels the CDI instrument moves per nudge hotkey press.",
                    new AcceptableValueRange<float>(1f, 200f)));
            HudNudgeUpKey = Config.Bind("Hotkeys", "HudNudgeUp", new KeyboardShortcut(KeyCode.UpArrow, KeyCode.LeftControl),
                "Move the CDI instrument up (persisted to Hud OffsetY).");
            HudNudgeDownKey = Config.Bind("Hotkeys", "HudNudgeDown", new KeyboardShortcut(KeyCode.DownArrow, KeyCode.LeftControl),
                "Move the CDI instrument down (persisted to Hud OffsetY).");
            HudNudgeLeftKey = Config.Bind("Hotkeys", "HudNudgeLeft", new KeyboardShortcut(KeyCode.LeftArrow, KeyCode.LeftControl),
                "Move the CDI instrument left (persisted to Hud OffsetX).");
            HudNudgeRightKey = Config.Bind("Hotkeys", "HudNudgeRight", new KeyboardShortcut(KeyCode.RightArrow, KeyCode.LeftControl),
                "Move the CDI instrument right (persisted to Hud OffsetX).");

            PanelX = Config.Bind("Panel", "PanelX", -620f,
                new ConfigDescription("Horizontal position of the nav panel (anchored px from screen center).",
                    new AcceptableValueRange<float>(-2000f, 2000f)));
            PanelY = Config.Bind("Panel", "PanelY", 0f,
                new ConfigDescription("Vertical position of the nav panel (anchored px from screen center).",
                    new AcceptableValueRange<float>(-2000f, 2000f)));

            _controller = new GameObject("NOVorController");
            _controller.AddComponent<NavController>();
            DontDestroyOnLoad(_controller);

            Log.LogInfo($"{MyPluginInfo.PLUGIN_NAME} v{MyPluginInfo.PLUGIN_VERSION} loaded.");
        }

        private void OnDestroy()
        {
            if (_controller != null) Destroy(_controller);
            Log?.LogInfo("NOVor shut down.");
        }
    }
}
