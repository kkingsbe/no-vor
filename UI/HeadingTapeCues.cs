using NOVor.Core;
using UnityEngine;
using UnityEngine.UI;

namespace NOVor.UI
{
    public class HeadingTapeCues : MonoBehaviour
    {
        private RawImage _compass;
        private HudCueIcon _courseCue;
        private HudCueIcon _commandCue;
        private bool _hasSmoothedDeltas;
        private float _smoothedCourseDelta;
        private float _smoothedCommandDelta;

        public void Initialize(RawImage compass)
        {
            _compass = compass;
            Build();
            _hasSmoothedDeltas = false;
        }

        public void SetVisible(bool visible)
        {
            if (!visible) _hasSmoothedDeltas = false;
            gameObject.SetActive(visible);
        }

        public void SetData(CdiData data)
        {
            if (!isActiveAndEnabled || _compass == null) return;

            float visibleDegrees = Mathf.Abs(_compass.uvRect.width) * 360f;
            if (visibleDegrees < 1f) visibleDegrees = 90f;
            float halfSpan = visibleDegrees * 0.5f;
            float courseDelta = (float)NavMath.DeltaAngleDegrees(data.Heading, data.Course);
            float commandDelta = (float)NavMath.DeltaAngleDegrees(data.Heading, data.CommandHeading);
            float maxStep = Plugin.HeadingCueResponseDegreesPerSecond.Value * Time.unscaledDeltaTime;

            if (!_hasSmoothedDeltas)
            {
                _smoothedCourseDelta = courseDelta;
                _smoothedCommandDelta = commandDelta;
                _hasSmoothedDeltas = true;
            }
            else
            {
                _smoothedCourseDelta = Mathf.MoveTowardsAngle(_smoothedCourseDelta, courseDelta, maxStep);
                _smoothedCommandDelta = Mathf.MoveTowardsAngle(_smoothedCommandDelta, commandDelta, maxStep);
            }

            float lane = _compass.rectTransform.rect.height * 0.25f;
            SetCue(_courseCue, _smoothedCourseDelta, halfSpan, lane, true);
            SetCue(_commandCue, _smoothedCommandDelta, halfSpan, -lane, false);
        }

        private void SetCue(HudCueIcon cue, float delta, float halfSpan, float laneY,
            bool pointOutward)
        {
            float halfWidth = Mathf.Max(1f, _compass.rectTransform.rect.width * 0.5f - 12f);
            bool offScale = Mathf.Abs(delta) > halfSpan;
            float x = Mathf.Clamp(delta / halfSpan, -1f, 1f) * halfWidth;
            cue.Rect.anchoredPosition = new Vector2(x, laneY);
            cue.Rect.localEulerAngles = pointOutward && offScale
                ? new Vector3(0f, 0f, delta < 0f ? -90f : 90f)
                : Vector3.zero;
        }

        private void Build()
        {
            var rt = (RectTransform)transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);

            _courseCue = HudCueIcon.CreateCourse(rt);
            _commandCue = HudCueIcon.CreateCommand(rt);
        }
    }
}
