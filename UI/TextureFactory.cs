using System.Collections.Generic;
using UnityEngine;

namespace NOVor.UI
{
    internal static class TextureFactory
    {
        private static readonly Dictionary<(Color, Color, int), Sprite> FramedSprites =
            new Dictionary<(Color, Color, int), Sprite>();

        public static Sprite CreateFramedSprite(Color background, Color borderColor, int borderPixels = 1)
        {
            var key = (background, borderColor, borderPixels);
            if (FramedSprites.TryGetValue(key, out Sprite cached)) return cached;

            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool border = x < borderPixels || x >= size - borderPixels ||
                                  y < borderPixels || y >= size - borderPixels;
                    pixels[y * size + x] = border ? borderColor : background;
                }
            }
            texture.SetPixels(pixels);
            texture.Apply();
            var borders = new Vector4(borderPixels, borderPixels, borderPixels, borderPixels);
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f), 1f, 0, SpriteMeshType.FullRect, borders);
            FramedSprites[key] = sprite;
            return sprite;
        }
    }
}
