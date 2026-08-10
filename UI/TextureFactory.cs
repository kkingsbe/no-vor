using UnityEngine;

namespace NOVor.UI
{
    internal static class TextureFactory
    {
        private static Texture2D _cachedPanelBg;
        private static Sprite _fadeTopSprite;
        private static Sprite _fadeBottomSprite;

        // 1xN vertical gradient sprite: opaque dark scrim at one edge, transparent at the other.
        // Used to soften the airport list edges when content overflows the viewport.
        public static Sprite CreateFadeSprite(bool opaqueAtTop)
        {
            var cached = opaqueAtTop ? _fadeTopSprite : _fadeBottomSprite;
            if (cached != null) return cached;

            const int h = 32;
            var tex = new Texture2D(1, h, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color[h];
            for (int y = 0; y < h; y++)
            {
                float t = (float)y / (h - 1); // 0 at texture bottom, 1 at top
                float a = opaqueAtTop ? t : 1f - t;
                pixels[y] = new Color(0.02f, 0.06f, 0.035f, a);
            }
            tex.SetPixels(pixels);
            tex.Apply();
            var sprite = Sprite.Create(tex, new Rect(0f, 0f, 1f, h), new Vector2(0.5f, 0.5f));
            if (opaqueAtTop) _fadeTopSprite = sprite; else _fadeBottomSprite = sprite;
            return sprite;
        }

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
