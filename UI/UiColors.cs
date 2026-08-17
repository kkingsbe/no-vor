using UnityEngine;

namespace NOVor.UI
{
    internal static class UiColors
    {
        public static readonly Color Chrome = Hex(0x101312);
        public static readonly Color ChromeRaised = Hex(0x191d1c);
        public static readonly Color InstrumentWell = Hex(0x080b0a);
        public static readonly Color RowSurface = Hex(0x070a09);
        public static readonly Color Rule = Hex(0x3b403e);
        public static readonly Color Amber = Hex(0xe3a64b);
        public static readonly Color AmberDim = Hex(0x78562c, 0.72f);
        public static readonly Color SelectionSurface = Hex(0x342b1b);
        public static readonly Color PanelText = Hex(0xd8ddd9);
        public static readonly Color PanelSecondaryText = Hex(0xaeb6b1);
        public static readonly Color PanelDisabledText = Hex(0x727b76);
        public static readonly Color PanelInteractive = Hex(0xc8cfca);
        public static readonly Color PanelMuted = PanelSecondaryText;
        public static readonly Color PanelFaint = PanelDisabledText;
        public static readonly Color OnAmber = Hex(0x17120a);
        public static readonly Color Transparent = new Color(0f, 0f, 0f, 0f);

        public static readonly Color FactionFriendly = Hex(0x4f8fe0);
        public static readonly Color FactionRail = Hex(0x4f8fe0, 0.72f);
        public static readonly Color FactionEnemy = Hex(0xe0554f);
        public static readonly Color FactionUnknown = Hex(0x8b928e);
        public static readonly Color MovAccent = Hex(0x56c8d6);

        public static readonly Color HudGreen = Hex(0x33ff99);
        public static readonly Color HudGreenDim = Hex(0x1a8a55, 0.55f);
        public static readonly Color HudAmber = Hex(0xffb347);
        public static readonly Color TextPrimary = Hex(0xccffdd);
        public static readonly Color TextSecondary = Hex(0x9ac8a8);
        public static readonly Color TextMuted = Hex(0x6f9c80);
        public static readonly Color HudContext = Hex(0xb6dfc2);
        public static readonly Color HudSupport = Hex(0x8fb99b);

        private static Color Hex(int rgb, float alpha = 1f)
        {
            float r = ((rgb >> 16) & 0xFF) / 255f;
            float g = ((rgb >> 8) & 0xFF) / 255f;
            float b = (rgb & 0xFF) / 255f;
            return new Color(r, g, b, alpha);
        }
    }
}
