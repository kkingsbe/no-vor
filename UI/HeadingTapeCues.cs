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
            _courseCue = HudGlyphs.MakeCue(rt, "NOVorCourseCue", "▽", UiColors.HudGreen,
                new Vector2(0f, quarterHeight), 15);
            _courseCue.rectTransform.sizeDelta = new Vector2(24f, 20f);
            _steeringCue = HudGlyphs.MakeCue(rt, "NOVorSteeringCue", "◇", UiColors.HudAmber,
                new Vector2(0f, -quarterHeight), 15);
            _steeringCue.rectTransform.sizeDelta = new Vector2(24f, 20f);
        }
    }
}
