using UnityEngine;

namespace NOVor.UI
{
    internal static class UiColors
    {
        // Green-phosphor HUD palette, matching Nuclear Option's cockpit HUD.
        public static readonly Color BgPanel = Hex(0x070d09, 0.85f);
        public static readonly Color BgPanelRaised = Hex(0x0e1a12, 0.90f);
        public static readonly Color BorderSubtle = Hex(0x14301f);
        public static readonly Color BorderPanel = Hex(0x1f4d33);
        public static readonly Color HudGreen = Hex(0x33ff99);
        public static readonly Color HudGreenDim = Hex(0x1a8a55, 0.55f);
        public static readonly Color HudAmber = Hex(0xffb347);
        public static readonly Color TextPrimary = Hex(0xccffdd);
        public static readonly Color TextSecondary = Hex(0x77aa88);
        public static readonly Color TextMuted = Hex(0x4a7a5a);

        private static Color Hex(int rgb, float alpha = 1f)
        {
            float r = ((rgb >> 16) & 0xFF) / 255f;
            float g = ((rgb >> 8) & 0xFF) / 255f;
            float b = (rgb & 0xFF) / 255f;
            return new Color(r, g, b, alpha);
        }
    }
}
