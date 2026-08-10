using UnityEngine;

namespace NOVor.UI
{
    internal static class UiColors
    {
        // Green-phosphor HUD palette, matching Nuclear Option's cockpit HUD.
        public static readonly Color BgPanel = Hex(0x070d09, 0.98f);
        public static readonly Color BgPanelRaised = Hex(0x0e1a12, 0.95f);
        public static readonly Color BorderSubtle = Hex(0x14301f);
        public static readonly Color BorderPanel = Hex(0x1f4d33);
        public static readonly Color HudGreen = Hex(0x33ff99);
        public static readonly Color HudGreenDim = Hex(0x1a8a55, 0.55f);
        public static readonly Color HudAmber = Hex(0xffb347);
        public static readonly Color HudAmberDim = Hex(0x8a5a1a, 0.55f);
        public static readonly Color RowScrim = Hex(0x0a140d, 0.8f);
        public static readonly Color RowSelected = Hex(0x1a8a55, 0.8f);
        // Primary actions sit on a slightly raised fill; secondary steppers stay ghosted.
        public static readonly Color ButtonPrimary = Hex(0x1c4430);
        public static readonly Color ButtonGhost = Hex(0x0a120d, 0.6f);
        // Dark text drawn on top of bright accent fills (active segment, TO/FR flag).
        public static readonly Color OnAccent = Hex(0x06120c);
        public static readonly Color TextPrimary = Hex(0xccffdd);
        public static readonly Color TextSecondary = Hex(0x9ac8a8);
        public static readonly Color TextMuted = Hex(0x6f9c80);

        private static Color Hex(int rgb, float alpha = 1f)
        {
            float r = ((rgb >> 16) & 0xFF) / 255f;
            float g = ((rgb >> 8) & 0xFF) / 255f;
            float b = (rgb & 0xFF) / 255f;
            return new Color(r, g, b, alpha);
        }
    }
}
