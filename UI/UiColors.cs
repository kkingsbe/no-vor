using UnityEngine;

namespace NOVor.UI
{
    internal static class UiColors
    {
        public static readonly Color BgPanel = Hex(0x0e0e1a);
        public static readonly Color BgPanelRaised = Hex(0x161626);
        public static readonly Color BorderSubtle = Hex(0x1e1e3a);
        public static readonly Color BorderPanel = Hex(0x2a2a5a);
        public static readonly Color HudGreen = Hex(0x33ff99);
        public static readonly Color HudGreenDim = Hex(0x1a8a55);
        public static readonly Color HudAmber = Hex(0xffb347);
        public static readonly Color TextPrimary = Hex(0xccffdd);
        public static readonly Color TextSecondary = Hex(0x77aa88);
        public static readonly Color TextMuted = Hex(0x3a6644);

        private static Color Hex(int rgb)
        {
            float r = ((rgb >> 16) & 0xFF) / 255f;
            float g = ((rgb >> 8) & 0xFF) / 255f;
            float b = (rgb & 0xFF) / 255f;
            return new Color(r, g, b);
        }
    }
}
