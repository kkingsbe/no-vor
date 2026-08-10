using UnityEngine;

namespace NOVor.UI
{
    internal static class TextureFactory
    {
        private static Texture2D _cachedPanelBg;
        private static Texture2D _cachedScanline;

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

        public static Texture2D CreateScanlineTexture(int width, int height, float lineThickness = 2f, float lineSpacing = 4f)
        {
            if (_cachedScanline == null)
            {
                var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
                tex.filterMode = FilterMode.Point;
                tex.wrapMode = TextureWrapMode.Repeat;
                var pixels = new Color[width * height];
                var dark = new Color(0, 0, 0, 0.06f);
                var clear = new Color(0, 0, 0, 0);
                for (int y = 0; y < height; y++)
                {
                    float rowInPattern = y % (lineThickness + lineSpacing);
                    Color c = rowInPattern < lineThickness ? dark : clear;
                    for (int x = 0; x < width; x++)
                        pixels[y * width + x] = c;
                }
                tex.SetPixels(pixels);
                tex.Apply();
                _cachedScanline = tex;
            }
            return _cachedScanline;
        }
    }
}
