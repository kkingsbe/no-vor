using UnityEngine;
using UnityEngine.UI;
using NOVor.Core;

namespace NOVor.UI
{
    public class CdiInstrument : MonoBehaviour
    {
        private const float NeedleTravelPx = 80f;
        private const float ScaleHalfWidthPx = 90f;

        private static readonly Color HudGreen = new Color(0.2f, 1f, 0.6f, 0.95f);
        private static readonly Color NeedleAmber = new Color(1f, 0.85f, 0.25f, 1f);
        private static readonly Color Backdrop = new Color(0f, 0f, 0f, 0.6f);

        private Text _titleText;
        private Text _dataText;
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

            _titleText.text = $"{data.AirportName}   [{index + 1}/{count}]";
            _dataText.text =
                $"HDG {Mathf.RoundToInt(data.Heading):000}\u00b0   " +
                $"CRS {Mathf.RoundToInt(data.Course):000}\u00b0   " +
                $"BRG {Mathf.RoundToInt(data.Bearing):000}\u00b0   " +
                $"DEV {Mathf.RoundToInt(data.Deviation):+#;-#;0}\u00b0   " +
                $"D {data.DistanceKm:F1} km";

            if (_needle != null)
                _needle.anchoredPosition = new Vector2(-data.Deflection * NeedleTravelPx, 0f);
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
            rt.sizeDelta = new Vector2(440f, 150f);

            var bg = MakeRect("Bg", Backdrop);
            bg.anchorMin = new Vector2(0f, 0f);
            bg.anchorMax = new Vector2(1f, 1f);
            bg.offsetMin = Vector2.zero;
            bg.offsetMax = Vector2.zero;

            var title = MakeText("Title", HudGreen, 16, FontStyle.Bold);
            title.anchorMin = new Vector2(0.5f, 1f);
            title.anchorMax = new Vector2(0.5f, 1f);
            title.pivot = new Vector2(0.5f, 1f);
            title.anchoredPosition = new Vector2(0f, -6f);
            title.sizeDelta = new Vector2(420f, 24f);
            _titleText = title.GetComponent<Text>();

            var data = MakeText("Data", HudGreen, 14, FontStyle.Bold);
            data.anchorMin = new Vector2(0.5f, 1f);
            data.anchorMax = new Vector2(0.5f, 1f);
            data.pivot = new Vector2(0.5f, 1f);
            data.anchoredPosition = new Vector2(0f, -34f);
            data.sizeDelta = new Vector2(420f, 22f);
            _dataText = data.GetComponent<Text>();

            BuildScale(rt);
            SetVisible(false);
        }

        private void BuildScale(RectTransform parent)
        {
            var scale = MakeRect("Scale", Color.clear);
            scale.SetParent(parent, false);
            scale.anchorMin = new Vector2(0.5f, 0.5f);
            scale.anchorMax = new Vector2(0.5f, 0.5f);
            scale.pivot = new Vector2(0.5f, 0.5f);
            scale.anchoredPosition = new Vector2(0f, -46f);
            scale.sizeDelta = new Vector2(ScaleHalfWidthPx * 2f, 50f);

            float[] ticks = { -1f, -0.5f, 0f, 0.5f, 1f };
            foreach (var t in ticks)
            {
                var tick = MakeRect("Tick", HudGreen);
                tick.SetParent(scale, false);
                tick.anchorMin = new Vector2(0.5f, 0.5f);
                tick.anchorMax = new Vector2(0.5f, 0.5f);
                tick.pivot = new Vector2(0.5f, 0.5f);
                float half = Mathf.Abs(t) < 0.01f ? 6f : 10f;
                tick.sizeDelta = new Vector2(2f, half);
                tick.anchoredPosition = new Vector2(t * ScaleHalfWidthPx, 0f);
            }

            var needle = MakeRect("Needle", NeedleAmber);
            _needle = needle;
            needle.SetParent(scale, false);
            needle.anchorMin = new Vector2(0.5f, 0.5f);
            needle.anchorMax = new Vector2(0.5f, 0.5f);
            needle.pivot = new Vector2(0.5f, 0.5f);
            needle.sizeDelta = new Vector2(3f, 40f);
            needle.anchoredPosition = Vector2.zero;
        }

        private static RectTransform MakeRect(string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var img = go.GetComponent<Image>();
            img.color = color;
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
