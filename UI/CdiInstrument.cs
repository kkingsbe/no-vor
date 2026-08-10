using UnityEngine;
using UnityEngine.UI;
using NOVor.Core;

namespace NOVor.UI
{
    public class CdiInstrument : MonoBehaviour
    {
        private const float NeedleTravelPx = 80f;
        private const float ScaleHalfWidthPx = 90f;

        private Text _dataText;
        private Text _subText;
        private Text _steerLeft;
        private Text _steerRight;
        private RectTransform _needle;

        public void ApplyOffsets(float x, float y)
        {
            var rt = (RectTransform)transform;
            rt.anchoredPosition = new Vector2(x, y);
        }

        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }

        public void SetData(CdiData data, int index, int count)
        {
            if (!isActiveAndEnabled) return;

            string modeTag = data.Mode == CourseMode.Manual ? "MAN" : "AUTO";
            _dataText.text =
                $"{data.AirportName}  [{index + 1}/{count}]  {modeTag} · " +
                $"BRG {Mathf.RoundToInt(data.Bearing):000}° {data.DistanceKm:F1}km · " +
                $"CRS {Mathf.RoundToInt(data.Course):000}° {(data.ToStation ? "TO" : "FR")}";
            _subText.text =
                $"HDG {Mathf.RoundToInt(data.Heading):000}° · " +
                $"DEV {Mathf.RoundToInt(data.Deviation):+#;-#;0}°";

            if (_needle != null)
                _needle.anchoredPosition = new Vector2(-data.Deflection * NeedleTravelPx, 0f);

            // Steer arrows light up amber at near-full deflection, toward the needle side.
            bool steerLeft = data.Deflection >= 0.95f;
            bool steerRight = data.Deflection <= -0.95f;
            if (_steerLeft != null)
                _steerLeft.color = steerLeft ? UiColors.HudAmber : UiColors.TextMuted;
            if (_steerRight != null)
                _steerRight.color = steerRight ? UiColors.HudAmber : UiColors.TextMuted;
        }

        private void Awake()
        {
            Build();
        }

        private void Build()
        {
            var rt = (RectTransform)transform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(360f, 90f);

            var data = MakeText("DataLine", UiColors.HudGreen, 14, FontStyle.Bold);
            data.SetParent(rt, false);
            data.anchorMin = new Vector2(0.5f, 0.5f);
            data.anchorMax = new Vector2(0.5f, 0.5f);
            data.pivot = new Vector2(0.5f, 0.5f);
            data.anchoredPosition = new Vector2(0f, 34f);
            data.sizeDelta = new Vector2(360f, 20f);
            _dataText = data.GetComponent<Text>();

            var sub = MakeText("SubLine", UiColors.TextMuted, 11, FontStyle.Normal);
            sub.SetParent(rt, false);
            sub.anchorMin = new Vector2(0.5f, 0.5f);
            sub.anchorMax = new Vector2(0.5f, 0.5f);
            sub.pivot = new Vector2(0.5f, 0.5f);
            sub.anchoredPosition = new Vector2(0f, -26f);
            sub.sizeDelta = new Vector2(360f, 16f);
            _subText = sub.GetComponent<Text>();

            BuildScale(rt);
            SetVisible(false);
        }

        private void BuildScale(RectTransform parent)
        {
            var scale = new GameObject("Scale", typeof(RectTransform));
            var scaleRt = (RectTransform)scale.transform;
            scaleRt.SetParent(parent, false);
            scaleRt.anchorMin = new Vector2(0.5f, 0.5f);
            scaleRt.anchorMax = new Vector2(0.5f, 0.5f);
            scaleRt.pivot = new Vector2(0.5f, 0.5f);
            scaleRt.anchoredPosition = new Vector2(0f, 2f);
            scaleRt.sizeDelta = new Vector2(ScaleHalfWidthPx * 2f + 40f, 24f);

            float[] ticks = { -1f, -0.5f, 0.5f, 1f };
            foreach (var t in ticks)
            {
                var tick = MakeRect("Tick", UiColors.HudGreen);
                tick.SetParent(scaleRt, false);
                tick.anchorMin = new Vector2(0.5f, 0.5f);
                tick.anchorMax = new Vector2(0.5f, 0.5f);
                tick.pivot = new Vector2(0.5f, 0.5f);
                tick.sizeDelta = new Vector2(2f, 8f);
                tick.anchoredPosition = new Vector2(t * ScaleHalfWidthPx, 0f);
            }

            // Center marker: taller, brighter caret marking the selected course.
            var center = MakeRect("CenterMarker", UiColors.HudGreen);
            center.SetParent(scaleRt, false);
            center.anchorMin = new Vector2(0.5f, 0.5f);
            center.anchorMax = new Vector2(0.5f, 0.5f);
            center.pivot = new Vector2(0.5f, 0.5f);
            center.sizeDelta = new Vector2(3f, 14f);
            center.anchoredPosition = Vector2.zero;

            var needle = MakeRect("Needle", UiColors.HudAmber);
            _needle = needle;
            needle.SetParent(scaleRt, false);
            needle.anchorMin = new Vector2(0.5f, 0.5f);
            needle.anchorMax = new Vector2(0.5f, 0.5f);
            needle.pivot = new Vector2(0.5f, 0.5f);
            needle.sizeDelta = new Vector2(3f, 22f);
            needle.anchoredPosition = Vector2.zero;

            _steerLeft = MakeArrow(scaleRt, "SteerLeft", "<", -(ScaleHalfWidthPx + 16f));
            _steerRight = MakeArrow(scaleRt, "SteerRight", ">", ScaleHalfWidthPx + 16f);
        }

        private static Text MakeArrow(RectTransform parent, string name, string glyph, float x)
        {
            var rt = MakeText(name, UiColors.TextMuted, 16, FontStyle.Bold);
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(20f, 22f);
            rt.anchoredPosition = new Vector2(x, 0f);
            var text = rt.GetComponent<Text>();
            text.text = glyph;
            return text;
        }

        private static RectTransform MakeRect(string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return (RectTransform)go.transform;
        }

        private static RectTransform MakeText(string name, Color color, int size, FontStyle style)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            var text = go.GetComponent<Text>();
            text.font = GetDefaultFont();
            text.color = color;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return (RectTransform)go.transform;
        }

        private static Font _font;
        private static Font GetDefaultFont()
        {
            if (_font != null) return _font;
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (_font == null) _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return _font;
        }
    }
}
