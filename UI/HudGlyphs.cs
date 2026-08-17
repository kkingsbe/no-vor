using UnityEngine;
using UnityEngine.UI;

namespace NOVor.UI
{
    internal static class HudGlyphs
    {
        public const string OffScaleLeft = "◀";
        public const string OffScaleRight = "▶";
        private static Font _font;

        public static Font Font
        {
            get
            {
                if (_font != null) return _font;
                _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                    ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
                return _font;
            }
        }

        public static Text MakeText(string name, Color color, int size, FontStyle style)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            var text = go.GetComponent<Text>();
            text.font = Font;
            text.color = color;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            AddOutline(text);
            return text;
        }

        public static Text MakeCue(RectTransform parent, string name, string glyph, Color color,
            Vector2 position, int fontSize)
        {
            var text = MakeText(name, color, fontSize, FontStyle.Bold);
            text.rectTransform.SetParent(parent, false);
            text.text = glyph;
            Place(text.rectTransform, position, new Vector2(24f, 24f));
            return text;
        }

        public static RectTransform MakeRect(string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            AddOutline(image);
            return (RectTransform)go.transform;
        }

        public static void Place(RectTransform rt, Vector2 position, Vector2 size)
        {
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
        }

        public static void AddOutline(Graphic graphic)
        {
            var outline = graphic.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.95f);
            outline.effectDistance = new Vector2(1f, -1f);
            outline.useGraphicAlpha = true;
        }
    }
}
