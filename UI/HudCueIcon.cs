using UnityEngine;

namespace NOVor.UI
{
    internal sealed class HudCueIcon
    {
        public RectTransform Rect { get; }

        private HudCueIcon(RectTransform rect)
        {
            Rect = rect;
        }

        public static HudCueIcon CreateCourse(RectTransform parent)
        {
            RectTransform root = MakeRoot(parent, "NOVorCourseCue");
            AddStroke(root, "Top", new Vector2(10f, 2f), new Vector2(0f, 4f), 0f, UiColors.HudGreen);
            AddStroke(root, "Left", new Vector2(10f, 2f), new Vector2(-2.5f, 0f), -58f,
                UiColors.HudGreen);
            AddStroke(root, "Right", new Vector2(10f, 2f), new Vector2(2.5f, 0f), 58f,
                UiColors.HudGreen);
            return new HudCueIcon(root);
        }

        public static HudCueIcon CreateCommand(RectTransform parent)
        {
            RectTransform root = MakeRoot(parent, "NOVorCommandCue");
            RectTransform diamond = HudGlyphs.MakeRect("Diamond", UiColors.HudAmber);
            diamond.SetParent(root, false);
            HudGlyphs.Place(diamond, Vector2.zero, new Vector2(8f, 8f));
            diamond.localEulerAngles = new Vector3(0f, 0f, 45f);
            return new HudCueIcon(root);
        }

        private static RectTransform MakeRoot(RectTransform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            HudGlyphs.Place(rect, Vector2.zero, new Vector2(20f, 20f));
            return rect;
        }

        private static void AddStroke(RectTransform parent, string name, Vector2 size,
            Vector2 position, float rotation, Color color)
        {
            RectTransform stroke = HudGlyphs.MakeRect(name, color);
            stroke.SetParent(parent, false);
            HudGlyphs.Place(stroke, position, size);
            stroke.localEulerAngles = new Vector3(0f, 0f, rotation);
        }
    }
}
