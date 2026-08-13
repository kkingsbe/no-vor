using UnityEngine;
using UnityEngine.UI;
using NOVor.Core;

namespace NOVor.UI
{
    public class HeadingTapeCues : MonoBehaviour
    {
        private RawImage _compass;
        private Text _courseCue;
        private Text _steeringCue;

        public void Initialize(RawImage compass)
        {
            _compass = compass;
            Build();
        }

        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }

        public void SetData(CdiData data)
        {
            if (!isActiveAndEnabled || _compass == null) return;

            float visibleDegrees = Mathf.Abs(_compass.uvRect.width) * 360f;
            if (visibleDegrees < 1f) visibleDegrees = 90f;
            float halfSpan = visibleDegrees * 0.5f;
            float courseDelta = (float)NavMath.DeltaAngleDegrees(data.Heading, data.Course);
            float steerDelta = (float)NavMath.DeltaAngleDegrees(data.Heading, data.SteerHeading);

            SetCue(_courseCue, courseDelta, halfSpan, "▽", "◁", "▷");
            SetCue(_steeringCue, steerDelta, halfSpan, "◇", "←", "→");
        }

        private void SetCue(Text cue, float delta, float halfSpan, string glyph, string left, string right)
        {
            float halfWidth = _compass.rectTransform.rect.width * 0.5f;
            float x = Mathf.Clamp(delta / halfSpan, -1f, 1f) * halfWidth;
            cue.rectTransform.anchoredPosition = new Vector2(x, cue.rectTransform.anchoredPosition.y);
            cue.text = delta < -halfSpan ? left : delta > halfSpan ? right : glyph;
        }

        private void Build()
        {
            var rt = (RectTransform)transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);

            float quarterHeight = _compass.rectTransform.rect.height * 0.25f;
            _courseCue = MakeCue("NOVorCourseCue", "▽", UiColors.HudGreen, quarterHeight);
            _steeringCue = MakeCue("NOVorSteeringCue", "◇", UiColors.HudAmber, -quarterHeight);
        }

        private Text MakeCue(string name, string glyph, Color color, float y)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            var rt = (RectTransform)go.transform;
            rt.SetParent(transform, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, y);
            rt.sizeDelta = new Vector2(24f, 20f);

            var text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.text = glyph;
            text.color = color;
            text.fontSize = 15;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;

            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.95f);
            outline.effectDistance = new Vector2(1f, -1f);
            outline.useGraphicAlpha = true;
            return text;
        }
    }
}
