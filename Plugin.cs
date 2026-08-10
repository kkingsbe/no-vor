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

            FullDeflectionDeg = Config.Bind("Navigation", "FullDeflectionDegrees", 10f,
                new ConfigDescription("Degrees of course deviation that moves the CDI needle to full deflection.",
                    new AcceptableValueRange<float>(1f, 90f)));
            HudOffsetX = Config.Bind("Hud", "OffsetX", 0f,
                new ConfigDescription("Horizontal offset of the CDI instrument from HUD center (screen px).",
                    new AcceptableValueRange<float>(-800f, 800f)));
            HudOffsetY = Config.Bind("Hud", "OffsetY", 240f,
                new ConfigDescription("Vertical offset of the CDI instrument from HUD center (screen px).",
                    new AcceptableValueRange<float>(-800f, 800f)));

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
