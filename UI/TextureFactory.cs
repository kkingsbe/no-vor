using UnityEngine;

namespace NOVor.UI
{
    internal static class TextureFactory
    {
        private static Texture2D _cachedPanelBg;
        private static Sprite _fadeTopSprite;
        private static Sprite _fadeBottomSprite;
        private static Sprite _framedSprite;

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

        // 9-slice framed sprite: solid fill with a crisp border that stays thin at any
        // size. Used to give input fields a visible bezel so they read as editable.
        public static Sprite CreateFramedSprite(Color bgColor, Color borderColor, int borderPx = 2)
        {
            if (_framedSprite != null) return _framedSprite;

            const int size = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    bool onBorder = x < borderPx || x >= size - borderPx ||
                                    y < borderPx || y >= size - borderPx;
                    pixels[y * size + x] = onBorder ? borderColor : bgColor;
                }
            tex.SetPixels(pixels);
            tex.Apply();
            var border = new Vector4(borderPx, borderPx, borderPx, borderPx);
            _framedSprite = Sprite.Create(tex, new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
            return _framedSprite;
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
