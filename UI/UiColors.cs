using UnityEngine;

namespace NOVor.UI
{
    internal static class UiColors
    {
        public static readonly Color Chrome = Hex(0x101312, 0.55f);
        public static readonly Color ChromeRaised = Hex(0x191d1c, 0.84f);
        public static readonly Color InstrumentWell = Hex(0x080b0a, 0.40f);
        public static readonly Color RowSurface = Hex(0x070a09, 0.45f);
        public static readonly Color Rule = Hex(0x3b403e);
        public static readonly Color Amber = Hex(0xe3a64b);
        public static readonly Color AmberDim = Hex(0x78562c, 0.72f);
        public static readonly Color SelectionFill = Hex(0x8b541d, 0.86f);
        public static readonly Color PanelText = Hex(0xd8ddd9);
        public static readonly Color PanelMuted = Hex(0x8b928e);
        public static readonly Color OnAmber = Hex(0x17120a);
        public static readonly Color Transparent = new Color(0f, 0f, 0f, 0f);

        public static readonly Color HudGreen = Hex(0x33ff99);
        public static readonly Color HudGreenDim = Hex(0x1a8a55, 0.55f);
        public static readonly Color HudAmber = Hex(0xffb347);
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
