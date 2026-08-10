using UnityEngine;

namespace NOVor.UI
{
    internal static class TextureFactory
    {
        private static Texture2D _cachedPanelBg;

        public static Texture2D CreatePanelBackground(int width, int height, Color bgColor, Color borderColor, float borderWidth = 1f)
        {
            if (_cachedPanelBg == null)
            {
                var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
                tex.filterMode = FilterMode.Bilinear;
                tex.wrapMode = TextureWrapMode.Clamp;
                var pixels = new Color[width * height];
                for (int y = 0; y < height; y++)
                    for (int x = 0; x < width; x++)
                    {
                        bool onBorder = x < borderWidth || x >= width - borderWidth ||
                                        y < borderWidth || y >= height - borderWidth;
                        pixels[y * width + x] = onBorder ? borderColor : bgColor;
                    }
                tex.SetPixels(pixels);
                tex.Apply();
                _cachedPanelBg = tex;
            }
            return _cachedPanelBg;
        }
    }
}
