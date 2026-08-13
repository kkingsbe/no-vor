using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using NOVor.Core;

namespace NOVor.UI
{
    public sealed class PanelHsi : MonoBehaviour
    {
        private float _size;
        private RectTransform _compassCard;
        private RectTransform _bearingPointer;
        private RectTransform _courseAssembly;
        private RectTransform _deviationBar;
        private TextMeshProUGUI _courseReadout;
        private TextMeshProUGUI _toFromFlag;

        public event Action<float> CourseAdjusted;

        public void Init(float size)
        {
            _size = size;
            var root = (RectTransform)transform;
            root.sizeDelta = new Vector2(size, size);
            var hit = gameObject.GetComponent<Image>() ?? gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0.001f);

            _compassCard = MakeContainer(root, "CompassCard");
            BuildCompassCard(_compassCard);

            _bearingPointer = MakeContainer(root, "BearingPointer");
            MakeRect(_bearingPointer, "BearingShaft", UiColors.PanelMuted,
                new Vector2(2f, size * 0.68f), Vector2.zero);
            MakeRect(_bearingPointer, "BearingHead", UiColors.PanelMuted,
                new Vector2(9f, 9f), new Vector2(0f, size * 0.34f));

            _courseAssembly = MakeContainer(root, "CourseAssembly");
            MakeRect(_courseAssembly, "CourseShaft", UiColors.Amber,
                new Vector2(3f, size * 0.7f), Vector2.zero);
            MakeRect(_courseAssembly, "CourseHead", UiColors.Amber,
                new Vector2(12f, 12f), new Vector2(0f, size * 0.35f));
            _deviationBar = MakeRect(_courseAssembly, "DeviationBar", UiColors.PanelText,
                new Vector2(4f, size * 0.38f), Vector2.zero);

            MakeRect(root, "AircraftWing", UiColors.PanelText,
                new Vector2(size * 0.18f, 3f), Vector2.zero);
            MakeRect(root, "AircraftNose", UiColors.PanelText,
                new Vector2(3f, size * 0.10f), new Vector2(0f, size * 0.04f));
            MakeRect(root, "LubberLine", UiColors.Amber,
                new Vector2(5f, 15f), new Vector2(0f, size * 0.43f));

            _courseReadout = MakeText(root, "CourseReadout", "CRS 000°", 15,
                UiColors.Amber, TextAlignmentOptions.Center,
                new Vector2(size, 22f), new Vector2(0f, size * 0.5f - 1f));
            MakeRect(root, "FlagWell", UiColors.ChromeRaised,
                new Vector2(44f, 20f), new Vector2(0f, -size * 0.5f + 11f));
            _toFromFlag = MakeText(root, "ToFrom", "TO", 14,
                UiColors.Amber, TextAlignmentOptions.Center,
                new Vector2(40f, 18f), new Vector2(0f, -size * 0.5f + 11f));

            var selector = gameObject.GetComponent<HsiCourseSelector>() ?? gameObject.AddComponent<HsiCourseSelector>();
            selector.Delta += delta => CourseAdjusted?.Invoke(delta);
        }

        public void SetData(CdiData data)
        {
            if (_compassCard == null) return;
            bool manual = data.Mode == CourseMode.Manual;
            _compassCard.localEulerAngles = new Vector3(0f, 0f, data.Heading);
            _bearingPointer.localEulerAngles = new Vector3(0f, 0f, -(data.Bearing - data.Heading));
            _courseAssembly.localEulerAngles = new Vector3(0f, 0f, -(data.Course - data.Heading));
            _deviationBar.gameObject.SetActive(manual);
            _deviationBar.anchoredPosition = new Vector2(data.Deflection * _size * 0.22f, 0f);
            _courseReadout.text = manual
                ? $"CRS {Mathf.RoundToInt(data.Course):000}°  {ScaleTag(data.ScaleMode)}"
                : $"BRG {Mathf.RoundToInt(data.Bearing):000}°";
            _toFromFlag.text = manual ? data.ToStation ? "TO" : "FR" : "DIR";
            _toFromFlag.color = manual && !data.ToStation ? UiColors.PanelMuted : UiColors.Amber;
        }

        private static string ScaleTag(CdiScaleMode mode)
        {
            switch (mode)
            {
                case CdiScaleMode.Approach: return "APP";
                case CdiScaleMode.Terminal: return "TERM";
                case CdiScaleMode.Enroute: return "ENR";
                default: return "FIX";
            }
        }

        private void BuildCompassCard(RectTransform parent)
        {
            float radius = _size * 0.405f;
            for (int degrees = 0; degrees < 360; degrees += 10)
            {
                bool major = degrees % 30 == 0;
                float radians = degrees * Mathf.Deg2Rad;
                var tick = MakeRect(parent, "Tick" + degrees, major ? UiColors.PanelText : UiColors.Rule,
                    new Vector2(major ? 2f : 1f, major ? 11f : 6f),
                    new Vector2(Mathf.Sin(radians) * radius, Mathf.Cos(radians) * radius));
                tick.localEulerAngles = new Vector3(0f, 0f, -degrees);
            }

            MakeCardinal(parent, "N", 0f, radius - 19f);
            MakeCardinal(parent, "E", 90f, radius - 19f);
            MakeCardinal(parent, "S", 180f, radius - 19f);
            MakeCardinal(parent, "W", 270f, radius - 19f);
        }

        private void MakeCardinal(RectTransform parent, string label, float degrees, float radius)
        {
            float radians = degrees * Mathf.Deg2Rad;
            MakeText(parent, label, label, 12, UiColors.PanelMuted, TextAlignmentOptions.Center,
                new Vector2(18f, 18f),
                new Vector2(Mathf.Sin(radians) * radius, Mathf.Cos(radians) * radius));
        }

        private static RectTransform MakeContainer(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
            return rt;
        }

        private static RectTransform MakeRect(Transform parent, string name, Color color,
            Vector2 size, Vector2 position)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = position;
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return rt;
        }

        private static TextMeshProUGUI MakeText(Transform parent, string name, string text,
            int size, Color color, TextAlignmentOptions alignment, Vector2 rectSize, Vector2 position)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = rectSize;
            rt.anchoredPosition = position;
            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.font = FontLoader.GetDefaultFont();
            tmp.fontSize = size;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = color;
            tmp.alignment = alignment;
            tmp.text = text;
            tmp.raycastTarget = false;
            return tmp;
        }
    }
}
