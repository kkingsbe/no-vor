using UnityEngine;
using UnityEngine.UI;
using NOVor.Core;

namespace NOVor.UI
{
    public class CdiInstrument : MonoBehaviour
    {
        private const float ScaleHalfWidthPx = 90f;
        private static readonly Color Hidden = new Color(0f, 0f, 0f, 0f);

        private Text _fieldText;
        private Text _actionText;
        private GameObject _manualGroup;
        private Text _offScaleLeft;
        private Text _offScaleRight;
        private Text _scaleLabel;
        private Text _toFromFlag;
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

            bool manual = data.Mode == CourseMode.Manual;
            _manualGroup.SetActive(manual);
            _fieldText.text = CompactFieldName(data.AirportName) + "  " + FormatRange(data.DistanceNm);

            if (manual)
                SetManualData(data);
            else
                SetAutoData(data);
        }

        private void SetManualData(CdiData data)
        {
            float magnitude = Mathf.Abs(data.CrossTrackNm);
            string side = data.CrossTrackNm > 0.005f ? "R" : data.CrossTrackNm < -0.005f ? "L" : "ON";
            _actionText.text = "XTK " + FormatCrossTrack(magnitude) + " " + side;

            bool offScale = data.FullScaleNm > 0f && magnitude >= data.FullScaleNm;
            _needle.gameObject.SetActive(!offScale);
            _needle.anchoredPosition = new Vector2(data.Deflection * ScaleHalfWidthPx, 0f);
            _offScaleLeft.color = offScale && data.Deflection < 0f ? UiColors.HudAmber : Hidden;
            _offScaleRight.color = offScale && data.Deflection > 0f ? UiColors.HudAmber : Hidden;
            _scaleLabel.text = FormatScale(data.FullScaleNm);
            _toFromFlag.text = data.ToStation ? "▲ TO" : "▼ FR";
        }

        private void SetAutoData(CdiData data)
        {
            string command = data.SteeringError > 0.5f ? "R" : data.SteeringError < -0.5f ? "L" : "ON";
            _actionText.text = "CMD " + Mathf.Abs(data.SteeringError).ToString("F0") + "° " + command;
        }

        private static string CompactFieldName(string value)
        {
            string compact = (value ?? "NAV").ToUpperInvariant()
                .Replace("ANNEX CLASS CARRIER", "ANNEX CV")
                .Replace("INTERNATIONAL", "INTL")
                .Replace("AIRFIELD", "FLD")
                .Replace("AIRBASE", "")
                .Trim();
            while (compact.Contains("  ")) compact = compact.Replace("  ", " ");
            if (compact.Length > 10) compact = compact.Substring(0, 10).TrimEnd();
            return compact.Length > 0 ? compact : "NAV";
        }

        private static string FormatRange(float distanceNm)
        {
            return distanceNm < 100f ? distanceNm.ToString("F1") + "NM" : distanceNm.ToString("F0") + "NM";
        }

        private static string FormatCrossTrack(float crossTrackNm)
        {
            return (crossTrackNm < 10f ? crossTrackNm.ToString("F1") : crossTrackNm.ToString("F0")) + "NM";
        }

        private static string FormatScale(float fullScaleNm)
        {
            string value = fullScaleNm < 10f ? fullScaleNm.ToString("0.#") : fullScaleNm.ToString("F0");
            return value + "NM";
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
            rt.sizeDelta = new Vector2(320f, 118f);

            _fieldText = HudGlyphs.MakeText("FieldRange", UiColors.TextSecondary, 12, FontStyle.Bold);
            _fieldText.rectTransform.SetParent(rt, false);
            HudGlyphs.Place(_fieldText.rectTransform, new Vector2(0f, 48f), new Vector2(260f, 18f));

            BuildManualScale(rt);

            _actionText = HudGlyphs.MakeText("Action", UiColors.HudGreen, 16, FontStyle.Bold);
            _actionText.rectTransform.SetParent(rt, false);
            HudGlyphs.Place(_actionText.rectTransform, new Vector2(0f, -47f), new Vector2(260f, 22f));

            SetVisible(false);
        }

        private void BuildManualScale(RectTransform parent)
        {
            var scale = MakeGroup(parent, "ManualCdi", new Vector2(0f, -10f), 24f);
            _manualGroup = scale.gameObject;

            foreach (float x in new[] { -ScaleHalfWidthPx, -ScaleHalfWidthPx * 0.5f,
                         ScaleHalfWidthPx * 0.5f, ScaleHalfWidthPx })
                HudGlyphs.MakeCue(scale, "DeviationDot", "•", UiColors.HudGreenDim, new Vector2(x, 0f), 14);

            HudGlyphs.MakeCue(scale, "CenterIndex", "▽", UiColors.HudGreen, Vector2.zero, 16);

            _needle = HudGlyphs.MakeRect("DeviationNeedle", UiColors.HudGreen);
            _needle.SetParent(scale, false);
            HudGlyphs.Place(_needle, Vector2.zero, new Vector2(3f, 20f));

            _offScaleLeft = HudGlyphs.MakeCue(scale, "OffScaleLeft", HudGlyphs.OffScaleLeft,
                UiColors.HudAmber, new Vector2(-(ScaleHalfWidthPx + 18f), 0f), 16);
            _offScaleRight = HudGlyphs.MakeCue(scale, "OffScaleRight", HudGlyphs.OffScaleRight,
                UiColors.HudAmber, new Vector2(ScaleHalfWidthPx + 18f, 0f), 16);
            _offScaleLeft.color = Hidden;
            _offScaleRight.color = Hidden;

            _scaleLabel = HudGlyphs.MakeCue(scale, "FullScale", "1NM", UiColors.TextSecondary,
                new Vector2(ScaleHalfWidthPx + 30f, 10f), 9);
            _scaleLabel.alignment = TextAnchor.MiddleLeft;
            _scaleLabel.rectTransform.sizeDelta = new Vector2(42f, 16f);

            _toFromFlag = HudGlyphs.MakeCue(scale, "ToFrom", "▲ TO", UiColors.TextSecondary,
                new Vector2(-(ScaleHalfWidthPx + 32f), 10f), 9);
            _toFromFlag.alignment = TextAnchor.MiddleRight;
            _toFromFlag.rectTransform.sizeDelta = new Vector2(48f, 16f);
        }

        private static RectTransform MakeGroup(RectTransform parent, string name, Vector2 position, float height)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            HudGlyphs.Place(rt, position, new Vector2(ScaleHalfWidthPx * 2f + 90f, height));
            return rt;
        }
    }
}
