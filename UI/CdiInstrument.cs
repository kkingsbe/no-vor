using NOVor.Core;
using UnityEngine;
using UnityEngine.UI;

namespace NOVor.UI
{
    public class CdiInstrument : MonoBehaviour
    {
        private const float BlockWidth = 288f;
        private const float ManualHeight = 136f;
        private const float DirectHeight = 104f;
        private const float ScaleHalfWidthPx = 72f;
        private static readonly Color Hidden = new Color(0f, 0f, 0f, 0f);

        private Text _fieldText;
        private Text _actionText;
        private Text _courseText;
        private Text _etaText;
        private GameObject _manualGroup;
        private Text _offScaleLeft;
        private Text _offScaleRight;
        private Text _scaleLabel;
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

        public void SetData(CdiData data)
        {
            if (!isActiveAndEnabled) return;

            var readout = CockpitPresentation.Build(new CockpitPresentationInput
            {
                AirportName = data.AirportName,
                DistanceNm = data.DistanceNm,
                Course = data.Course,
                Bearing = data.Bearing,
                CommandHeading = data.CommandHeading,
                GroundSpeedKnots = data.GroundSpeedKnots,
                EtaSeconds = data.EtaSeconds,
                FullScaleNm = data.FullScaleNm,
                RunwayLabel = data.RunwayLabel,
                RunwayPhase = data.RunwayPhase,
                HasRunway = data.HasRunway,
                Manual = data.Mode != CourseMode.Auto,
                ToStation = data.ToStation,
                OffScale = data.OffScale,
                HasEta = data.HasEta,
                ScaleMode = data.ScaleMode,
                Units = Plugin.DisplayUnits.Value
            });

            _fieldText.text = readout.TargetLine;
            _courseText.text = readout.ContextLine;
            _actionText.text = readout.CommandLine;
            _etaText.text = readout.SupportLine;
            _scaleLabel.text = readout.ScaleLine;
            _actionText.color = readout.CommandAttention ? UiColors.HudAmber : UiColors.HudContext;
            _manualGroup.SetActive(readout.ShowCdi);
            ApplyLayout(readout.ShowCdi);

            if (readout.ShowCdi) SetManualData(data);
        }

        private void SetManualData(CdiData data)
        {
            _needle.gameObject.SetActive(!data.OffScale);
            _needle.anchoredPosition = new Vector2(data.Deflection * ScaleHalfWidthPx, 0f);
            _offScaleLeft.color = data.OffScale && data.Deflection < 0f ? UiColors.HudAmber : Hidden;
            _offScaleRight.color = data.OffScale && data.Deflection > 0f ? UiColors.HudAmber : Hidden;
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

            _fieldText = HudGlyphs.MakeText("FieldRange", UiColors.HudContext, 12, FontStyle.Bold);
            _fieldText.rectTransform.SetParent(rt, false);

            _courseText = HudGlyphs.MakeText("Course", UiColors.HudSupport, 11, FontStyle.Bold);
            _courseText.rectTransform.SetParent(rt, false);

            BuildManualScale(rt);

            _actionText = HudGlyphs.MakeText("Action", UiColors.HudContext, 17, FontStyle.Bold);
            _actionText.rectTransform.SetParent(rt, false);

            _etaText = HudGlyphs.MakeText("Eta", UiColors.HudSupport, 10, FontStyle.Normal);
            _etaText.rectTransform.SetParent(rt, false);

            ApplyLayout(false);
            SetVisible(false);
        }

        private void BuildManualScale(RectTransform parent)
        {
            var scale = MakeGroup(parent, "ManualCdi", new Vector2(0f, 1f), 30f);
            _manualGroup = scale.gameObject;

            var rail = HudGlyphs.MakeRect("DeviationRail", UiColors.HudGreenDim);
            rail.SetParent(scale, false);
            HudGlyphs.Place(rail, Vector2.zero, new Vector2(ScaleHalfWidthPx * 2f, 2f));

            foreach (float x in new[] { -ScaleHalfWidthPx, -ScaleHalfWidthPx * 0.5f, 0f,
                         ScaleHalfWidthPx * 0.5f, ScaleHalfWidthPx })
            {
                var tick = HudGlyphs.MakeRect("DeviationTick", UiColors.HudGreenDim);
                tick.SetParent(scale, false);
                HudGlyphs.Place(tick, new Vector2(x, 0f), new Vector2(2f, x == 0f ? 20f : 10f));
            }

            _needle = HudGlyphs.MakeRect("DeviationNeedle", UiColors.HudGreen);
            _needle.SetParent(scale, false);
            HudGlyphs.Place(_needle, Vector2.zero, new Vector2(3f, 20f));

            _offScaleLeft = HudGlyphs.MakeCue(scale, "OffScaleLeft", HudGlyphs.OffScaleLeft,
                UiColors.HudAmber, new Vector2(-(ScaleHalfWidthPx + 15f), 0f), 16);
            _offScaleRight = HudGlyphs.MakeCue(scale, "OffScaleRight", HudGlyphs.OffScaleRight,
                UiColors.HudAmber, new Vector2(ScaleHalfWidthPx + 15f, 0f), 16);
            _offScaleLeft.color = Hidden;
            _offScaleRight.color = Hidden;

            _scaleLabel = HudGlyphs.MakeCue(scale, "FullScale", string.Empty, UiColors.HudSupport,
                new Vector2(0f, 11f), 9);
            _scaleLabel.rectTransform.sizeDelta = new Vector2(150f, 14f);
        }

        private void ApplyLayout(bool manual)
        {
            var rt = (RectTransform)transform;
            rt.sizeDelta = new Vector2(BlockWidth, manual ? ManualHeight : DirectHeight);
            HudGlyphs.Place(_fieldText.rectTransform, new Vector2(0f, manual ? 52f : 36f),
                new Vector2(270f, 18f));
            HudGlyphs.Place(_courseText.rectTransform, new Vector2(0f, manual ? 30f : 14f),
                new Vector2(270f, 18f));
            HudGlyphs.Place(_actionText.rectTransform, new Vector2(0f, manual ? -34f : -14f),
                new Vector2(270f, 22f));
            HudGlyphs.Place(_etaText.rectTransform, new Vector2(0f, manual ? -58f : -38f),
                new Vector2(270f, 16f));
        }

        private static RectTransform MakeGroup(RectTransform parent, string name, Vector2 position, float height)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            HudGlyphs.Place(rt, position, new Vector2(210f, height));
            return rt;
        }
    }
}
